using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DevDock.Desktop.Models;

namespace DevDock.Desktop.Controls;

public partial class StatusIndicator : UserControl
{
    public static readonly StyledProperty<ServiceState> StateProperty = AvaloniaProperty.Register<StatusIndicator, ServiceState>(nameof(State));
    public static readonly StyledProperty<bool> ShowTextProperty = AvaloniaProperty.Register<StatusIndicator, bool>(nameof(ShowText), true);
    public ServiceState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool ShowText { get => GetValue(ShowTextProperty); set => SetValue(ShowTextProperty, value); }
    public IBrush DotBrush => Application.Current!.FindResource(State switch
    {
        ServiceState.Running or ServiceState.Healthy => "SuccessBrush",
        ServiceState.Warning => "WarningBrush",
        ServiceState.Stopped or ServiceState.Error => "ErrorBrush",
        _ => "UnknownBrush"
    }) as IBrush ?? Brushes.Gray;
    public StatusIndicator() => InitializeComponent();
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty) RaisePropertyChanged(DotBrushProperty, Brushes.Transparent, DotBrush);
    }
    public static readonly DirectProperty<StatusIndicator, IBrush> DotBrushProperty = AvaloniaProperty.RegisterDirect<StatusIndicator, IBrush>(nameof(DotBrush), o => o.DotBrush);
}
