using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Controls;

public partial class ServiceHealthCard : UserControl
{
    private static readonly Brush ProblemDot = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Brush HealthyDot = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly Brush ProblemBorder = new SolidColorBrush(Color.FromRgb(0x4A, 0x2A, 0x2A));
    private static readonly Brush HealthyBorder = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3C));

    public ServiceHealthCard()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    /// <summary>Which service to inspect. Set it in XAML.</summary>
    public ServiceKind Kind { get; set; } = ServiceKind.Apache;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subtitle.Text = $"Find out why {Kind} refuses to start, without leaving Emberport.";
        SummaryText.Text = "Press Run checks.";
    }

    private void OnRunClick(object sender, RoutedEventArgs e) => Run();

    private void Run()
    {
        ServiceLauncher.Rescan();

        var findings = ServiceTroubleshooter.Inspect(Kind);
        var items = new List<FindingItem>();
        var problems = 0;

        foreach (var finding in findings)
        {
            if (finding.IsProblem)
            {
                problems++;
            }

            items.Add(new FindingItem(
                finding.Title,
                finding.Detail,
                finding.IsProblem ? ProblemDot : HealthyDot,
                finding.IsProblem ? ProblemBorder : HealthyBorder));
        }

        FindingList.ItemsSource = items;

        SummaryText.Text = problems == 0
            ? $"Everything looks fine for {Kind}."
            : $"{problems} issue(s) need attention.";
    }

    private sealed record FindingItem(string Title, string Detail, Brush DotColor, Brush BorderColor);
}