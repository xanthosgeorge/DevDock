using Avalonia.Controls;
using DevDock.Desktop.ViewModels;

namespace DevDock.Desktop.Views;

public partial class ProjectImportWindow : Window
{
    public ProjectImportWindow() => InitializeComponent();

    public ProjectImportWindow(ProjectImportViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += CloseWithResult;
        Closed += (_, _) => viewModel.CloseRequested -= CloseWithResult;
    }

    private void CloseWithResult(bool confirmed) => Close(confirmed);
}
