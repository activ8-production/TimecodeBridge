using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TimecodeBridge.ViewModels;

public abstract class DispatcherViewModel : ObservableObject
{
    protected Dispatcher Dispatcher { get; } = Dispatcher.CurrentDispatcher;

    protected void RunOnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess())
            action();
        else
            Dispatcher.BeginInvoke(action);
    }
}
