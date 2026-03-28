using System.Threading;
using System.Windows;
using TCodeLaunchpad.App.Services;

namespace TCodeLaunchpad.App;

public partial class App : System.Windows.Application
{
    private const string SingletonMutexName = "Global\\TCodeLaunchpad.Singleton";
    private const string ActivationEventName = "Global\\TCodeLaunchpad.Activate";

    private MainWindow? _window;
    private SingleInstanceService? _singleInstance;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationWaitHandle;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);

        _singleInstance = new SingleInstanceService(SingletonMutexName);
        if (!_singleInstance.IsPrimaryInstance)
        {
            _activationEvent.Set();
            Shutdown();
            return;
        }

        try
        {
            _window = new MainWindow();
            _window.Show();
            _window.HideLauncher();

            _activationWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _activationEvent,
                (_, _) => Dispatcher.Invoke(() => _window?.ShowLauncherFromActivation()),
                state: null,
                millisecondsTimeOutInterval: -1,
                executeOnlyOnce: false);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "TCode Launchpad Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationWaitHandle?.Unregister(null);
        _activationEvent?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
