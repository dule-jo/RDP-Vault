using System.Collections.Generic;
using System.Windows;
using RdpVault.Models;

namespace RdpVault;

public partial class AddServerWindow : Window
{
    private readonly bool _isEditMode;
    private readonly string? _editingId;
    private readonly string? _editingCredentialRef;

    public ServerEntry? Result { get; private set; }
    public string GroupName { get; private set; } = string.Empty;
    public string Password => PasswordBox.Password;
    public bool RememberPassword => RememberPasswordCheck.IsChecked == true;
    public bool HasExistingCredential => !string.IsNullOrEmpty(_editingCredentialRef);

    public AddServerWindow(IEnumerable<string> existingGroupNames, ServerEntry? prefillFrom = null, string? prefillGroupName = null, bool isEditMode = false)
    {
        InitializeComponent();
        foreach (var name in existingGroupNames)
        {
            GroupBox.Items.Add(name);
        }

        _isEditMode = isEditMode;

        if (prefillFrom is not null)
        {
            HeaderText.Text = isEditMode ? "Edit Server" : "Copy Server";
            Title = HeaderText.Text;
            NameBox.Text = isEditMode ? prefillFrom.Name : $"{prefillFrom.Name} (Copy)";
            HostBox.Text = prefillFrom.Host;
            PortBox.Text = prefillFrom.Port.ToString();
            UsernameBox.Text = prefillFrom.Username;
            DomainBox.Text = prefillFrom.Domain ?? string.Empty;
            GroupBox.Text = prefillGroupName ?? string.Empty;
            FavoriteCheck.IsChecked = prefillFrom.IsFavorite;

            if (isEditMode)
            {
                _editingId = prefillFrom.Id;
                _editingCredentialRef = prefillFrom.CredentialRef;

                if (_editingCredentialRef is not null)
                {
                    RememberPasswordCheck.IsChecked = true;
                    PasswordHint.Visibility = Visibility.Visible;
                }
            }
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

        if (_isEditMode)
        {
            Result.Id = _editingId!;
            Result.CredentialRef = _editingCredentialRef;
        }

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
