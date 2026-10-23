using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevDock.Desktop.ViewModels;
using DevDock.Desktop.Views;
using DevDock.Desktop.Services;
using Avalonia.Threading;

namespace DevDock.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var discovery = new ProjectDiscoveryService();
            var runner = new ProjectRunnerService();
            var dashboard = new DashboardViewModel(discovery, new AvaloniaProjectImportInteraction(window),
                new AvaloniaProjectWorkspaceInteraction(window), new GitCliService(new CommandRunner()), runner, new ProjectRunConfigurationService(discovery));
            window.DataContext = new MainWindowViewModel(dashboard);
            desktop.MainWindow = window;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            var stopped = false;
            window.Closing += async (_, e) =>
            {
                if (stopped) return;
                e.Cancel = true;
                if (dashboard.Projects.Any(p => p.Workspace is { IsBusy: true } || p.Workspace?.Git.IsBusy == true))
                {
                    dashboard.Feedback = "Wait for the current project/Git operation to finish before closing DevDock.";
                    return;
                }
                window.IsEnabled = false;
                try
                {
                    await runner.StopAllAsync();
                    stopped = true;
                    Dispatcher.UIThread.Post(window.Close);
                }
                catch (Exception ex) { dashboard.Feedback = "Unable to stop project processes: " + ex.Message; window.IsEnabled = true; }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
