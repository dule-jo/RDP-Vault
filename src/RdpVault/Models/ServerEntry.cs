namespace RdpVault.Models;

public class ServerEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3389;
    public string Username { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? GroupId { get; set; }
    public bool IsFavorite { get; set; }
    public string? CredentialRef { get; set; }
    public string? Notes { get; set; }
}
