namespace RdpVault.Models;

public class AppData
{
    public int Version { get; set; } = 1;
    public List<Group> Groups { get; set; } = [];
    public List<ServerEntry> Servers { get; set; } = [];
}
