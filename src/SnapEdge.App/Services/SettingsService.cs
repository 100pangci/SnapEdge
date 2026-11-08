using System.Text.Json;

namespace SnapEdge.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ConfigPath = Path.Combine(appData, "SnapEdge", "settings.json");
    }

    public string ConfigPath { get; }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new AppSettings();

            await using var stream = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            if (settings is null)
                return new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        var payload = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        await _saveLock.WaitAsync(cancellationToken);
        var temporaryPath = ConfigPath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(temporaryPath, payload, cancellationToken);
            File.Move(temporaryPath, ConfigPath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            _saveLock.Release();
        }
    }
}
