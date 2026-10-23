using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using DevDock.Desktop.ViewModels;

namespace DevDock.Desktop.Views;

public partial class ProjectDetailsWindow : Window
{
    private bool _scrollPending;
    public ProjectDetailsWindow() => InitializeComponent();
    public ProjectDetailsWindow(ProjectWorkspaceViewModel workspace) : this()
    {
        DataContext = workspace;
        workspace.Output.CollectionChanged += OnOutputChanged;
        Closed += (_, _) => workspace.Output.CollectionChanged -= OnOutputChanged;
        Opened += async (_, _) => { if (workspace.Git.RefreshCommand.CanExecute(null)) await workspace.Git.RefreshCommand.ExecuteAsync(null); };
    }
    private void OnOutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_scrollPending && DataContext is ProjectWorkspaceViewModel { AutoScroll: true })
        {
            _scrollPending = true;
            Dispatcher.UIThread.Post(() => { _scrollPending = false; OutputScroll.ScrollToEnd(); }, DispatcherPriority.Background);
        }
    }
}
