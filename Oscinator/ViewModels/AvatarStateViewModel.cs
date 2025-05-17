using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using Avalonia.Threading;
using Oscinator.Core;

namespace Oscinator.ViewModels;

public class AvatarStateViewModel
{
    public ObservableCollection<NamedAvatarParameter> ParameterItems { get; } = new();
    private readonly Dictionary<string, int> myParameterIndices = new();
    public readonly AvatarState AvatarState;

    private int myLocalChange;
    private bool myIncludeReadOnlyParameters;
    
    public bool IncludeReadOnlyParameters
    {
        get => myIncludeReadOnlyParameters;
        set
        {
            myIncludeReadOnlyParameters = value; 
            RebuildList();
        }
    }

    public bool IsLocalChange => myLocalChange > 0;

    public AvatarStateViewModel(AvatarState avatarState)
    {
        AvatarState = avatarState;
        EndPoint = AvatarState.HostInfo.OscEndPoint;

        AvatarState.ParameterChange += UpdateParameter;
        AvatarState.ParametersBulkChange += RebuildList;
        
        RebuildList();
    }

    public ParameterVariant? this[string name] => AvatarState[name];
    public readonly IPEndPoint EndPoint;

    private void RebuildList()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RebuildList);
            return;
        }
        
        using var cookie = new MyLocalChangeCookie(this);
        
        myParameterIndices.Clear();
        ParameterItems.Clear();
        
        foreach (var (key, value) in AvatarState.Parameters)
        {
            var isParameterReadOnly = AvatarState.IsParameterReadOnly(key);
            if (!IncludeReadOnlyParameters && isParameterReadOnly)
                continue;
            myParameterIndices[key] = ParameterItems.Count;
            ParameterItems.Add(new NamedAvatarParameter(key, AvatarState.GetDisplayName(key), isParameterReadOnly) { Value = value });
        }
    }
    
    
    private void UpdateParameter(string name)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(c => UpdateParameter((string) c!), name);
            return;
        }
        
        var isParameterReadOnly = AvatarState.IsParameterReadOnly(name);
        if (!IncludeReadOnlyParameters && isParameterReadOnly)
            return;

        var parameterValue = AvatarState[name];
        if (parameterValue == null)
        {
            // we'll get a bulk rebuild notification
            return;
        }

        using var cookie = new MyLocalChangeCookie(this);

        if (!myParameterIndices.TryGetValue(name, out var index))
        {
            myParameterIndices[name] = ParameterItems.Count;
            ParameterItems.Add(new NamedAvatarParameter(name, AvatarState.GetDisplayName(name), isParameterReadOnly) { Value = parameterValue.Value });
        }
        else
        {
            ParameterItems[index].Value = parameterValue.Value;
        }
    }
    
    private readonly struct MyLocalChangeCookie : IDisposable
    {
        private readonly AvatarStateViewModel myModel;

        public MyLocalChangeCookie(AvatarStateViewModel model)
        {
            myModel = model;
            myModel.myLocalChange++;
        }
        public void Dispose()
        {
            myModel.myLocalChange--;
        }
    }
}