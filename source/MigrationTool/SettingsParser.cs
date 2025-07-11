using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigrationTool
{
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

        public bool AnyEmulatorConfigured =>
            XeniaCanaryConfigured || XeniaMousehookConfigured || XeniaNetplayConfigured;

        public int ConfiguredEmulatorCount =>
            (XeniaCanaryConfigured ? 1 : 0) +
            (XeniaMousehookConfigured ? 1 : 0) +
            (XeniaNetplayConfigured ? 1 : 0);
    }

    public static class SettingsParser
    {
        private const string CONTENT_FOLDER = "content";

        // Old format section names
        private const string OLD_CANARY_SECTION = "xenia_canary_info";
        private const string OLD_MOUSEHOOK_SECTION = "xenia_mousehook_info";
        private const string OLD_NETPLAY_SECTION = "xenia_netplay_info";

        // New format section names
        private const string NEW_CANARY_SECTION = "canary";
        private const string NEW_MOUSEHOOK_SECTION = "mousehook";
        private const string NEW_NETPLAY_SECTION = "netplay";

        private const string EMULATORS_SECTION = "emulators";
        private const string SETTINGS_SECTION = "settings";
        private const string UNIFIED_CONTENT_PROPERTY = "unified_content";
        private const string EMULATOR_LOCATION_PROPERTY = "emulator_location";

        public static OldManagerConfig ParseOldConfig(string json, string baseDirectory)
        {
            using var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            return new OldManagerConfig
            {
                XeniaCanary = ParseEmulatorSection(baseDirectory, root, OLD_CANARY_SECTION),
                XeniaMousehook = ParseEmulatorSection(baseDirectory, root, OLD_MOUSEHOOK_SECTION),
                XeniaNetplay = ParseEmulatorSection(baseDirectory, root, OLD_NETPLAY_SECTION)
            };
        }

        public static NewManagerConfig ParseNewConfig(string json, string baseDirectory)
        {
            using var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            var config = new NewManagerConfig();

            // Parse unified content folder setting
            if (TryGetNestedProperty(root, out JsonElement unifiedContent, EMULATORS_SECTION, SETTINGS_SECTION, UNIFIED_CONTENT_PROPERTY))
            {
                config.UnifiedContentFolder = unifiedContent.GetBoolean();
            }

            // Parse emulator configurations
            if (root.TryGetProperty(EMULATORS_SECTION, out JsonElement emulators))
            {
                config.XeniaCanary = ParseEmulatorSection(baseDirectory, emulators, NEW_CANARY_SECTION);
                config.XeniaMousehook = ParseEmulatorSection(baseDirectory, emulators, NEW_MOUSEHOOK_SECTION);
                config.XeniaNetplay = ParseEmulatorSection(baseDirectory, emulators, NEW_NETPLAY_SECTION);
            }

            return config;
        }

        private static bool TryGetNestedProperty(JsonElement root, out JsonElement result, params string[] propertyPath)
        {
            result = default;
            JsonElement current = root;

            foreach (string property in propertyPath)
            {
                if (!current.TryGetProperty(property, out current))
                {
                    return false;
                }
            }

            result = current;
            return true;
        }

        private static EmulatorInfo ParseEmulatorSection(string baseDirectory, JsonElement root, string sectionName)
        {
            var emulatorInfo = new EmulatorInfo();

            if (root.TryGetProperty(sectionName, out JsonElement emulatorSection) && emulatorSection.ValueKind != JsonValueKind.Null && emulatorSection.TryGetProperty(EMULATOR_LOCATION_PROPERTY, out JsonElement emulatorLocation))
            {
                string location = emulatorLocation.GetString() ?? string.Empty;
                if (!string.IsNullOrEmpty(location))
                {
                    emulatorInfo.EmulatorLocation = Path.Combine(baseDirectory, location, CONTENT_FOLDER);
                }
            }

            return emulatorInfo;
        }

        public static bool IsEmulatorConfigured(JsonElement root, string sectionName)
        {
            return root.TryGetProperty(sectionName, out JsonElement section) &&
                   section.ValueKind != JsonValueKind.Null &&
                   section.TryGetProperty(EMULATOR_LOCATION_PROPERTY, out JsonElement location) &&
                   !string.IsNullOrEmpty(location.GetString());
        }

        public static EmulatorStatus GetEmulatorStatus(string json, bool isNewFormat = false)
        {
            using var jsonDocument = JsonDocument.Parse(json);
            var root = jsonDocument.RootElement;

            if (isNewFormat)
            {
                return GetNewFormatStatus(root);
            }
            else
            {
                return GetOldFormatStatus(root);
            }
        }

        private static EmulatorStatus GetNewFormatStatus(JsonElement root)
        {
            if (root.TryGetProperty(EMULATORS_SECTION, out JsonElement emulators))
            {
                return new EmulatorStatus
                {
                    XeniaCanaryConfigured = IsEmulatorConfigured(emulators, NEW_CANARY_SECTION),
                    XeniaMousehookConfigured = IsEmulatorConfigured(emulators, NEW_MOUSEHOOK_SECTION),
                    XeniaNetplayConfigured = IsEmulatorConfigured(emulators, NEW_NETPLAY_SECTION)
                };
            }

            return new EmulatorStatus();
        }

        private static EmulatorStatus GetOldFormatStatus(JsonElement root)
        {
            return new EmulatorStatus
            {
                XeniaCanaryConfigured = IsEmulatorConfigured(root, OLD_CANARY_SECTION),
                XeniaMousehookConfigured = IsEmulatorConfigured(root, OLD_MOUSEHOOK_SECTION),
                XeniaNetplayConfigured = IsEmulatorConfigured(root, OLD_NETPLAY_SECTION)
            };
        }

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
}