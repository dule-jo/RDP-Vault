using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using RdpVault.Models;

namespace RdpVault;

/// <summary>
/// A sidebar tree node representing either the virtual "Favorites" group or a real group.
/// </summary>
public class TreeGroup
{
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = "📁";
    public List<ServerNode> Servers { get; init; } = [];
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
    public MainWindow()
    {
        InitializeComponent();
        ServerTree.ItemsSource = BuildMockTree();
    }

    private static List<TreeGroup> BuildMockTree()
    {
        var production = new Group { Name = "Production" };
        var development = new Group { Name = "Development" };

        var web01 = new ServerEntry { Name = "Web 01", Host = "10.10.1.10", Username = "dusan", Domain = "CORP", GroupId = production.Id };
        var web02 = new ServerEntry { Name = "Web 02", Host = "10.10.1.11", Username = "dusan", Domain = "CORP", GroupId = production.Id };
        var db01 = new ServerEntry { Name = "DB 01", Host = "10.10.1.50", Username = "dusan", Domain = "CORP", GroupId = production.Id, IsFavorite = true };

        var dev01 = new ServerEntry { Name = "Dev 01", Host = "10.10.2.10", Username = "dusan", GroupId = development.Id, IsFavorite = true };
        var dev02 = new ServerEntry { Name = "Dev 02", Host = "10.10.2.11", Username = "dusan", GroupId = development.Id };

        var productionNode = new TreeGroup
        {
            Name = production.Name,
            Servers =
            [
                new ServerNode { Server = web01, GroupName = production.Name },
                new ServerNode { Server = web02, GroupName = production.Name },
                new ServerNode { Server = db01, GroupName = production.Name },
            ],
        };

        var developmentNode = new TreeGroup
        {
            Name = development.Name,
            Servers =
            [
                new ServerNode { Server = dev01, GroupName = development.Name },
                new ServerNode { Server = dev02, GroupName = development.Name },
            ],
        };

        List<ServerNode> allServers = [.. productionNode.Servers, .. developmentNode.Servers];
        var favoritesNode = new TreeGroup
        {
            Name = "Favorites",
            Icon = "⭐",
            Servers = allServers.FindAll(s => s.Server.IsFavorite),
        };

        return [favoritesNode, productionNode, developmentNode];
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
}
