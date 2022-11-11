using DynamicData;
using ModManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ModManager
{
    public static class FileIO
    {
        public static readonly string GameSourceDirectory = "D:/Tools/Skyrim Anniversary Edition";  // TODO: Set via user input on initial load.
        public static readonly string GameDataSourceDirectory = Path.Combine(GameSourceDirectory, "Data");
        public static readonly string PluginOrderFile = "C:/Users/Bradley/AppData/Local/Skyrim Special Edition GOG/plugins.txt"; // TODO: Set to the users path.

        public static readonly string ModManagerDirectory = Directory.GetCurrentDirectory();
        public static readonly string ModsDirectory = Path.Combine(ModManagerDirectory, "Mods");

        public static readonly string GameTargetDirectory = Path.Combine(ModManagerDirectory, "Game");
        public static readonly string GameDataTargetDirectory = Path.Combine(GameTargetDirectory, "Data");
        public static readonly string ModOrderFile = Path.Combine(ModManagerDirectory, "mods.txt");

        /// <summary>
        /// Loads plugins found in <see cref="GameDataTargetDirectory"/> and <see cref="ModsDirectory"/> into a format Avalonia UI can use and then resaves to account for missing/additional plugins. <br/>
        /// Order is based on the contents of <see cref="PluginOrderFile"/>, but plugins are added and removed based on the contents of <see cref="GameDataTargetDirectory"/> and subdirectories in <see cref="ModsDirectory"/>. <br/>
        /// TODO: Handle cases where there are duplicates in a dodgily setup plugins.txt, preferring to stick with active plugins over non-active duplicates? <br/>
        /// TODO: Show core plugins, and handle missing ones. With SE/AE it doesn't really matter as you can't disable DLC anyway. <br/>
        /// </summary>
        /// <remarks>
        /// Windows cannot have an asterick in file/directory names, meaning we know that an asterick is likely not a part of the mod's name. <br/>
        /// On Linux you can get files with astericks in the name, but it's convoluted to do and is unlikely to occur. <br/>
        /// </remarks>
        public static void LoadPlugins(ObservableCollection<PluginData> plugins, ObservableCollection<ModData> mods)
        {
            List<string> pluginOrder = new(); // These plugins might not exist due to external user action, but determine priority based on their order.
            if (File.Exists(PluginOrderFile))
            {
                pluginOrder = File.ReadAllLines(PluginOrderFile).ToList(); // 1. Load plugins.txt to determine order and if active.
            }

            // Get plugins from directory, priority doesn't matter as this is to check if a plugin still exists or if a new plugin has been added externally.
            IEnumerable<string> existingPlugins = GetPluginNamesFromDirectory(GameDataSourceDirectory); // 2. Load plugins in GameDataSource. These plugins exist, but have been created by the manager.
            IEnumerable<string> pluginsForActiveMods = mods.Where(mod => mod.IsActive).SelectMany(mod => mod.Plugins); // 3. Load active plugins in ModsDirectory. All the plugins in ModsDirectory exist but might not need to be added to GameTargetDirectory and so shouldn't be managed.
            existingPlugins = existingPlugins.Concat(pluginsForActiveMods);

            IEnumerable<string> potentialPlugins = pluginOrder.Concat(existingPlugins); // Plugin order must come first to get the correct priority.
            List<PluginData> nonDistinctPlugins = new();
            foreach (string plugin in potentialPlugins)
            {
                var isActive = plugin[0] == '*';
                var name = isActive ? plugin.Substring(1) : plugin;

                if (!existingPlugins.Contains(name)) //4. Remove non-existance mods from mods.txt. Existing plugins will never contain an asterick as they are retrieved from a directory, so we can use that to check if a plugin still exists.
                    continue;

                nonDistinctPlugins.Add(new PluginData(name, isActive, plugins));
            }

            // Remove core plugins from managed plugins as we can't do anything with them anyway. This is different for other Bethesda games.
            string[] corePlugins = new[] { "skyrim.esm", "update.esm", "dawnguard.esm", "hearthfires.esm", "dragonborn.esm" }; // Store as lower case for comparison's sake. TODO: Make a global specific to different games.

            plugins.Clear(); // Clear to prevent duplicates if LoadPlugins is called more than once.
            foreach (var plugin in nonDistinctPlugins.Distinct(PluginDuplicateEqualityComparer.Instance).Where(plugin => !corePlugins.Contains(plugin.Name.ToLower()))) // Add only the plugins that aren't duplicates (names are unique) or core plugins. Order is important when distincting as it determines priority.
            {
                plugins.Add(plugin);
            }

            SavePluginOrder(plugins); // SavePlugins in case we need to create the file or update with any newly found plugins.
        }

        /// <summary>
        /// Saves <see cref="Plugins"/> to file after converting into a format readable by the game.
        /// </summary>
        /// <remarks>
        /// Gets called every time a change is made to the plugin order.
        /// </remarks>
        public static void SavePluginOrder(ObservableCollection<PluginData> plugins)
        {
            string[] formattedPlugins = plugins.Select(plugin => (plugin.IsActive ? "*" : "") + plugin.Name).ToArray(); // Add back the asterick to denote an active plugin.

            if (!File.Exists(PluginOrderFile))
                File.Create(PluginOrderFile).Close();

            File.WriteAllLines(PluginOrderFile, formattedPlugins);
        }

        /// <summary>
        /// Loads mods found in <see cref="ModsDirectory"/> into a format Avalonia UI can use and then resaves to account for missing/additional mods. <br/>
        /// Order is based on the contents of <see cref="ModOrderFile"/>, but mods are added and removed based on the contents of <see cref="ModsDirectory"/>. <br/>
        /// </summary>
        public static void LoadMods(ObservableCollection<ModData> mods, ObservableCollection<PluginData> plugins)
        {
            List<string> modOrder = new(); // These plugins might not exist due to external user action, but determine priority based on their order.
            if (File.Exists(ModOrderFile))
            {
                modOrder = File.ReadAllLines(ModOrderFile).ToList(); // 1. Load mods.txt to determine order and if active.
            }

            // Get mods from directory, priority doesn't matter as this is to check if a mod still exists or if a new mod has been added externally.
            Directory.CreateDirectory(ModsDirectory);
            List<string> existingMods = GetModNamesFromModDirectory().ToList(); // 2. Load mods in ModDirectory. These mods definitely exist, but might not have been created by the manager and are posssibly missing meta data.


            List<string> potentialMods = modOrder.Concat(existingMods).ToList(); // Mod order must come first to get the correct priority.
            List<ModData> nonDistinctMods = new();
            foreach (string mod in potentialMods)
            {
                var isActive = mod[0] == '*';
                var name = isActive ? mod.Substring(1) : mod;

                if (!existingMods.Contains(name)) // 3. Remove non-existant mods from mods.txt. Existing mods will never contain an asterick as they are retrieved from a directory, so we can use that to check if a mod still exists.
                { 
                    continue;
                }

                nonDistinctMods.Add(new ModData(name, isActive, mods, plugins));
            }

            mods.Clear();
            foreach (var mod in nonDistinctMods.Distinct(ModDuplicateEqualityComparer.Instance)) // 4. Remove duplicates. Order is important when distincting as it determines priority.
            {
                mods.Add(mod);
            }

            SaveModOrder(mods);
        }

        public static void SaveModOrder(ObservableCollection<ModData> mods)
        {
            string[] formattedMods = mods.Select(mod => (mod.IsActive ? "*" : "") + mod.Name).ToArray(); // Add back the asterick to denote an active plugin. May as well follow the Bethesda convention.

            if (!File.Exists(ModOrderFile))
                File.Create(ModOrderFile).Close();

            File.WriteAllLines(ModOrderFile, formattedMods);
        }

        public static IEnumerable<string> GetPluginNamesFromDirectory(string directory) =>
            Directory.EnumerateFiles(directory, "*.esm", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(directory, "*.esp", SearchOption.TopDirectoryOnly))
                .Concat(Directory.EnumerateFiles(directory, "*.esl", SearchOption.TopDirectoryOnly))
                .Select(path => Path.GetFileName(path)) // Get only the file name and extension.
                .OrderBy(file => file); // Order alphabetically

        public static IEnumerable<string> GetModNamesFromModDirectory() =>
            Directory.GetDirectories(ModsDirectory)
                .Select(path => Path.GetFileName(path))
                .OrderBy(directory => directory);

        public static void CreateHardLinks(string source, string target) // Thank you, https://github.com/Sonozuki.
        {
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

                    Directory.CreateDirectory(newDirectory); // CreateHardLink won't automatically create the directory

                    if (!Kernel32.CreateHardLink(newFile, file.FullName, IntPtr.Zero))
                        Console.WriteLine($"Error occured when creating hardlink for file: {newFile}. Error code: {Marshal.GetLastWin32Error()}");
                }

                foreach (var dir in directoryInfo.EnumerateDirectories())
                    directoriesToLink.Enqueue(dir.FullName);
            }
        }

        private static class Kernel32
        {
            [DllImport("Kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
        }
    }
}
