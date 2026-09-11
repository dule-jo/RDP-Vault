using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        var existingGroupNames = _appData.Groups.Select(g => g.Name);
        var dialog = new AddServerWindow(existingGroupNames) { Owner = this };
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
}
