using System.IO;
using RdpVault.Models;

namespace RdpVault.Services;

/// <summary>
/// Generates temporary .rdp files consumed by mstsc.exe. Cleanup of the
/// generated file after the mstsc process exits is the caller's responsibility
/// (see ConnectService).
/// </summary>
public class RdpFileService
{
    private readonly CredentialService _credentialService;

    public RdpFileService(CredentialService credentialService)
    {
        _credentialService = credentialService;
    }

    public string GenerateTempRdpFile(ServerEntry server)
    {
        var lines = new List<string>
        {
            $"full address:s:{server.Host}:{server.Port}",
            $"username:s:{server.Username}",
        };

        if (!string.IsNullOrWhiteSpace(server.Domain))
        {
            lines.Add($"domain:s:{server.Domain}");
        }

        if (!string.IsNullOrWhiteSpace(server.CredentialRef))
        {
            var credential = _credentialService.GetCredential(server.CredentialRef);
            if (credential is not null)
            {
                var protectedPassword = _credentialService.ProtectForRdpFile(credential.Password);
                lines.Add($"password 51:b:{protectedPassword}");
            }
        }

        lines.AddRange(
        [
            "screen mode id:i:2",
            "use multimon:i:0",
            "audiomode:i:0",
            "redirectclipboard:i:1",
            "authentication level:i:2",
            "prompt for credentials:i:0",
            "negotiate security layer:i:1",
            "autoreconnection enabled:i:1",
        ]);

        var tempPath = Path.Combine(Path.GetTempPath(), $"rdpvault-{server.Id}-{Guid.NewGuid():N}.rdp");
        File.WriteAllLines(tempPath, lines);
        return tempPath;
    }
}
