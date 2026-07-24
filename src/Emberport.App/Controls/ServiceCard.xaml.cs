using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emberport.Models;

namespace Emberport.Controls;

public partial class ServiceCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(ServiceCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(ServiceCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VersionProperty =
        DependencyProperty.Register(
            nameof(Version), typeof(string), typeof(ServiceCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(
            nameof(Detail), typeof(string), typeof(ServiceCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status), typeof(ServiceStatus), typeof(ServiceCard),
            new PropertyMetadata(ServiceStatus.Stopped, OnVisualStateChanged));

    public static readonly DependencyProperty IsDetectedProperty =
        DependencyProperty.Register(
            nameof(IsDetected), typeof(bool), typeof(ServiceCard),
            new PropertyMetadata(true, OnVisualStateChanged));

    private readonly bool _isReady;

    public ServiceCard()
    {
        InitializeComponent();
        _isReady = true;
        UpdateVisualState();
    }

    public event EventHandler? StartRequested;

    public event EventHandler? StopRequested;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Version
    {
        get => (string)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public ServiceStatus Status
    {
        get => (ServiceStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public bool IsDetected
    {
        get => (bool)GetValue(IsDetectedProperty);
        set => SetValue(IsDetectedProperty, value);
    }

    private static void OnVisualStateChanged(DependencyObject source, DependencyPropertyChangedEventArgs args) =>
        ((ServiceCard)source).UpdateVisualState();

    private void UpdateVisualState()
    {
        if (!_isReady)
        {
            return;
        }

        if (!IsDetected)
        {
            Apply("StatusStoppedBrush", "Not detected", canStart: false, canStop: false);
            return;
        }

        switch (Status)
        {
            case ServiceStatus.Running:
                Apply("StatusRunningBrush", "Running", canStart: false, canStop: true);
                break;
            case ServiceStatus.Starting:
                Apply("StatusWarningBrush", "Starting", canStart: false, canStop: false);
                break;
            case ServiceStatus.Faulted:
                Apply("StatusErrorBrush", "Failed", canStart: true, canStop: false);
                break;
            default:
                Apply("StatusStoppedBrush", "Stopped", canStart: true, canStop: false);
                break;
        }
    }

    private void Apply(string brushKey, string label, bool canStart, bool canStop)
    {
        StatusDot.Fill = (Brush)FindResource(brushKey);
        StatusLabel.Text = label;
        StartButton.IsEnabled = canStart;
        StopButton.IsEnabled = canStop;
    }

    private void OnStartClick(object sender, RoutedEventArgs e) =>
        StartRequested?.Invoke(this, EventArgs.Empty);

    private void OnStopClick(object sender, RoutedEventArgs e) =>
        StopRequested?.Invoke(this, EventArgs.Empty);
}