using Avalonia;
using Avalonia.Controls;

namespace DevDock.Desktop.Controls;

public partial class TechnologyBadge : UserControl
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<TechnologyBadge, string>(nameof(Text), "");
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public TechnologyBadge() => InitializeComponent();
}
