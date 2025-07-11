using System.Windows.Forms;

namespace MigrationTool;
class Program
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

    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("=== Xenia Manager Migration Tool ===\n");
        try
        {
            // Get old and new Xenia Manager paths
            var (oldXeniaManagerPath, oldXeniaManagerConfigPath) = GetXeniaManagerPath("old");
            var (newXeniaManagerPath, newXeniaManagerConfigPath) = GetXeniaManagerPath("new");

            // Load configurations
            Console.WriteLine("Loading configurations...");
            var oldConfigJson = File.ReadAllText(oldXeniaManagerConfigPath);
            var newConfigJson = File.ReadAllText(newXeniaManagerConfigPath);

            var oldConfigStatus = SettingsParser.GetEmulatorStatus(oldConfigJson, false);
            var oldConfig = SettingsParser.ParseOldConfig(oldConfigJson, oldXeniaManagerPath);
            var newConfigStatus = SettingsParser.GetEmulatorStatus(newConfigJson, true);
            var newConfig = SettingsParser.ParseNewConfig(newConfigJson, newXeniaManagerPath);

            // Display current status
            DisplayConfigurationStatus(oldConfigStatus, newConfigStatus);

            // Show migration options
            ShowMigrationMenu(oldConfig, newConfig, oldXeniaManagerPath, newXeniaManagerPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    private static (string path, string configPath) GetXeniaManagerPath(string type)
    {
        Console.WriteLine($"Please select the {type} Xenia Manager executable...");

        OpenFileDialog openFileDialog = new()
        {
            Title = $"Select {type} Xenia Manager executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
        };

        string xeniaManagerPath = string.Empty;
        string xeniaManagerConfigPath = string.Empty;

        while (string.IsNullOrEmpty(xeniaManagerPath) || string.IsNullOrEmpty(xeniaManagerConfigPath))
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var selectedPath = Path.GetDirectoryName(openFileDialog.FileName);

                if (Directory.Exists(selectedPath))
                {
                    xeniaManagerPath = selectedPath;
                    Console.WriteLine($"{type} Xenia Manager Path: {xeniaManagerPath}");
                }
                else
                {
                    Console.WriteLine($"Invalid {type} Xenia Manager Path");
                    continue;
                }

                var configPath = Path.Combine(xeniaManagerPath, "Config", "config.json");
                if (File.Exists(configPath))
                {
                    xeniaManagerConfigPath = configPath;
                    Console.WriteLine($"{type} Config found: {xeniaManagerConfigPath}");
                }
                else
                {
                    Console.WriteLine($"Config file not found in {type} Xenia Manager Path");
                    xeniaManagerPath = string.Empty; // Reset to continue loop
                }
            }
            else
            {
                Console.WriteLine("No file selected. Exiting...");
                Environment.Exit(0);
            }
        }

        return (xeniaManagerPath, xeniaManagerConfigPath);
    }

    private static void DisplayConfigurationStatus(EmulatorStatus oldStatus, EmulatorStatus newStatus)
    {
        Console.WriteLine("\n=== Configuration Status ===");
        Console.WriteLine("OLD CONFIGURATION:");
        Console.WriteLine($"  Xenia Canary: {(oldStatus.XeniaCanaryConfigured ? "✓ Configured" : "✗ Not configured")}");
        Console.WriteLine($"  Xenia Mousehook: {(oldStatus.XeniaMousehookConfigured ? "✓ Configured" : "✗ Not configured")}");
        Console.WriteLine($"  Xenia Netplay: {(oldStatus.XeniaNetplayConfigured ? "✓ Configured" : "✗ Not configured")}");
        Console.WriteLine($"  Total configured: {oldStatus.ConfiguredEmulatorCount}");

        Console.WriteLine("\nNEW CONFIGURATION:");
        Console.WriteLine($"  Xenia Canary: {(newStatus.XeniaCanaryConfigured ? "✓ Configured" : "✗ Not configured")}");
        Console.WriteLine($"  Xenia Mousehook: {(newStatus.XeniaMousehookConfigured ? "✓ Configured" : "✗ Not configured")}");
        Console.WriteLine($"  Xenia Netplay: {(newStatus.XeniaNetplayConfigured ? "✓ Configured" : "✗ Not configured")}");
        Console.WriteLine($"  Total configured: {newStatus.ConfiguredEmulatorCount}");
    }

    private static void ShowMigrationMenu(OldManagerConfig oldConfig, NewManagerConfig newConfig,
                                        string oldPath, string newPath)
    {
        if (!HasAnyEmulatorToMigrate(oldConfig))
        {
            Console.WriteLine("\n⚠️  No emulators found in old configuration to migrate.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        while (true)
        {
            Console.WriteLine("\n=== Migration Options ===");
            Console.WriteLine("Select which Xenia version(s) to migrate:");
            Console.WriteLine();

            int optionNumber = 1;
            var availableOptions = new List<MigrationOption>();

            // Individual emulator options
            if (HasValidEmulatorPath(oldConfig.XeniaCanary))
            {
                availableOptions.Add(new MigrationOption(optionNumber++, "Xenia Canary", "canary", oldConfig.XeniaCanary));
            }

            if (HasValidEmulatorPath(oldConfig.XeniaMousehook))
            {
                availableOptions.Add(new MigrationOption(optionNumber++, "Xenia Mousehook", "mousehook", oldConfig.XeniaMousehook));
            }

            if (HasValidEmulatorPath(oldConfig.XeniaNetplay))
            {
                availableOptions.Add(new MigrationOption(optionNumber++, "Xenia Netplay", "netplay", oldConfig.XeniaNetplay));
            }

            // Migrate all option
            if (availableOptions.Count > 1)
            {
                availableOptions.Add(new MigrationOption(optionNumber++, "Migrate All", "all", null));
            }

            // Display options
            foreach (var option in availableOptions)
            {
                Console.WriteLine($"{option.Number}. {option.DisplayName}");
                if (option.EmulatorInfo != null)
                {
                    Console.WriteLine($"   From: {option.EmulatorInfo.EmulatorLocation}");
                }
            }

            Console.WriteLine($"{optionNumber}. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice: ");

            if (int.TryParse(Console.ReadLine(), out int choice))
            {
                if (choice == optionNumber) // Exit option
                {
                    Console.WriteLine("Exiting...");
                    break;
                }

                var selectedOption = availableOptions.FirstOrDefault(o => o.Number == choice);
                if (selectedOption != null)
                {
                    if (selectedOption.Type == "all")
                    {
                        MigrateAllEmulators(availableOptions.Where(o => o.Type != "all").ToList(), oldPath, newPath);
                    }
                    else
                    {
                        MigrateSingleEmulator(selectedOption, oldPath, newPath);
                    }
                }
                else
                {
                    Console.WriteLine("Invalid choice. Please try again.");
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter a number.");
            }
        }
    }

    private static bool HasValidEmulatorPath(EmulatorInfo emulatorInfo)
    {
        return !string.IsNullOrEmpty(emulatorInfo.EmulatorLocation) &&
               (Directory.Exists(emulatorInfo.EmulatorLocation) || File.Exists(emulatorInfo.EmulatorLocation));
    }

    private static bool HasAnyEmulatorToMigrate(OldManagerConfig oldConfig)
    {
        return HasValidEmulatorPath(oldConfig.XeniaCanary) ||
               HasValidEmulatorPath(oldConfig.XeniaMousehook) ||
               HasValidEmulatorPath(oldConfig.XeniaNetplay);
    }

    private static void MigrateSingleEmulator(MigrationOption option, string oldBasePath, string newBasePath)
    {
        Console.WriteLine($"\n=== Migrating {option.DisplayName} ===");

        try
        {
            var sourcePath = GetEmulatorDirectory(option.EmulatorInfo.EmulatorLocation);
            var targetPath = Path.Combine(newBasePath, "Emulators", option.DisplayName, "content");

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

        Console.Write("\nProceed with migration? (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            int successful = 0;
            int failed = 0;

            foreach (var option in options)
            {
                try
                {
                    Console.WriteLine($"\nMigrating {option.DisplayName}...");
                    var sourcePath = GetEmulatorDirectory(option.EmulatorInfo.EmulatorLocation);
                    var targetPath = Path.Combine(newBasePath, "Emulators", option.DisplayName, "content");

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

            Console.WriteLine($"\n=== Migration Summary ===");
            Console.WriteLine($"Successful: {successful}");
            Console.WriteLine($"Failed: {failed}");
            Console.WriteLine($"Total: {successful + failed}");
        }
        else
        {
            Console.WriteLine("Migration cancelled.");
        }
    }

    private static bool ConfirmMigration(string emulatorName, string sourcePath, string targetPath)
    {
        Console.WriteLine($"Source: {sourcePath}");
        Console.WriteLine($"Target: {targetPath}");

        if (Directory.Exists(targetPath))
        {
            Console.WriteLine("⚠️  Target directory already exists. Contents will be overwritten.");
        }

        Console.Write($"Migrate {emulatorName}? (y/n): ");
        return Console.ReadLine()?.ToLower() == "y";
    }

    private static string GetEmulatorDirectory(string emulatorPath)
    {
        // If it's a file, return the directory containing it
        if (File.Exists(emulatorPath))
        {
            return Path.GetDirectoryName(emulatorPath);
        }

        // If it's already a directory, return it
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

        // Create target directory if it doesn't exist
        Directory.CreateDirectory(targetDir);

        // Copy all files
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string targetFile = Path.Combine(targetDir, relativePath);

            // Create subdirectory if needed
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

            // Copy file
            File.Copy(file, targetFile, true);
        }

        Console.WriteLine($"Copied {Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories).Length} files");
    }
}