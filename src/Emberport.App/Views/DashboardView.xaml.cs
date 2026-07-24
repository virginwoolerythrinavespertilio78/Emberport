using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Emberport.Controls;
using Emberport.Models;
using Emberport.Services;

namespace Emberport.Views;

public partial class DashboardView : UserControl
{
    private static readonly ServiceKind[] ManagedServices =
        [ServiceKind.Apache, ServiceKind.MySql, ServiceKind.Redis];

    private static readonly Dictionary<ServiceKind, string> Glyphs = new()
    {
        [ServiceKind.Apache] = "\uE774",
        [ServiceKind.MySql] = "\uE8F1",
        [ServiceKind.Redis] = "\uE945",
    };

    private readonly IBinaryScanner _scanner = new BinaryScanner();

    public DashboardView()
    {
        InitializeComponent();
        LoadServices();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => LoadServices();

    private void LoadServices()
    {
        var installations = _scanner.Scan(AppPaths.BinariesRoot);

        ServiceList.Items.Clear();

        foreach (var kind in ManagedServices)
        {
            // The scanner returns newest versions first, so First wins.
            var installation = installations.FirstOrDefault(item => item.Kind == kind);

            var card = new ServiceCard
            {
                Margin = new Thickness(0, 0, 0, 12),
                Title = kind.ToDisplayName(),
                Glyph = Glyphs[kind],
                IsDetected = installation is not null,
                Version = installation?.Version ?? "—",
                Detail = installation?.ExecutablePath ?? $"Place the binaries in {AppPaths.BinariesRoot}",
                Status = ServiceStatus.Stopped,
            };

            ServiceList.Items.Add(card);
        }

        var phpCount = installations.Count(item => item.Kind == ServiceKind.Php);

        SummaryLabel.Text = phpCount == 0
            ? $"No PHP versions found in {AppPaths.BinariesRoot}"
            : $"{phpCount} PHP version(s) available · workspace at {AppPaths.WorkspaceRoot}";
    }
}