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
        public static readonly string GamePath = "D:\\Tools\\Skyrim Anniversary Edition"; // TODO: Set via user input on initial load.
        public static readonly string GameDataPath = GamePath + "\\Data"; // TODO: Set via user input on initial load.
        public static readonly string PluginFilePath = "C:\\Users\\Bradley\\AppData\\Local\\Skyrim Special Edition GOG\\plugins.txt"; // TODO: Set to the users path.

        public static readonly string ModManagerPath = Directory.GetCurrentDirectory();
        public static readonly string ModManagerGamePath = ModManagerPath + "\\game\\";

        /// <summary>
        /// Loads plugin data from file and converts it into a format usuable by Avalonia UI. <br/>
        /// TODO: Handle cases where there are duplicates in a dodgily setup plugins.txt, preferring to stick with active plugins over non-active duplicates? <br/>
        /// TODO: See if you can create a plugin with an asterick at the start of the name and what happens. <br/>
        /// TODO: Show core plugins, and handle missing ones. With SE/AE it doesn't really matter as you can't disable DLC anyway. <br/>
        /// </summary>
        public static void LoadPlugins(ObservableCollection<PluginData> referencedPlugins)
        {
            var plugins = new List<PluginData>();
            List<string> unformattedPlugins = new();
            if (File.Exists(PluginFilePath))
                unformattedPlugins = File.ReadAllLines(PluginFilePath).ToList();

            unformattedPlugins = unformattedPlugins.Concat(GetFilesFromData()).ToList();

            foreach (string plugin in unformattedPlugins)
            {
                if (plugin[0] == '*') // Asterick at start of plugin name means it's an active plugin. 
                {
                    plugins.Add(new PluginData(plugin.Substring(1), true, referencedPlugins));
                }
                else
                {
                    plugins.Add(new PluginData(plugin, false, referencedPlugins));
                }
            }

            // Remove core plugins from managed plugins as we can't do anything with them anyway. This is different for other Bethesda games.
            string[] corePlugins = new[] { "skyrim.esm", "update.esm", "dawnguard.esm", "hearthfires.esm", "dragonborn.esm" }; // Store as lower case for comparison's sake. TODO: Make a global specific to different games.

            referencedPlugins.Clear(); // Clear to prevent duplicates if LoadPlugins is called more than once.
            foreach (var plugin in plugins.Distinct(PluginDuplicateEqualityComparer.Instance).Where(plugin => !corePlugins.Contains(plugin.Name.ToLower())))
            {
                referencedPlugins.Add(plugin); // Remove core plugins from plugins.
            }

            SavePlugins(referencedPlugins); // SavePlugins in case we need to create the file or update with any newly found plugins.
        }

        /// <summary>
        /// Saves <see cref="Plugins"/> to file after converting into a format readable by the game.
        /// </summary>
        /// <remarks>
        /// Gets called every time a change is made to the plugin order.
        /// </remarks>
        public static void SavePlugins(ObservableCollection<PluginData> plugins)
        {
            string[] formattedPlugins = plugins.Select(plugin => (plugin.IsActive ? "*" : "") + plugin.Name).ToArray(); // Add back the asterick to denote an active plugin.

            if (!File.Exists(PluginFilePath))
                File.Create(PluginFilePath).Close();

            File.WriteAllLines(PluginFilePath, formattedPlugins);
        }

        /// <summary>
        /// test
        /// </summary>
        /// <returns> A <see cref="IEnumerable&lt;<string&lt;>"/> thigny </returns>
        public static IEnumerable<string> GetFilesFromData() =>
            Directory.EnumerateFiles(GameDataPath, "*.esm", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(GameDataPath, "*.esp", SearchOption.TopDirectoryOnly))
                .Concat(Directory.EnumerateFiles(GameDataPath, "*.esl", SearchOption.TopDirectoryOnly))
                .Select(path => Path.GetFileName(path)) // Get only the file name and extension.
                .OrderBy(file => file); // Order alphabetically

        public static void CreateHardLinks(string directory)
        {
            var directoryInfo = new DirectoryInfo(directory);
            foreach (var file in directoryInfo.EnumerateFiles())
            {
                var oldFileRelative = Path.GetRelativePath(GamePath, file.FullName);
                var newDirectory = ModManagerGamePath + Path.GetDirectoryName(oldFileRelative);
                var newFile = ModManagerGamePath + oldFileRelative;

                Directory.CreateDirectory(newDirectory); // win api will not create hardlink if directory doesn't exist

                Kernel32.CreateHardLink(newFile, file.FullName, IntPtr.Zero);
            }

            foreach (var dir in directoryInfo.EnumerateDirectories())
            {
                CreateHardLinks(dir.FullName);
            }
        }
    }
}
internal static class Kernel32
{
    [DllImport("Kernel32")]
    public static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}