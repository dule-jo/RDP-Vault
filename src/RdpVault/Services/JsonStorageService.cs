using System.IO;
using System.Text.Json;
using RdpVault.Models;

namespace RdpVault.Services;

public class JsonStorageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonStorageService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RdpVault",
            "servers.json");
    }

    public AppData Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppData();
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<AppData>(json, SerializerOptions) ?? new AppData();
    }

    public void Save(AppData data)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(data, SerializerOptions);
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
