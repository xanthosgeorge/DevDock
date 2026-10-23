using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DevDock.Desktop.ViewModels;

namespace DevDock.Desktop.Controls;

public partial class ProjectCard : UserControl
{
    public ProjectCard() => InitializeComponent();
    private void CardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Avalonia.Visual visual && (visual is Button || visual.FindAncestorOfType<Button>() != null)) return;
        if (DataContext is ProjectViewModel project) project.SelectCommand.Execute(null);
    }
}
