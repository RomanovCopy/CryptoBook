using System.IO;
using System.Text.Json;

namespace CryptoBook.AI;

public sealed class AiSettingsStore
{
    private readonly string filePath;
    public AiConfiguration Current { get; private set; }
    public event EventHandler? Changed;

    public AiSettingsStore() : this(Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "CryptoBook", "ai-settings.json")) { }

    public AiSettingsStore(string filePath)
    {
        this.filePath = filePath;
        try
        {
            Current = File.Exists(filePath)
                ? JsonSerializer.Deserialize<AiConfiguration>(File.ReadAllText(filePath)) ?? new()
                : new();
            if(Current.Models is null || Current.IdleMinutes is < 0 or > 120)
                Current = new();
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException)
        {
            Current = new();
        }
    }

    public void Save(AiConfiguration configuration)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(configuration,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, filePath, overwrite: true);
        }
        finally
        {
            if(File.Exists(temporary)) File.Delete(temporary);
        }
        Current = configuration;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
