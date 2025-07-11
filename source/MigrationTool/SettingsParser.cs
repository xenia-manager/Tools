using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigrationTool;

public class NewManagerConfig
{
    public bool UnifiedContentFolder { get; set; } = false;
    public EmulatorInfo XeniaCanary { get; set; } = new EmulatorInfo();
    public EmulatorInfo XeniaMousehook { get; set; } = new EmulatorInfo();
    public EmulatorInfo XeniaNetplay { get; set; } = new EmulatorInfo();
}

public class OldManagerConfig
{
    public EmulatorInfo XeniaCanary { get; set; } = new EmulatorInfo();
    public EmulatorInfo XeniaMousehook { get; set; } = new EmulatorInfo();
    public EmulatorInfo XeniaNetplay { get; set; } = new EmulatorInfo();
}

public class EmulatorInfo
{
    [JsonPropertyName("emulator_location")]
    public string EmulatorLocation { get; set; } = string.Empty;
}

public class EmulatorStatus
{
    public bool XeniaCanaryConfigured { get; set; }
    public bool XeniaMousehookConfigured { get; set; }
    public bool XeniaNetplayConfigured { get; set; }

    public bool AnyEmulatorConfigured => XeniaCanaryConfigured || XeniaMousehookConfigured || XeniaNetplayConfigured;
    public int ConfiguredEmulatorCount => (XeniaCanaryConfigured ? 1 : 0) +
                                         (XeniaMousehookConfigured ? 1 : 0) +
                                         (XeniaNetplayConfigured ? 1 : 0);
}

public static class SettingsParser
{
    public static OldManagerConfig ParseOldConfig(string json, string baseDirectory)
    {
        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        JsonElement root = jsonDocument.RootElement;

        var config = new OldManagerConfig();

        // Parse Xenia Canary Info
        config.XeniaCanary = ParseEmulatorSection(baseDirectory, root, "xenia_canary_info");

        // Parse Xenia Mousehook Info
        config.XeniaMousehook = ParseEmulatorSection(baseDirectory, root, "xenia_mousehook_info");

        // Parse Xenia Netplay Info
        config.XeniaNetplay = ParseEmulatorSection(baseDirectory, root, "xenia_netplay_info");

        return config;
    }

    public static NewManagerConfig ParseNewConfig(string json, string baseDirectory)
    {
        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        JsonElement root = jsonDocument.RootElement;

        var config = new NewManagerConfig();

        // Parse unified content folder from emulators.settings.unified_content
        if (root.TryGetProperty("emulators", out JsonElement emulatorsSection) &&
            emulatorsSection.TryGetProperty("settings", out JsonElement settingsSection) &&
            settingsSection.TryGetProperty("unified_content", out JsonElement unifiedContent))
        {
            config.UnifiedContentFolder = unifiedContent.GetBoolean();
        }

        // Parse emulator info from emulators section
        if (root.TryGetProperty("emulators", out JsonElement emulators))
        {
            config.XeniaCanary = ParseEmulatorSection(baseDirectory, emulators, "canary");
            config.XeniaMousehook = ParseEmulatorSection(baseDirectory, emulators, "mousehook");
            config.XeniaNetplay = ParseEmulatorSection(baseDirectory, emulators, "netplay");
        }

        return config;
    }

    private static EmulatorInfo ParseEmulatorSection(string baseDirectory, JsonElement root, string sectionName)
    {
        var emulatorInfo = new EmulatorInfo();

        if (root.TryGetProperty(sectionName, out JsonElement emulatorSection) &&
            emulatorSection.ValueKind != JsonValueKind.Null)
        {
            if (emulatorSection.TryGetProperty("emulator_location", out JsonElement emulatorLocation))
            {
                string location = emulatorLocation.GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(location))
                {
                    emulatorInfo.EmulatorLocation = Path.Combine(baseDirectory, location, "content");
                }
                else
                {
                    emulatorInfo.EmulatorLocation = string.Empty;
                }
                
            }
        }

        return emulatorInfo;
    }

    // Helper method to check if an emulator section exists and is not null
    public static bool IsEmulatorConfigured(JsonElement root, string sectionName)
    {
        return root.TryGetProperty(sectionName, out JsonElement section) &&
               section.ValueKind != JsonValueKind.Null &&
               section.TryGetProperty("emulator_location", out JsonElement location) &&
               !string.IsNullOrEmpty(location.GetString());
    }

    // Method to get emulator status summary
    public static EmulatorStatus GetEmulatorStatus(string json, bool isNewFormat = false)
    {
        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        JsonElement root = jsonDocument.RootElement;

        if (isNewFormat)
        {
            // For new format, look under emulators section
            if (root.TryGetProperty("emulators", out JsonElement emulators))
            {
                return new EmulatorStatus
                {
                    XeniaCanaryConfigured = IsEmulatorConfigured(emulators, "canary"),
                    XeniaMousehookConfigured = IsEmulatorConfigured(emulators, "mousehook"),
                    XeniaNetplayConfigured = IsEmulatorConfigured(emulators, "netplay")
                };
            }
            else
            {
                return new EmulatorStatus();
            }
        }
        else
        {
            return new EmulatorStatus
            {
                XeniaCanaryConfigured = IsEmulatorConfigured(root, "xenia_canary_info"),
                XeniaMousehookConfigured = IsEmulatorConfigured(root, "xenia_mousehook_info"),
                XeniaNetplayConfigured = IsEmulatorConfigured(root, "xenia_netplay_info")
            };
        }
    }

    // Method to migrate from old config to new config
    public static NewManagerConfig MigrateToNewConfig(OldManagerConfig oldConfig)
    {
        return new NewManagerConfig
        {
            UnifiedContentFolder = false, // Default value for new config
            XeniaCanary = oldConfig.XeniaCanary,
            XeniaMousehook = oldConfig.XeniaMousehook,
            XeniaNetplay = oldConfig.XeniaNetplay
        };
    }

    // Method to serialize config back to JSON
    public static string SerializeConfig<T>(T config) where T : class
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(config, options);
    }
}