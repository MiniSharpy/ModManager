using DynamicData;
using ModManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ModManager
{
    public static class FileIO
    {
        private static string? _GameSourceDirectory;

        public static string? GameSourceDirectory
        {
            get { return _GameSourceDirectory; }
            set
            {
                _GameSourceDirectory = value;

                GameDataSourceDirectory = value != null ? Path.Combine(value, "Data") : null;
            }
        }

        public static string? GameDataSourceDirectory { get; private set; }
        public static string? PluginOrderFile { get; set; }

        public static readonly string ModManagerDirectory = Directory.GetCurrentDirectory(); // In VSCode this won't be the build folder for some reason.
        public static readonly string ModsDirectory = Path.Combine(ModManagerDirectory, "Mods");
        public static readonly string ModManagerConfigFile = Path.Combine(ModManagerDirectory, "GeneralConfig.json");

        public static readonly string GameTargetDirectory = Path.Combine(ModManagerDirectory, "Game");
        public static readonly string GameDataTargetDirectory = Path.Combine(GameTargetDirectory, "Data");
        public static readonly string ModOrderFile = Path.Combine(ModManagerDirectory, "mods.txt");

        static FileIO()
        {
            // Read config from JSON
            if (File.Exists(ModManagerConfigFile))
            {
                string json = File.ReadAllText(ModManagerConfigFile);
                GeneralConfig config = JsonSerializer.Deserialize<GeneralConfig>(json)!; // Doesn't matter if it's null as we check for that on launch.
                // TODO: Handle loading improperly. See https://github.com/Sonozuki/NovaEngine/blob/450c09a5fd221f8cdb99caaf71c929324dda9913/NovaEngine/Settings/SettingsBase.cs#L36
                GameSourceDirectory = config.GameSourceDirectory!;
                PluginOrderFile = config.PluginOrderFile!;
            }
        }

        public static void SaveGeneralConfig()
        {
            GeneralConfig config = new()
            {
                GameSourceDirectory = GameSourceDirectory,
                PluginOrderFile = PluginOrderFile
            };

            string json = JsonSerializer.Serialize(config);
            File.WriteAllText(ModManagerConfigFile, json);
        }

        /// <summary>
        /// Loads plugins found in <see cref="GameDataTargetDirectory"/> and <see cref="ModsDirectory"/> into a format Avalonia UI can use and then resaves to account for missing/additional plugins.<br/>
        /// Order is based on the contents of <see cref="PluginOrderFile"/>, but plugins are added and removed based on the contents of <see cref="GameDataTargetDirectory"/> and subdirectories in <see cref="ModsDirectory"/>.<br/>
        /// TODO: Handle cases where there are duplicates in a dodgily setup plugins.txt, preferring to stick with active plugins over non-active duplicates?<br/>
        /// TODO: Show core plugins, and handle missing ones. With SE/AE it doesn't really matter as you can't disable DLC anyway.<br/>
        /// </summary>
        /// <param name="plugins">A reference to the plugin collection that Avalonia UI uses to construct the view.</param>
        /// <param name="mods">A reference to the mod collection that Avalonia UI uses to construct the view.</param>
        /// <remarks>
        /// Windows cannot have an asterisk in file/directory names, meaning we know that an asterisk is likely not a part of the mod's name.<br/>
        /// On Linux you can get files with asterisk in the name, but it's convoluted to do and is unlikely to occur. In addition Skyrim/Proton/WINE is unlikely to handle it well anyway.<br/>
        /// </remarks>
        public static void LoadPluginsAndSaveChanges(ObservableCollection<Plugin> plugins, ObservableCollection<Mod> mods)
        {
            List<string> pluginOrder = new();
            if (File.Exists(PluginOrderFile))
            {
                pluginOrder = File.ReadAllLines(PluginOrderFile).ToList(); // 1. Load plugins.txt to determine order and if active. These plugins might not exist due to external user action, but determine priority based on their order.
            }

            // Get plugins from directory, priority doesn't matter as this is to check if a plugin still exists or if a new plugin has been added externally.
            IEnumerable<string> existingPlugins = GetPluginNamesFromDirectory(GameDataSourceDirectory!); // 2. Load plugins in GameDataSource. These plugins exist, but have not been created by the manager.
            IEnumerable<string> pluginsForActiveMods = mods.Where(mod => mod.IsActive).SelectMany(mod => mod.ManagedPlugins); // 3. Load active plugins in ModsDirectory. All the plugins in ModsDirectory exist, but non-active mods won't have their plugins placed in the hard linked folder.
            existingPlugins = existingPlugins.Concat(pluginsForActiveMods);

            IEnumerable<string> potentialPlugins = pluginOrder.Concat(existingPlugins); // Plugin order must come first to get the correct priority.
            List<Plugin> nonDistinctPlugins = new();
            foreach (string plugin in potentialPlugins)
            {
                var isActive = plugin[0] == '*';
                var name = isActive ? plugin.Substring(1) : plugin;

                if (!existingPlugins.Contains(name)) // 4. Remove non-existent plugins from plugins.txt. Existing plugins will never contain an asterisk as they are retrieved from a directory, so we can use that to check if a plugin still exists.
                    continue;

                nonDistinctPlugins.Add(new Plugin(name, isActive, plugins));
            }

            // Remove core plugins from managed plugins as we can't do anything with them anyway. This is different for other Bethesda games.
            string[] corePlugins = new[] { "skyrim.esm", "update.esm", "dawnguard.esm", "hearthfires.esm", "dragonborn.esm" }; // Store as lower case for comparison's sake. TODO: Make a global specific to different games.

            plugins.Clear(); // Clear to prevent duplicates if LoadPlugins is called more than once.
            foreach (var plugin in nonDistinctPlugins.Distinct(PluginDuplicateEqualityComparer.Instance).Where(plugin => !corePlugins.Contains(plugin.Name.ToLower()))) // Add only the plugins that aren't duplicates (names are unique) or core plugins. Order is important when distincting as it determines priority.
            {
                plugins.Add(plugin);
            }

            SavePluginOrder(plugins); // Save plugins in case we need to create the file or update with any new/removed plugins.
        }

        /// <summary>
        /// Loads mods found in <see cref="ModsDirectory"/> into a format Avalonia UI can use and then resaves to account for missing/additional mods.<br/>
        /// Order is based on the contents of <see cref="ModOrderFile"/>, but mods are added and removed based on the contents of <see cref="ModsDirectory"/>.<br/>
        /// </summary>
        /// <param name="plugins">A reference to the plugin collection that Avalonia UI uses to construct the view.</param>
        /// <param name="mods">A reference to the mod collection that Avalonia UI uses to construct the view.</param>
        public static void LoadModsAndSaveChanges(ObservableCollection<Mod> mods, ObservableCollection<Plugin> plugins)
        {
            List<string> modOrder = new();
            if (File.Exists(ModOrderFile))
            {
                modOrder = File.ReadAllLines(ModOrderFile).ToList(); // 1. Load mods.txt to determine order and if active. These mods might not exist due to external user action, but determine priority based on their order.
            }

            // Get mods from directory, priority doesn't matter as this is to check if a mod still exists or if a new mod has been added externally.
            Directory.CreateDirectory(ModsDirectory);
            List<string> existingMods = GetModNamesFromModDirectory().ToList(); // 2. Load mods in ModDirectory. These mods definitely exist, but might not have been created by the manager and are posssibly missing metadata.

            List<string> potentialMods = modOrder.Concat(existingMods).ToList(); // Mod order must come first to get the correct priority.
            List<Mod> nonDistinctMods = new();
            foreach (string mod in potentialMods)
            {
                var isActive = mod[0] == '*';
                var name = isActive ? mod.Substring(1) : mod;

                if (!existingMods.Contains(name)) // 3. Remove non-existent mods from mods.txt. Existing mods will never contain an asterisk as they are retrieved from a directory, so we can use that to check if a mod still exists.
                {
                    continue;
                }

                nonDistinctMods.Add(new Mod(name, isActive, mods, plugins));
            }

            mods.Clear();
            foreach (var mod in nonDistinctMods.Distinct(ModDuplicateEqualityComparer.Instance)) // 4. Remove duplicates. Order is important when distincting as it determines priority.
            {
                mods.Add(mod);
            }

            SaveModOrder(mods); // Save mods in case we need to create the file or update with any new/removed mods.
        }

        /// <summary>
        /// Saves plugins to file after converting into a format readable by the game.
        /// </summary>
        /// <param name="plugins">The collection of plugins to save to file.</param>
        /// <remarks>
        /// Gets called every time an Avalonia editable property of a <see cref="Plugin"/> is changed.
        /// </remarks>
        public static void SavePluginOrder(ObservableCollection<Plugin> plugins)
        {
            string[] formattedPlugins = plugins.Select(plugin => (plugin.IsActive ? "*" : "") + plugin.Name).ToArray(); // Add back the asterisk to denote an active plugin.

            if (!File.Exists(PluginOrderFile))
            {
                File.Create(PluginOrderFile!).Close();
            }

            File.WriteAllLines(PluginOrderFile!, formattedPlugins);
        }

        /// <summary>
        /// Saves plugins to file after converting into a format readable by the game.
        /// </summary>
        /// <param name="plugins">The collection of plugins to save to file.</param>
        /// <remarks>
        /// Gets called every time an Avalonia editable property of a <see cref="Mod"/> is changed.
        /// </remarks>
        public static void SaveModOrder(ObservableCollection<Mod> mods)
        {
            string[] formattedMods = mods.Select(mod => (mod.IsActive ? "*" : "") + mod.Name).ToArray(); // Add back the asterisk to denote an active plugin. May as well follow the Bethesda convention.

            if (!File.Exists(ModOrderFile))
            {
                File.Create(ModOrderFile).Close();
            }

            File.WriteAllLines(ModOrderFile, formattedMods);
        }

        public static IEnumerable<string> GetPluginNamesFromDirectory(string directory) =>
            Directory.EnumerateFiles(directory, "*.esm", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(directory, "*.esp", SearchOption.TopDirectoryOnly))
                .Concat(Directory.EnumerateFiles(directory, "*.esl", SearchOption.TopDirectoryOnly))
                .Select(path => Path.GetFileName(path))
                .OrderBy(file => file);

        public static IEnumerable<string> GetModNamesFromModDirectory() =>
            Directory.GetDirectories(ModsDirectory)
                .Select(path => Path.GetFileName(path))
                .OrderBy(directory => directory);

        public static void CreateHardLinks(string source, string target) // Thank you, https://github.com/Sonozuki.
        {
            static bool CreateHardLink(string newPath, string oldPath)
            {
                if (OperatingSystem.IsWindows())
                {
                    return Kernel32.CreateHardLink(newPath, oldPath, IntPtr.Zero);
                }
                else if (OperatingSystem.IsLinux())
                {
                    return Libc.link(oldPath, newPath) == 0;
                }

                return false;
            }

            var directoriesToLink = new Queue<string>();
            directoriesToLink.Enqueue(source);

            while (directoriesToLink.Any())
            {
                var directoryInfo = new DirectoryInfo(directoriesToLink.Dequeue());

                foreach (var file in directoryInfo.EnumerateFiles())
                {
                    var oldFileRelative = Path.GetRelativePath(source, file.FullName);
                    var newDirectory = Path.Combine(target, Path.GetDirectoryName(oldFileRelative)!);
                    var newFile = Path.Combine(target, oldFileRelative);

                    Directory.CreateDirectory(newDirectory); // Kernel32.CreateHardLink won't automatically create the directory

                    if (!CreateHardLink(newFile, file.FullName))
                        Console.WriteLine($"Error occured when creating hardlink for file: {newFile}. Error code: {Marshal.GetLastPInvokeError()}");
                }

                foreach (var dir in directoryInfo.EnumerateDirectories())
                    directoriesToLink.Enqueue(dir.FullName);
            }
        }
    }
}

