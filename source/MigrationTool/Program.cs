using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MigrationTool
{
    public class MigrationOption
    {
        public int Number { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; }
        public EmulatorInfo EmulatorInfo { get; set; }

        public MigrationOption(int number, string displayName, string type, EmulatorInfo emulatorInfo)
        {
            Number = number;
            DisplayName = displayName;
            Type = type;
            EmulatorInfo = emulatorInfo;
        }
    }

    class Program
    {
        private const string MIGRATION_ALL_TYPE = "all";
        private const string EXIT_OPTION_TEXT = "Exit";
        private const string EMULATORS_FOLDER = "Emulators";
        private const string CONTENT_FOLDER = "content";

        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("=== Xenia Manager Migration Tool ===\n");

            try
            {
                var pathInfo = GetManagerPaths();
                var configData = LoadConfigurations(pathInfo);

                DisplayConfigurationStatus(configData.OldStatus, configData.NewStatus);
                ShowMigrationMenu(configData, pathInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static (string OldPath, string OldConfigPath, string NewPath, string NewConfigPath) GetManagerPaths()
        {
            var (oldPath, oldConfigPath) = GetXeniaManagerPath("old");
            var (newPath, newConfigPath) = GetXeniaManagerPath("new");
            return (oldPath, oldConfigPath, newPath, newConfigPath);
        }

        private static (EmulatorStatus OldStatus, EmulatorStatus NewStatus, OldManagerConfig OldConfig, NewManagerConfig NewConfig) LoadConfigurations((string OldPath, string OldConfigPath, string NewPath, string NewConfigPath) pathInfo)
        {
            Console.WriteLine("Loading configurations...");

            var oldConfigJson = File.ReadAllText(pathInfo.OldConfigPath);
            var newConfigJson = File.ReadAllText(pathInfo.NewConfigPath);

            var oldStatus = SettingsParser.GetEmulatorStatus(oldConfigJson, false);
            var oldConfig = SettingsParser.ParseOldConfig(oldConfigJson, pathInfo.OldPath);
            var newStatus = SettingsParser.GetEmulatorStatus(newConfigJson, true);
            var newConfig = SettingsParser.ParseNewConfig(newConfigJson, pathInfo.NewPath);

            return (oldStatus, newStatus, oldConfig, newConfig);
        }

        private static (string path, string configPath) GetXeniaManagerPath(string type)
        {
            Console.WriteLine($"Please select the {type} Xenia Manager executable...");

            var openFileDialog = new OpenFileDialog
            {
                Title = $"Select {type} Xenia Manager executable",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
            };

            while (true)
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    Console.WriteLine("No file selected. Exiting...");
                    Environment.Exit(0);
                }

                var selectedPath = Path.GetDirectoryName(openFileDialog.FileName);
                if (!Directory.Exists(selectedPath))
                {
                    Console.WriteLine($"Invalid {type} Xenia Manager Path");
                    continue;
                }

                var configPath = Path.Combine(selectedPath, "Config", "config.json");
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Config file not found in {type} Xenia Manager Path");
                    continue;
                }

                Console.WriteLine($"{type} Xenia Manager Path: {selectedPath}");
                Console.WriteLine($"{type} Config found: {configPath}");
                return (selectedPath, configPath);
            }
        }

        private static void DisplayConfigurationStatus(EmulatorStatus oldStatus, EmulatorStatus newStatus)
        {
            Console.WriteLine("\n=== Configuration Status ===");

            DisplayEmulatorStatus("OLD CONFIGURATION", oldStatus);
            DisplayEmulatorStatus("NEW CONFIGURATION", newStatus);
        }

        private static void DisplayEmulatorStatus(string title, EmulatorStatus status)
        {
            Console.WriteLine($"{title}:");
            Console.WriteLine($"  Xenia Canary: {GetStatusIcon(status.XeniaCanaryConfigured)}");
            Console.WriteLine($"  Xenia Mousehook: {GetStatusIcon(status.XeniaMousehookConfigured)}");
            Console.WriteLine($"  Xenia Netplay: {GetStatusIcon(status.XeniaNetplayConfigured)}");
            Console.WriteLine($"  Total configured: {status.ConfiguredEmulatorCount}");
            Console.WriteLine();
        }

        private static string GetStatusIcon(bool configured) => configured ? "✓ Configured" : "✗ Not configured";

        private static void ShowMigrationMenu((EmulatorStatus OldStatus, EmulatorStatus NewStatus, OldManagerConfig OldConfig, NewManagerConfig NewConfig) configData, (string OldPath, string OldConfigPath, string NewPath, string NewConfigPath) pathInfo)
        {
            if (!HasAnyEmulatorToMigrate(configData.OldConfig))
            {
                Console.WriteLine("\n⚠️  No emulators found in old configuration to migrate.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Check if there are any emulators that can be migrated (exist in old but not in new)
            var availableOptions = GetAvailableMigrationOptions(configData.OldConfig, configData.NewConfig);
            if (availableOptions.Count == 0)
            {
                Console.WriteLine("\n⚠️  All emulators from old configuration are already configured in the new configuration.");
                Console.WriteLine("No migration needed.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            while (true)
            {
                availableOptions = GetAvailableMigrationOptions(configData.OldConfig, configData.NewConfig);
                DisplayMigrationOptions(availableOptions);

                var choice = GetUserChoice(availableOptions.Count + 1);
                if (choice == availableOptions.Count + 1) // Exit option
                {
                    Console.WriteLine("Exiting...");
                    break;
                }

                var selectedOption = availableOptions.FirstOrDefault(o => o.Number == choice);
                if (selectedOption == null)
                {
                    Console.WriteLine("Invalid choice. Please try again.");
                    continue;
                }

                ProcessMigrationChoice(selectedOption, availableOptions, pathInfo.OldPath, pathInfo.NewPath);
            }
        }

        private static List<MigrationOption> GetAvailableMigrationOptions(OldManagerConfig oldConfig, NewManagerConfig newConfig)
        {
            var options = new List<MigrationOption>();
            int optionNumber = 1;

            // Only add migration option if emulator exists in old config AND is not already configured in new config
            if (HasValidEmulatorPath(oldConfig.XeniaCanary) && HasValidEmulatorPath(newConfig.XeniaCanary))
            {
                options.Add(new MigrationOption(optionNumber++, "Xenia Canary", "canary", oldConfig.XeniaCanary));
            }

            if (HasValidEmulatorPath(oldConfig.XeniaMousehook) && HasValidEmulatorPath(newConfig.XeniaMousehook))
            {
                options.Add(new MigrationOption(optionNumber++, "Xenia Mousehook", "mousehook", oldConfig.XeniaMousehook));
            }

            if (HasValidEmulatorPath(oldConfig.XeniaNetplay) && HasValidEmulatorPath(newConfig.XeniaNetplay))
            {
                options.Add(new MigrationOption(optionNumber++, "Xenia Netplay", "netplay", oldConfig.XeniaNetplay));
            }

            if (options.Count > 1)
            {
                options.Add(new MigrationOption(optionNumber, "Migrate All", MIGRATION_ALL_TYPE, null));
            }

            return options;
        }

        private static void DisplayMigrationOptions(List<MigrationOption> options)
        {
            Console.WriteLine("\n=== Migration Options ===");
            Console.WriteLine("Select which Xenia version(s) to migrate:");
            Console.WriteLine();

            foreach (var option in options)
            {
                Console.WriteLine($"{option.Number}. {option.DisplayName}");
                if (option.EmulatorInfo != null)
                {
                    Console.WriteLine($"   From: {option.EmulatorInfo.EmulatorLocation}");
                }
            }

            Console.WriteLine($"{options.Count + 1}. {EXIT_OPTION_TEXT}");
            Console.WriteLine();
        }

        private static int GetUserChoice(int maxChoice)
        {
            while (true)
            {
                Console.Write("Enter your choice: ");
                if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= maxChoice)
                {
                    return choice;
                }
                Console.WriteLine("Invalid input. Please enter a valid number.");
            }
        }

        private static void ProcessMigrationChoice(MigrationOption selectedOption, List<MigrationOption> availableOptions, string oldPath, string newPath)
        {
            if (selectedOption.Type == MIGRATION_ALL_TYPE)
            {
                var emulatorsToMigrate = availableOptions.Where(o => o.Type != MIGRATION_ALL_TYPE).ToList();
                MigrateAllEmulators(emulatorsToMigrate, oldPath, newPath);
            }
            else
            {
                MigrateSingleEmulator(selectedOption, oldPath, newPath);
            }
        }

        private static bool HasValidEmulatorPath(EmulatorInfo emulatorInfo) =>
            !string.IsNullOrEmpty(emulatorInfo.EmulatorLocation) && (Directory.Exists(emulatorInfo.EmulatorLocation) || File.Exists(emulatorInfo.EmulatorLocation));

        private static bool HasAnyEmulatorToMigrate(OldManagerConfig oldConfig) =>
            HasValidEmulatorPath(oldConfig.XeniaCanary) || HasValidEmulatorPath(oldConfig.XeniaMousehook) || HasValidEmulatorPath(oldConfig.XeniaNetplay);

        private static void MigrateSingleEmulator(MigrationOption option, string oldBasePath, string newBasePath)
        {
            Console.WriteLine($"\n=== Migrating {option.DisplayName} ===");

            try
            {
                var sourcePath = GetEmulatorDirectory(option.EmulatorInfo.EmulatorLocation);
                var targetPath = Path.Combine(newBasePath, EMULATORS_FOLDER, option.DisplayName, CONTENT_FOLDER);

                if (ConfirmMigration(option.DisplayName, sourcePath, targetPath))
                {
                    CopyDirectory(sourcePath, targetPath);
                    Console.WriteLine($"✓ {option.DisplayName} migrated successfully!");
                }
                else
                {
                    Console.WriteLine($"Migration of {option.DisplayName} cancelled.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to migrate {option.DisplayName}: {ex.Message}");
            }
        }

        private static void MigrateAllEmulators(List<MigrationOption> options, string oldBasePath, string newBasePath)
        {
            Console.WriteLine($"\n=== Migrating All Emulators ===");
            Console.WriteLine($"This will migrate {options.Count} emulator(s):");

            foreach (var option in options)
            {
                Console.WriteLine($"  • {option.DisplayName}");
            }

            if (!GetUserConfirmation("Proceed with migration"))
            {
                Console.WriteLine("Migration cancelled.");
                return;
            }

            var (successful, failed) = PerformBatchMigration(options, oldBasePath, newBasePath);
            DisplayMigrationSummary(successful, failed);
        }

        private static (int successful, int failed) PerformBatchMigration(List<MigrationOption> options, string oldBasePath, string newBasePath)
        {
            int successful = 0, failed = 0;

            foreach (var option in options)
            {
                try
                {
                    Console.WriteLine($"\nMigrating {option.DisplayName}...");
                    var sourcePath = GetEmulatorDirectory(option.EmulatorInfo.EmulatorLocation);
                    var targetPath = Path.Combine(newBasePath, EMULATORS_FOLDER, option.DisplayName, CONTENT_FOLDER);

                    CopyDirectory(sourcePath, targetPath);
                    Console.WriteLine($"✓ {option.DisplayName} completed");
                    successful++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ {option.DisplayName} failed: {ex.Message}");
                    failed++;
                }
            }

            return (successful, failed);
        }

        private static void DisplayMigrationSummary(int successful, int failed)
        {
            Console.WriteLine($"\n=== Migration Summary ===");
            Console.WriteLine($"Successful: {successful}");
            Console.WriteLine($"Failed: {failed}");
            Console.WriteLine($"Total: {successful + failed}");
        }

        private static bool GetUserConfirmation(string prompt)
        {
            Console.Write($"{prompt}? (y/n): ");
            return Console.ReadLine()?.ToLower() == "y";
        }

        private static bool ConfirmMigration(string emulatorName, string sourcePath, string targetPath)
        {
            Console.WriteLine($"Source: {sourcePath}");
            Console.WriteLine($"Target: {targetPath}");

            if (Directory.Exists(targetPath))
            {
                Console.WriteLine("⚠️  Target directory already exists. Contents will be overwritten.");
            }

            return GetUserConfirmation($"Migrate {emulatorName}");
        }

        private static string GetEmulatorDirectory(string emulatorPath)
        {
            if (File.Exists(emulatorPath))
            {
                return Path.GetDirectoryName(emulatorPath);
            }

            if (Directory.Exists(emulatorPath))
            {
                return emulatorPath;
            }

            throw new DirectoryNotFoundException($"Emulator path not found: {emulatorPath}");
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            Directory.CreateDirectory(targetDir);

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string targetFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
            }

            Console.WriteLine($"Copied {files.Length} files");
        }
    }
}