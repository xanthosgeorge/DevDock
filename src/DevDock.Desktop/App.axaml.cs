using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DevDock.Desktop.ViewModels;
using DevDock.Desktop.Views;
using DevDock.Desktop.Services;

namespace DevDock.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var dashboard = new DashboardViewModel(new ProjectDiscoveryService(), new AvaloniaProjectImportInteraction(window));
            window.DataContext = new MainWindowViewModel(dashboard);
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
