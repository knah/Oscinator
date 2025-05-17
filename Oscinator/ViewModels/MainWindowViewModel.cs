using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Oscinator.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private AvatarStateViewModel? myCurrentAvatarState;
    private bool myShowRemoteAppSelector;
    private bool myUseTreeView;

    public AvatarStateViewModel? CurrentAvatarState
    {
        get => myCurrentAvatarState;
        set
        {
            if (Equals(value, myCurrentAvatarState)) return;
            myCurrentAvatarState = value;
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(CurrentAvatarState)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool ShowRemoteAppSelector
    {
        get => myShowRemoteAppSelector;
        set
        {
            if (value == myShowRemoteAppSelector) return;
            myShowRemoteAppSelector = value;
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ShowRemoteAppSelector)));
        }
    }


    public bool UseTreeViewForParameters
    {
        get => myUseTreeView;
        set
        {
            if (value == myUseTreeView) return;
            myUseTreeView = value;
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(UseTreeViewForParameters)));
        }
    }
}