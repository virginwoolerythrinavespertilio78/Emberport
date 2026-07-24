using System.Windows;
using System.Windows.Controls;

namespace Emberport.Controls;

public partial class Sidebar : UserControl
{
    public Sidebar()
    {
        InitializeComponent();
    }

    /// <summary>Raised with the page key of the newly selected navigation item.</summary>
    public event EventHandler<string>? PageSelected;

    private void OnNavItemChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Uid.Length: > 0 } item)
        {
            PageSelected?.Invoke(this, item.Uid);
        }
    }
}