using System.Windows;

namespace RdpVault;

public partial class RenameGroupWindow : Window
{
    public string NewName { get; private set; } = string.Empty;

    public RenameGroupWindow(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "Name cannot be empty.", "Missing name", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NewName = NameBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
