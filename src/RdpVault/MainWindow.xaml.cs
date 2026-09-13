using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RdpVault.Models;
using RdpVault.Services;

namespace RdpVault;

/// <summary>
/// A sidebar tree node representing either the virtual "Favorites" group or a real group.
/// </summary>
public class TreeGroup
{
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = "📁";
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
    private AppData _appData = new();
    private ObservableCollection<TreeGroup> _groups = [];

    public MainWindow()
    {
        InitializeComponent();

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
                new ServerEntry { Name = "Web 01", Host = "10.10.1.10", Username = "dusan", Domain = "CORP", GroupId = production.Id },
                new ServerEntry { Name = "Web 02", Host = "10.10.1.11", Username = "dusan", Domain = "CORP", GroupId = production.Id },
                new ServerEntry { Name = "DB 01", Host = "10.10.1.50", Username = "dusan", Domain = "CORP", GroupId = production.Id, IsFavorite = true },
                new ServerEntry { Name = "Dev 01", Host = "10.10.2.10", Username = "dusan", GroupId = development.Id, IsFavorite = true },
                new ServerEntry { Name = "Dev 02", Host = "10.10.2.11", Username = "dusan", GroupId = development.Id },
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

        var groupNodes = _appData.Groups.Select(group => new TreeGroup
        {
            Name = group.Name,
            Servers = new ObservableCollection<ServerNode>(
                _appData.Servers
                    .Where(s => s.GroupId == group.Id)
                    .Select(s => new ServerNode { Server = s, GroupName = group.Name })),
        }).ToList();

        var favoritesNode = new TreeGroup
        {
            Name = "Favorites",
            Icon = "⭐",
            Servers = new ObservableCollection<ServerNode>(
                groupNodes.SelectMany(g => g.Servers).Where(n => n.Server.IsFavorite)),
        };

        _groups = [favoritesNode, .. groupNodes];
        ServerTree.ItemsSource = _groups;
        ServerTree.UpdateLayout();

        foreach (var name in previouslyExpanded)
        {
            if (ServerTree.ItemContainerGenerator.ContainerFromItem(_groups.FirstOrDefault(g => g.Name == name)) is TreeViewItem item)
            {
                item.IsExpanded = true;
            }
        }
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
    }

    private void AddServerButton_Click(object sender, RoutedEventArgs e)
    {
        OpenAddServerDialog();
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
