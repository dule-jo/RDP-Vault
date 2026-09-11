using System.Collections.Generic;
using System.Windows;
using RdpVault.Models;

namespace RdpVault;

public partial class AddServerWindow : Window
{
    public ServerEntry? Result { get; private set; }
    public string GroupName { get; private set; } = string.Empty;

    public AddServerWindow(IEnumerable<string> existingGroupNames)
    {
        InitializeComponent();
        foreach (var name in existingGroupNames)
        {
            GroupBox.Items.Add(name);
        }
    }

    private void RememberPasswordCheck_CheckedChanged(object sender, RoutedEventArgs e)
    {
        PasswordBox.IsEnabled = RememberPasswordCheck.IsChecked == true;
        if (!PasswordBox.IsEnabled)
        {
            PasswordBox.Clear();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(HostBox.Text))
        {
            MessageBox.Show(this, "Name and Host are required.", "Missing information", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var port = int.TryParse(PortBox.Text, out var parsedPort) ? parsedPort : 3389;
        var groupName = string.IsNullOrWhiteSpace(GroupBox.Text) ? "Ungrouped" : GroupBox.Text.Trim();

        Result = new ServerEntry
        {
            Name = NameBox.Text.Trim(),
            Host = HostBox.Text.Trim(),
            Port = port,
            Username = UsernameBox.Text.Trim(),
            Domain = string.IsNullOrWhiteSpace(DomainBox.Text) ? null : DomainBox.Text.Trim(),
            IsFavorite = FavoriteCheck.IsChecked == true,
        };
        GroupName = groupName;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
