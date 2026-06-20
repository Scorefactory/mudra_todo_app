using System.IO;
using System.Text.Json;
using TodoWpfPortable.Models;

namespace TodoWpfPortable.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettingsService()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TodoWpfPortable");

        DataDirectory = root;
        SettingsFilePath = Path.Combine(root, "settings.json");
    }

    public string DataDirectory { get; }

    public string SettingsFilePath { get; }

    public AppSettings Load()
    {
        Directory.CreateDirectory(DataDirectory);

        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataDirectory);
        var tempPath = SettingsFilePath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsFilePath, true);
    }
}