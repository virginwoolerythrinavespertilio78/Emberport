using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        SummaryText.Text = "Press Run checks. Clean up removes servers left behind by a previous run.";
    }

    private void OnRunClick(object sender, RoutedEventArgs e) => Run();

    private void OnCleanUpClick(object sender, RoutedEventArgs e)
    {
        var leftovers = OrphanReaper.Describe();

        if (leftovers.Count == 0)
        {
            SummaryText.Text = "Nothing to clean up. No server from an earlier run is alive.";
            return;
        }

        var answer = MessageBox.Show(
            $"These servers are still running from an earlier session:\n\n{string.Join("\n", leftovers)}\n\nStop them now?",
            "Clean up",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        Mouse.OverrideCursor = Cursors.Wait;

        int removed;

        try
        {
            removed = OrphanReaper.Sweep();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        SummaryText.Text = removed == 0
            ? "Nothing could be stopped. Close the processes from Task Manager."
            : $"Stopped {removed} leftover process(es). You can start the service again.";

        Run();
    }

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