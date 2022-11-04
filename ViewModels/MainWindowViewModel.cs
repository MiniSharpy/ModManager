using ModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;

namespace ModManager.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        static readonly string GameDataPath = "C:\\Games\\GOG Galaxy\\Games\\Skyrim Anniversary Edition\\Data";
        static readonly string PluginFilePath = "C:\\Users\\Bradley\\AppData\\Local\\Skyrim Special Edition GOG\\plugins.txt";

        static public List<PluginData> Plugins { get; set; } = LoadPlugins();

        /// <summary>
        /// Loads plugin data from file and converts it into a format usuable by Avalonia UI. <br/>
        /// TODO: Handle cases where there are duplicates in a dodgily setup plugins.txt, preferring to stick with active plugins over non-active duplicates? <br/>
        /// TODO: See if you can create a plugin with an asterick at the start of the name and what happens. <br/>
        /// TODO: Show core plugins, and handle missing ones. With SE/AE it doesn't really matter as you can't disable DLC anyway. <br/>
        /// </summary>
        static List<PluginData> LoadPlugins()
        {
            List<string> unformattedPlugins = new();
            if (File.Exists(PluginFilePath))
                unformattedPlugins = File.ReadAllLines(PluginFilePath).ToList();

            unformattedPlugins = unformattedPlugins.Concat(GetFilesFromData()).ToList();

            List<PluginData> plugins = new();
            foreach (string plugin in unformattedPlugins)
            {
                if (plugin[0] == '*') // Asterick at start of plugin name means it's an active plugin. 
                {
                    plugins.Add(new PluginData(plugin.Substring(1), true));
                }
                else
                {
                    plugins.Add(new PluginData(plugin, false));
                }
            }

            plugins = plugins.Distinct(PluginDuplicateEqualityComparer.Instance).ToList(); // Distinct to remove any duplicates from concatenating GetFilesFromData().

            // Remove core plugins from managed plugins as we can't do anything with them anyway.
            string[] corePlugins = new[] { "skyrim.esm", "update.esm", "dawnguard.esm", "hearthfires.esm", "dragonborn.esm" }; // Store as lower case for comparison's sake.
            plugins = plugins.Where(plugin => !corePlugins.Contains(plugin.Name.ToLower())).ToList(); // Remove core plugins from plugins.
            // var orderedCorePlugins = corePlugins.Intersect(plugins.Select(plugin => plugin.Name.ToLower())).Reverse(); // Get core plugins that appear in plugins.txt, perhaps not all dlc is avaliable. Reversed so as to add in foreach loop at index 0.

            SavePlugins(plugins); // SavePlugins in case we need to create the file or update with any newly found plugins.

            return plugins;
        }

        /// <summary>
        /// Saves plugin data to file after converting the mod manager's representation back into a format readable by the game.
        /// Gets called every time a change is made to the plugin order.
        /// </summary>
        static void SavePlugins()
        {
            SavePlugins(Plugins);
        }

        static void SavePlugins(List<PluginData> plugins)
        {
            string[] formattedPlugins = plugins.Select(plugin => (plugin.IsActive ? "*" : "") + plugin.Name).ToArray(); // Add back the asterick to denote an active plugin.

            if (!File.Exists(PluginFilePath))
                File.Create(PluginFilePath).Close();

            File.WriteAllLines(PluginFilePath, formattedPlugins);
        }

        static IEnumerable<string> GetFilesFromData() =>
        Directory.EnumerateFiles(GameDataPath, "*.esm", SearchOption.TopDirectoryOnly)
        .Concat(Directory.EnumerateFiles(GameDataPath, "*.esp", SearchOption.TopDirectoryOnly))
        .Concat(Directory.EnumerateFiles(GameDataPath, "*.esl", SearchOption.TopDirectoryOnly))
        .Select(path => Path.GetFileName(path)) // Get only the file name and extension.
        .OrderBy(file => file); // Order alphabetically

        public void Swap(ref PluginData a, ref PluginData b) => (a, b) = (b, a);
    }
}