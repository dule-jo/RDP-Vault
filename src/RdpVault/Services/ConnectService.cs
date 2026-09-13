using System.Diagnostics;
using System.IO;
using RdpVault.Models;

namespace RdpVault.Services;

/// <summary>
/// Launches mstsc.exe against a generated .rdp file and cleans up the temp
/// file once the mstsc process exits.
/// </summary>
public class ConnectService
{
    private readonly RdpFileService _rdpFileService;

    public ConnectService(RdpFileService rdpFileService)
    {
        _rdpFileService = rdpFileService;
    }

    public void Connect(ServerEntry server)
    {
        var rdpFilePath = _rdpFileService.GenerateTempRdpFile(server);

        var startInfo = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            Arguments = $"\"{rdpFilePath}\"",
            UseShellExecute = true,
        };

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return;
        }

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            try
            {
                File.Delete(rdpFilePath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
    }
}
