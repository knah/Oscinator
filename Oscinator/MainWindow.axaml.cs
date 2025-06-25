using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using NanoOsc;
using Oscinator.Core;
using Oscinator.ViewModels;
using Vrc.OscQuery;

namespace Oscinator;

public partial class MainWindow : Window
{
    private MultiRemoteListener? myMultiRemoteListener;
    private readonly ConcurrentDictionary<AvatarState, AvatarStateViewModel> myStateViewModels = new();

    private static readonly ILogger Logger = LogUtils.LoggerFor<MainWindow>();

    public ObservableCollection<LogEntryModel> LogEntries { get; } = new();
    public ObservableCollection<ServiceModel> Services { get; } = new();
    public ObservableCollection<InterfaceItemModel> Interfaces { get; } = new();
    public ObservableCollection<string> RemoteApplications { get; } = new();

    public MainWindowViewModel ViewModel { get; } = new();

    public IComparer ParameterSortComparer { get; } = new ParameterSortComparerImpl();

    private class ParameterSortComparerImpl: IComparer
    {
        public int Compare(object? x, object? y)
        {
            var xElem = x as NamedAvatarParameter;
            var yElem = y as NamedAvatarParameter;
            if (ReferenceEquals(xElem, yElem)) return 0;
            if (xElem == null)
                return -1;
            if (yElem == null)
                return 1;
            
            if (xElem.IsReadOnly == yElem.IsReadOnly)
                return string.Compare(xElem.Name, yElem.Name, StringComparison.OrdinalIgnoreCase);
            return xElem.IsReadOnly ? 1 : -1;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_VerticalScrollBar")]
    private static extern ScrollBar GetScrollBar(DataGrid grid);
    
    public MainWindow()
    {
        InitializeComponent();

        AvatarParametersGrid.Columns[0].CustomSortComparer = ParameterSortComparer;
        DataContext = this;

        LogUtils.OnLog += LogUtilsOnOnLog;

        RefreshInterfaces();

        DispatcherTimer.RunOnce(() =>
        {
            UpdateSort();
            // Start as non-resizable to trick tiling WMs on Linux into floating us
            CanResize = true;
            
            GetScrollBar(AvatarParametersGrid).AllowAutoHide = false;
        }, TimeSpan.FromSeconds(1));
    }

    private void RefreshInterfaces()
    {
        var selectedAddress = ((InterfaceItemModel?) InterfaceSelector.SelectedItem)?.BindAddress;
        
        Interfaces.Clear();
        Interfaces.Add(new InterfaceItemModel("localhost", IPAddress.Loopback));
        // Interfaces.Add(new InterfaceItemModel("All interfaces", IPAddress.Any));
        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            var address = iface.GetIPProperties().UnicastAddresses.FirstOrDefault(it => it.Address.AddressFamily == AddressFamily.InterNetwork);
            if (address == null) continue;
            
            Interfaces.Add(new InterfaceItemModel($"{iface.Name} ({address.Address})", address.Address));
        }

        var selectionIndex = Interfaces.Select((im, i) => (i, im.BindAddress)).FirstOrDefault(it => Equals(it.BindAddress, selectedAddress)).i;

        InterfaceSelector.SelectedIndex = selectionIndex;
    }

    private void CreateListenerAndState()
    {
        myMultiRemoteListener?.Dispose();
        myMultiRemoteListener = null;
        ViewModel.CurrentAvatarState = null;
        myStateViewModels.Clear();
        RemoteApplications.Clear();
        Services.Clear();

        var bindAddress = ((InterfaceItemModel?)InterfaceSelector.SelectedItem)?.BindAddress;
        if (bindAddress == null) return;
        var listener = new OscinatorListener(bindAddress);
        var multiListener = new MultiRemoteListener(listener);
        myMultiRemoteListener = multiListener;
        myMultiRemoteListener.OnRemoteSetChanged += DispatchRemoteSetChanged;
        
        listener.Discovery.OnAnyOscServiceRemoved += RemoveService;
        listener.Discovery.OnAnyOscServiceAdded += AddService;
    }

    private void DispatchRemoteSetChanged()
    {
        Dispatcher.UIThread.Post(RemoteApplicationSetChanged);
    }

    private void RemoteApplicationSetChanged()
    {
        var listener = myMultiRemoteListener;
        if (listener == null) return;
        var lastSelectedService = (string?)RemoteAppSelector.SelectionBoxItem;
        RemoteApplications.Clear();
        foreach (var state in listener.AvatarStates) 
            RemoteApplications.Add(state.HostInfo.Name);
        if (lastSelectedService != null && RemoteApplications.Contains(lastSelectedService)) 
            RemoteAppSelector.SelectedItem = lastSelectedService;

        if (ViewModel.CurrentAvatarState != null || RemoteApplications.Count <= 0) return;
        
        var randomState = listener.AvatarStates.FirstOrDefault();
        if (randomState != null)
        {
            ViewModel.CurrentAvatarState =
                myStateViewModels.GetOrAdd(randomState, static s => new AvatarStateViewModel(s));
            UpdateRemoteProcessLabel(randomState.HostInfo.Name);
        }


    }

    private void AddService(OscQueryServiceProfile profile)
    {
        var model = new ServiceModel(profile);
        if (!CheckAccess())
        {
            Dispatcher.UIThread.Post(m => Services.Add((ServiceModel) m!), model);
            return;
        }
        
        Services.Add(model);
    }
    
    private void RemoveService(OscQueryServiceProfile profile)
    {
        var model = new ServiceModel(profile);
        if (!CheckAccess())
        {
            Dispatcher.UIThread.Post(m => Services.Remove((ServiceModel) m!), model);
            return;
        }
        
        Services.Remove(model);
    }

    private void LogUtilsOnOnLog(LogLevel level, string category, string message)
    {
        var entry = new LogEntryModel
        {
            Level = level,
            Severity = LevelToShortString(level),
            Category = category,
            Message = message,
            Time = DateTime.Now,
        };
        
        if (CheckAccess())
            LogEntries.Add(entry);
        else
            Dispatcher.UIThread.Post((t) => LogEntries.Add((LogEntryModel) t!), entry);
    }

    private static string LevelToShortString(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRITICAL",
        LogLevel.None => "????",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };


    private void ShowBuiltInParametersCheck_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        foreach (var (key, value) in myStateViewModels) 
            value.IncludeReadOnlyParameters = ShowBuiltInParametersCheck.IsChecked == true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        myMultiRemoteListener?.Dispose();
    }

    private void Item_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        var checkBox = (CheckBox) sender!;
        var parameter = (NamedAvatarParameter) checkBox.DataContext!;

        if (parameter.IsReadOnly) return;

        var state = ViewModel.CurrentAvatarState;
        if (state == null || state.IsLocalChange) return;
        
        var currentValue = state[parameter.Name];
        if (currentValue is not { Type: ParameterType.Bool } || currentValue.Value.BoolValue == checkBox.IsChecked)
            return;

        using var messageMemory = MemoryPool<byte>.Shared.Rent(1024);
        var toSend = OscMessageBuilder.SimplePacket(messageMemory.Memory, AvatarState.AvatarParameterPrefixS + parameter.Name,
            checkBox.IsChecked == true);

        myMultiRemoteListener?.Listener.Send(toSend, state.EndPoint).NoAwait(Logger, "OSC message send");
    }

    private void ItemInt_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        var upDown = (NumericUpDown)sender!;
        var parameter = (NamedAvatarParameter) upDown.DataContext!;
        
        if (parameter.IsReadOnly) return;
        
        var state = ViewModel.CurrentAvatarState;
        if (state == null || state.IsLocalChange) return;
        
        var currentValue = state[parameter.Name];
        if (currentValue is not { Type: ParameterType.Int } || currentValue.Value.IntValue == upDown.Value)
            return;

        using var messageMemory = MemoryPool<byte>.Shared.Rent(1024);
        var toSend = OscMessageBuilder.SimplePacket(messageMemory.Memory, AvatarState.AvatarParameterPrefixS + parameter.Name,
            (int)(upDown.Value ?? 0));

        myMultiRemoteListener?.Listener.Send(toSend, state.EndPoint).NoAwait(Logger, "OSC message send");
    }

    private void ItemFloat_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        var upDown = (NumericUpDown)sender!;
        var parameter = (NamedAvatarParameter) upDown.DataContext!;
        
        if (parameter.IsReadOnly) return;
        
        var state = ViewModel.CurrentAvatarState;
        if (state == null || state.IsLocalChange) return;
        
        var currentValue = state[parameter.Name];
        if (currentValue is not { Type: ParameterType.Float } || currentValue.Value.FloatValue == (float) (upDown.Value ?? -100))
            return;

        var messageMemory = MemoryPool<byte>.Shared.Rent(1024);
        var toSend = OscMessageBuilder.SimplePacket(messageMemory.Memory, AvatarState.AvatarParameterPrefixS + parameter.Name, (float)(upDown.Value ?? 0));

        var valueTask = myMultiRemoteListener?.Listener.Send(toSend, state.EndPoint);
        valueTask?.GetAwaiter().OnCompleted(messageMemory.Dispose);
        valueTask?.NoAwait(Logger, "OSC message send");
    }

    private void InterfaceSelector_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var requestedAddress = ((InterfaceItemModel?)InterfaceSelector.SelectedItem)?.BindAddress;
        if (requestedAddress == null || Equals(requestedAddress, myMultiRemoteListener?.Listener.BindAddress)) return;
        
        CreateListenerAndState();
    }

    private void LogClearClick(object? sender, RoutedEventArgs e)
    {
        LogEntries.Clear();
    }

    private bool mySetSort;

    private void UpdateSort()
    {
        if (mySetSort) return;
        mySetSort = true;
        Dispatcher.UIThread.InvokeAsync(() => AvatarParametersGrid.Columns[0].Sort(ListSortDirection.Ascending));
    }

    private void AddDebugLogEntries(object? sender, RoutedEventArgs e)
    {
        Logger.LogCritical(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidCastException("Exception message")), "Critical exception");
        Logger.LogError(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidCastException("Different exception message")), "Error exception");
        Logger.LogError("Error without exception");
        Logger.LogWarning("Warning with a long text that under no circumstances would fit into a single and compact view that the vertical scrollable parameter list would target");
        Logger.LogInformation("Boring info");
        Logger.LogDebug("Even more boring debug");
        Logger.LogTrace("Abysmally boring trace. Why are we here even?");
    }

    private void UpdateSelectedRemoteApplication(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count < 1) return;
        var selectedItem = (string) e.AddedItems[0]!;

        var targetState = myMultiRemoteListener?.AvatarStates.FirstOrDefault(it => it.HostInfo.Name == selectedItem);
        ViewModel.CurrentAvatarState = targetState == null ? null : myStateViewModels.GetOrAdd(targetState, static state => new AvatarStateViewModel(state));
        targetState?.FetchCurrentAvatarId();
        
        UpdateRemoteProcessLabel(selectedItem);
    }

    private void UpdateRemoteProcessLabel(string remoteName)
    {
        RemoteAppProcessLabel.Content = myMultiRemoteListener?.GetProcessInfoForState(remoteName) ?? "";
    }

    private void RefreshButtonClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.CurrentAvatarState?.AvatarState.FetchCurrentAvatarId();
    }
}