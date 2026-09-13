using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RdpVault.Models;
using RdpVault.Services;

namespace RdpVault;

/// <summary>
/// A sidebar tree node representing either the virtual "Favorites" group or a real group.
/// </summary>
public class TreeGroup
{
    public string Name { get; init; } = string.Empty;
    public ObservableCollection<ServerNode> Servers { get; init; } = [];
}

/// <summary>
/// Pairs a server with the display name of the group it belongs to, for use in
/// both the tree leaf items and the detail panel.
/// </summary>
public class ServerNode
{
    public required ServerEntry Server { get; init; }
    public string GroupName { get; init; } = string.Empty;

    public string HostPort => $"{Server.Host}:{Server.Port}";
}

public partial class MainWindow : Window
{
    private readonly JsonStorageService _storage = new();
    private readonly CredentialService _credentialService = new();
    private readonly ConnectService _connectService;
    private AppData _appData = new();
    private ObservableCollection<TreeGroup> _groups = [];

    public MainWindow()
    {
        InitializeComponent();

        _connectService = new ConnectService(new RdpFileService(_credentialService));

        _appData = _storage.Load();
        if (_appData.Groups.Count == 0 && _appData.Servers.Count == 0)
        {
            _appData = BuildSeedData();
            _storage.Save(_appData);
        }

        RefreshTree();
    }

    private static AppData BuildSeedData()
    {
        var production = new Group { Name = "Production" };
        var development = new Group { Name = "Development" };

        return new AppData
        {
            Groups = [production, development],
            Servers =
            [
            ],
        };
    }

    private void RefreshTree()
    {
        var previouslyExpanded = _groups.Where(g => IsExpanded(g)).Select(g => g.Name).ToHashSet();

        // Deselect the current item in the OLD tree before rebuilding. Every server node below
        // is rebuilt fresh, so if ItemsSource is swapped while something is still selected, WPF
        // treats the selection as orphaned and silently re-homes it onto the first top-level
        // container instead of clearing it, leaving a stray highlight with no matching detail panel.
        if (ServerTree.SelectedItem is ServerNode currentlySelected)
        {
            var oldGroup = _groups.FirstOrDefault(g => g.Name != "Favorites" && g.Servers.Contains(currentlySelected));
            if (oldGroup is not null &&
                ServerTree.ItemContainerGenerator.ContainerFromItem(oldGroup) is TreeViewItem oldGroupItem &&
                oldGroupItem.ItemContainerGenerator.ContainerFromItem(currentlySelected) is TreeViewItem oldLeafItem)
            {
                oldLeafItem.IsSelected = false;
            }
        }

        var filter = SearchBox.Text?.Trim() ?? string.Empty;
        var isFiltering = !string.IsNullOrEmpty(filter);

        var groupNodes = _appData.Groups
            .Select(group => new TreeGroup
            {
                Name = group.Name,
                Servers = new ObservableCollection<ServerNode>(
                    _appData.Servers
                        .Where(s => s.GroupId == group.Id && MatchesFilter(s, filter))
                        .Select(s => new ServerNode { Server = s, GroupName = group.Name })),
            })
            .Where(g => !isFiltering || g.Servers.Count > 0)
            .ToList();

        var favoritesNode = new TreeGroup
        {
            Name = "Favorites",
            Servers = new ObservableCollection<ServerNode>(
                groupNodes.SelectMany(g => g.Servers).Where(n => n.Server.IsFavorite)),
        };

        _groups = isFiltering && favoritesNode.Servers.Count == 0
            ? [.. groupNodes]
            : [favoritesNode, .. groupNodes];
        ServerTree.ItemsSource = _groups;
        ServerTree.UpdateLayout();

        if (isFiltering)
        {
            // Auto-expand every visible group so search results are immediately visible.
            foreach (var group in _groups)
            {
                if (ServerTree.ItemContainerGenerator.ContainerFromItem(group) is TreeViewItem item)
                {
                    item.IsExpanded = true;
                }
            }
        }
        else
        {
            foreach (var name in previouslyExpanded)
            {
                if (ServerTree.ItemContainerGenerator.ContainerFromItem(_groups.FirstOrDefault(g => g.Name == name)) is TreeViewItem item)
                {
                    item.IsExpanded = true;
                }
            }
        }
    }

    private static bool MatchesFilter(ServerEntry server, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        return server.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || server.Host.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || server.Username.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsExpanded(TreeGroup group)
    {
        return ServerTree.ItemContainerGenerator.ContainerFromItem(group) is TreeViewItem { IsExpanded: true };
    }

    private void ServerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ServerNode node)
        {
            DetailContent.DataContext = node;
            DetailContent.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
        }
        else
        {
            DetailContent.DataContext = null;
            DetailContent.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        RefreshTree();
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (DetailContent.DataContext is ServerNode node)
        {
            _connectService.Connect(node.Server);
        }
    }

    private void AddServerButton_Click(object sender, RoutedEventArgs e)
    {
        OpenAddServerDialog();
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var menu = new ContextMenu { PlacementTarget = button };
        var app = (App)Application.Current;

        foreach (var (label, value) in new (string, string)[]
                 {
                     ("Auto (match Windows)", "Auto"),
                     ("Light", "Light"),
                     ("Dark", "Dark"),
                 })
        {
            var themeItem = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = app.ThemeChoice == value,
            };
            themeItem.Click += (_, _) => app.ApplyTheme(value);
            menu.Items.Add(themeItem);
        }
        menu.Items.Add(new Separator());

        var exportItem = new MenuItem { Header = "Export servers..." };
        exportItem.Click += (_, _) => ExportServers();
        menu.Items.Add(exportItem);

        var importItem = new MenuItem { Header = "Import servers..." };
        importItem.Click += (_, _) => ImportServers();
        menu.Items.Add(importItem);

        menu.IsOpen = true;
    }

    private void ExportServers()
    {
        var dialog = new SaveFileDialog
        {
            FileName = "rdpvault-export.json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var count = ExportServersToFile(dialog.FileName);

        MessageBox.Show(
            this,
            $"Exported {count} server(s) to:\n{dialog.FileName}\n\nSaved passwords are not included — they stay in Windows Credential Manager on this machine.",
            "Export complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private int ExportServersToFile(string path)
    {
        var json = JsonSerializer.Serialize(_appData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return _appData.Servers.Count;
    }

    private void ImportServers()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var importedCount = ImportServersFromFile(dialog.FileName);
        if (importedCount is null)
        {
            MessageBox.Show(this, "That file isn't a valid RDP Vault export.", "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            this,
            $"Imported {importedCount} server(s).\n\nAny saved passwords need to be re-entered — they aren't included in exports for security.",
            "Import complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private int? ImportServersFromFile(string path)
    {
        AppData imported;
        try
        {
            var json = File.ReadAllText(path);
            imported = JsonSerializer.Deserialize<AppData>(json) ?? new AppData();
        }
        catch (JsonException)
        {
            return null;
        }

        var groupIdMap = new Dictionary<string, string>();
        foreach (var importedGroup in imported.Groups)
        {
            var existing = _appData.Groups.FirstOrDefault(g => g.Name.Equals(importedGroup.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new Group { Name = importedGroup.Name };
                _appData.Groups.Add(existing);
            }

            groupIdMap[importedGroup.Id] = existing.Id;
        }

        var importedCount = 0;
        foreach (var importedServer in imported.Servers)
        {
            var groupId = importedServer.GroupId is not null && groupIdMap.TryGetValue(importedServer.GroupId, out var mappedId)
                ? mappedId
                : EnsureUngroupedGroupId();

            _appData.Servers.Add(new ServerEntry
            {
                Name = importedServer.Name,
                Host = importedServer.Host,
                Port = importedServer.Port,
                Username = importedServer.Username,
                Domain = importedServer.Domain,
                GroupId = groupId,
                IsFavorite = importedServer.IsFavorite,
                Notes = importedServer.Notes,
                // CredentialRef intentionally left null: a credential name from another
                // export/machine has no matching Windows Credential Manager entry here.
            });
            importedCount++;
        }

        _storage.Save(_appData);
        RefreshTree();

        return importedCount;
    }

    private string EnsureUngroupedGroupId()
    {
        var ungrouped = _appData.Groups.FirstOrDefault(g => g.Name == "Ungrouped");
        if (ungrouped is null)
        {
            ungrouped = new Group { Name = "Ungrouped" };
            _appData.Groups.Add(ungrouped);
        }

        return ungrouped.Id;
    }

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RenameGroupWindow(string.Empty, isNew: true) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var name = dialog.NewName;

        if (_appData.Groups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, $"A group named '{name}' already exists.", "Duplicate group", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _appData.Groups.Add(new Group { Name = name });
        _storage.Save(_appData);
        RefreshTree();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (DetailContent.DataContext is ServerNode node)
        {
            OpenAddServerDialog(node.Server, node.GroupName);
        }
    }

    private void OpenAddServerDialog(ServerEntry? copyFrom = null, string? copyFromGroupName = null)
    {
        var existingGroupNames = _appData.Groups.Select(g => g.Name);
        var dialog = new AddServerWindow(existingGroupNames, copyFrom, copyFromGroupName) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } server)
        {
            return;
        }

        var group = _appData.Groups.FirstOrDefault(g => g.Name == dialog.GroupName);
        if (group is null)
        {
            group = new Group { Name = dialog.GroupName };
            _appData.Groups.Add(group);
        }

        server.GroupId = group.Id;

        if (dialog.RememberPassword && !string.IsNullOrEmpty(dialog.Password))
        {
            _credentialService.SaveCredential(server.Id, server.Username, dialog.Password);
            server.CredentialRef = server.Id;
        }

        _appData.Servers.Add(server);
        _storage.Save(_appData);

        RefreshTree();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (DetailContent.DataContext is not ServerNode node)
        {
            return;
        }

        var existingGroupNames = _appData.Groups.Select(g => g.Name);
        var dialog = new AddServerWindow(existingGroupNames, node.Server, node.GroupName, isEditMode: true) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } updated)
        {
            return;
        }

        var group = _appData.Groups.FirstOrDefault(g => g.Name == dialog.GroupName);
        if (group is null)
        {
            group = new Group { Name = dialog.GroupName };
            _appData.Groups.Add(group);
        }
        updated.GroupId = group.Id;

        if (dialog.RememberPassword)
        {
            if (!string.IsNullOrEmpty(dialog.Password))
            {
                _credentialService.SaveCredential(updated.Id, updated.Username, dialog.Password);
                updated.CredentialRef = updated.Id;
            }
            else
            {
                // Checkbox left checked but no new password typed - keep the existing credential as-is.
                updated.CredentialRef = node.Server.CredentialRef;
            }
        }
        else if (node.Server.CredentialRef is not null)
        {
            _credentialService.DeleteCredential(node.Server.CredentialRef);
            updated.CredentialRef = null;
        }

        var index = _appData.Servers.FindIndex(s => s.Id == updated.Id);
        if (index >= 0)
        {
            _appData.Servers[index] = updated;
        }

        _storage.Save(_appData);

        DetailContent.DataContext = null;
        DetailContent.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;

        RefreshTree();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (DetailContent.DataContext is not ServerNode node)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Delete '{node.Server.Name}'? This cannot be undone.",
            "Delete server",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (node.Server.CredentialRef is not null)
        {
            _credentialService.DeleteCredential(node.Server.CredentialRef);
        }

        _appData.Servers.RemoveAll(s => s.Id == node.Server.Id);
        _storage.Save(_appData);

        DetailContent.DataContext = null;
        DetailContent.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;

        RefreshTree();
    }

    private void ServerTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var treeViewItem = FindAncestor<TreeViewItem>(source);
        if (treeViewItem?.DataContext is TreeGroup treeGroup && treeGroup.Name != "Favorites")
        {
            treeViewItem.ContextMenu = BuildGroupContextMenu(treeGroup);
        }
        else
        {
            e.Handled = true;
        }
    }

    private ContextMenu BuildGroupContextMenu(TreeGroup treeGroup)
    {
        var menu = new ContextMenu();

        var renameItem = new MenuItem { Header = "Rename Group..." };
        renameItem.Click += (_, _) => RenameGroup(treeGroup);
        menu.Items.Add(renameItem);

        var deleteItem = new MenuItem { Header = "Delete Group..." };
        deleteItem.Click += (_, _) => DeleteGroup(treeGroup);
        menu.Items.Add(deleteItem);

        return menu;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void RenameGroup(TreeGroup treeGroup)
    {
        var appGroup = _appData.Groups.FirstOrDefault(g => g.Name == treeGroup.Name);
        if (appGroup is null)
        {
            return;
        }

        var dialog = new RenameGroupWindow(appGroup.Name) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newName = dialog.NewName;
        if (newName == appGroup.Name)
        {
            return;
        }

        if (_appData.Groups.Any(g => g.Id != appGroup.Id && g.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, $"A group named '{newName}' already exists.", "Duplicate group", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        appGroup.Name = newName;
        _storage.Save(_appData);
        RefreshTree();
    }

    private void DeleteGroup(TreeGroup treeGroup)
    {
        var appGroup = _appData.Groups.FirstOrDefault(g => g.Name == treeGroup.Name);
        if (appGroup is null)
        {
            return;
        }

        var affectedServers = _appData.Servers.Where(s => s.GroupId == appGroup.Id).ToList();

        if (affectedServers.Count == 0)
        {
            if (MessageBox.Show(this, $"Delete empty group '{appGroup.Name}'?", "Delete group", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                return;
            }
        }
        else
        {
            var choice = MessageBox.Show(
                this,
                $"Group '{appGroup.Name}' contains {affectedServers.Count} server(s).\n\n" +
                "Yes = delete the group AND its servers\n" +
                "No = delete the group but keep its servers (moved to Ungrouped)\n" +
                "Cancel = don't delete anything",
                "Delete group",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel);

            if (choice == MessageBoxResult.Cancel)
            {
                return;
            }

            if (choice == MessageBoxResult.Yes)
            {
                foreach (var server in affectedServers.Where(s => s.CredentialRef is not null))
                {
                    _credentialService.DeleteCredential(server.CredentialRef!);
                }

                _appData.Servers.RemoveAll(s => s.GroupId == appGroup.Id);
            }
            else
            {
                var ungrouped = _appData.Groups.FirstOrDefault(g => g.Id != appGroup.Id && g.Name == "Ungrouped");
                if (ungrouped is null)
                {
                    ungrouped = new Group { Name = "Ungrouped" };
                    _appData.Groups.Add(ungrouped);
                }

                foreach (var server in affectedServers)
                {
                    server.GroupId = ungrouped.Id;
                }
            }
        }

        _appData.Groups.Remove(appGroup);
        _storage.Save(_appData);

        DetailContent.DataContext = null;
        DetailContent.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;

        RefreshTree();
    }
}
