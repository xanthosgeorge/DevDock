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
            var interaction = new AvaloniaProjectWorkspaceInteraction(window);
            var dashboard = new DashboardViewModel(discovery, new AvaloniaProjectImportInteraction(window),
                interaction, new GitCliService(new CommandRunner()), runner, new ProjectRunConfigurationService(discovery),
                platform: new AvaloniaPlatformService(window), portInteraction: interaction,
                workspaceStore: new JsonWorkspaceStore(JsonWorkspaceStore.DefaultFilePath));
            var main = new MainWindowViewModel(dashboard);
            window.DataContext = main;
            desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                main.Metrics.Start();
                await dashboard.InitializeAsync();
                if (dashboard.Ports.RefreshCommand.CanExecute(null)) await dashboard.Ports.RefreshCommand.ExecuteAsync(null);
            };
            window.Closed += async (_, _) => await main.Metrics.StopAsync();
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            var stopped = false;
            window.Closing += async (_, e) =>
            {
                if (stopped) return;
                e.Cancel = true;
                if (dashboard.IsLoadingWorkspace || dashboard.IsSavingWorkspace || dashboard.AddProjectCommand.IsRunning
                    || dashboard.Ports.IsBusy || dashboard.Projects.Any(p => p.IsRemoving || p.Workspace is { IsBusy: true } || p.Workspace?.Git.IsBusy == true))
                {
                    dashboard.Feedback = "Wait for the current project/Git/ports operation to finish before closing DevDock.";
                    return;
                }
                window.IsEnabled = false;
                try
                {
                    await runner.StopAllAsync();
                    await main.Metrics.StopAsync();
                    stopped = true;
                    Dispatcher.UIThread.Post(window.Close);
                }
                catch (Exception ex) { dashboard.Feedback = "Unable to stop project processes: " + ex.Message; window.IsEnabled = true; }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
