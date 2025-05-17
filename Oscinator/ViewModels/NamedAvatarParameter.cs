using System.ComponentModel;
using Avalonia.Interactivity;
using Oscinator.Core;

namespace Oscinator.ViewModels;

public class NamedAvatarParameter : INotifyPropertyChanged
{
    private ParameterVariant myValue;

    public NamedAvatarParameter(string name, string displayName, bool isReadOnly)
    {
        Name = name;
        DisplayName = displayName;
        IsReadOnly = isReadOnly;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public readonly bool IsReadOnly;
    
    public int IntRangeMinimum { get; }
    public int IntRangeMaximum { get; }
    
    public float FloatRangeMinimum { get; }
    public float FloatRangeMaximum { get; }
    public float FloatRangeStep { get; }

    public ParameterVariant Value
    {
        get => myValue;
        set
        {
            var typeChanged = myValue.Type != value.Type;
            myValue = value;
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ReadOnlyText)));
            if (!typeChanged) return;
            
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(CheckboxVisible)));
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(IntControlVisible)));
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(FloatControlVisible)));
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(UnknownTextVisible)));
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ReadOnlyTextVisible)));
            
        }
    }

    public bool CheckboxVisible => !IsReadOnly && Value.Type == ParameterType.Bool;
    public bool IntControlVisible => !IsReadOnly && Value.Type == ParameterType.Int;
    public bool FloatControlVisible => !IsReadOnly && Value.Type == ParameterType.Float;
    public bool UnknownTextVisible => !IsReadOnly && Value.Type == ParameterType.Unknown;
    public bool ReadOnlyTextVisible => IsReadOnly;

    public string ReadOnlyText
    {
        get
        {
            return myValue.Type switch
            {
                ParameterType.Float => myValue.FloatValue.ToString("F4"),
                ParameterType.Int => myValue.IntValue.ToString(),
                ParameterType.Bool => myValue.BoolValue.ToString(),
                _ => "unknown value (read-only)",
            };
        }
    }
    
    private void Item_CheckedChanged(object? sender, RoutedEventArgs e) {}

    public event PropertyChangedEventHandler? PropertyChanged;
}