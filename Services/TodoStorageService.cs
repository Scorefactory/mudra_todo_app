using System.IO;
using System.Text.Json;
using TodoWpfPortable.Models;

namespace TodoWpfPortable.Services;

public sealed class TodoStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public TodoStorageService()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TodoWpfPortable");

        DataDirectory = root;
        BackupDirectory = Path.Combine(root, "Backups");
        DataFilePath = Path.Combine(root, "tasks.json");
    }

    public string DataDirectory { get; }

    public string BackupDirectory { get; }

    public string DataFilePath { get; }

    public async Task<IReadOnlyList<TodoItem>> LoadAsync()
    {
        Directory.CreateDirectory(DataDirectory);

        if (!File.Exists(DataFilePath))
        {
            return Array.Empty<TodoItem>();
        }

        await using var stream = File.OpenRead(DataFilePath);
        var document = await JsonSerializer.DeserializeAsync<TodoDocument>(stream, JsonOptions);
        return document?.Items is null ? Array.Empty<TodoItem>() : document.Items;
    }

    public async Task SaveAsync(IEnumerable<TodoItem> todos)
    {
        Directory.CreateDirectory(DataDirectory);

        var document = new TodoDocument
        {
            LastSavedAt = DateTimeOffset.Now,
            Items = todos.ToList()
        };

        var tempPath = DataFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions);
        }

        File.Move(tempPath, DataFilePath, true);
    }

    public void Save(IEnumerable<TodoItem> todos)
    {
        Directory.CreateDirectory(DataDirectory);

        var document = new TodoDocument
        {
            LastSavedAt = DateTimeOffset.Now,
            Items = todos.ToList()
        };

        var tempPath = DataFilePath + ".tmp";
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, DataFilePath, true);
    }

    public async Task<string> CreateBackupAsync()
    {
        Directory.CreateDirectory(BackupDirectory);

        if (!File.Exists(DataFilePath))
        {
            await SaveAsync(Array.Empty<TodoItem>());
        }

        var backupPath = Path.Combine(
            BackupDirectory,
            $"todo-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        File.Copy(DataFilePath, backupPath, true);
        return backupPath;
    }

    public async Task<IReadOnlyList<TodoItem>> RestoreAsync(string backupPath)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("Backup file was not found.", backupPath);
        }

        await using (var stream = File.OpenRead(backupPath))
        {
            var document = await JsonSerializer.DeserializeAsync<TodoDocument>(stream, JsonOptions);
            if (document?.Items is null)
            {
                throw new InvalidDataException("The selected file is not a valid TodoWpfPortable backup.");
            }
        }

        Directory.CreateDirectory(DataDirectory);
        File.Copy(backupPath, DataFilePath, true);
        return await LoadAsync();
    }

    private sealed class TodoDocument
    {
        public int Version { get; set; } = 1;

        public DateTimeOffset LastSavedAt { get; set; } = DateTimeOffset.Now;

        public List<TodoItem> Items { get; set; } = [];
    }
}
