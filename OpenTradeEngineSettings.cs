using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTradeEngine;

public sealed record OpenTradeEngineSettings(
    string? InstallationPath,
    bool? EnableMods = null,
    bool? EnableGameplayLogging = null,
    int? GameplayLogLimitMb = null)
{
    [JsonIgnore]
    public bool ModsEnabled => EnableMods != false;

    [JsonIgnore]
    public bool GameplayLoggingEnabled => EnableGameplayLogging == true;

    [JsonIgnore]
    public int EffectiveGameplayLogLimitMb => Math.Clamp(GameplayLogLimitMb ?? 100, 25, 250);

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenTradeEngine");

    private static readonly string SettingsPath = Path.Combine(
        SettingsDirectory,
        "settings.json");

    public static OpenTradeEngineSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new OpenTradeEngineSettings((string?)null);
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<OpenTradeEngineSettings>(json)
                   ?? new OpenTradeEngineSettings((string?)null);
        }
        catch
        {
            // A damaged settings file should never prevent the application starting.
            return new OpenTradeEngineSettings((string?)null);
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
