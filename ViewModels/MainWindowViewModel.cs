using Avalonia.Threading;
using ModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;

namespace ModManager.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        static readonly string GameDataPath = "C:\\Games\\GOG Galaxy\\Games\\Skyrim Anniversary Edition\\Data"; // TODO: Set via user input on initial load.
        static readonly string PluginFilePath = "C:\\Users\\Bradley\\AppData\\Local\\Skyrim Special Edition GOG\\plugins.txt"; // TODO: Set to the users path..

        /// <summary>
        /// A list of all plugins where index is plugin priority. 
        /// </summary>
        /// <remarks>
        /// PluginData relies on a reference to Plugins, as of such setting after initalisation is disabled. <br/>
        /// Alter the collection (though not neccesarily the elements) via the UI thread (<see cref="Dispatcher.UIThread"/>). For example when removing an element by index do ``Dispatcher.UIThread.Post(() => Plugins.RemoveAt(Priority));`` 
        /// to avoid a <see cref="NullReferenceException"/>.
        /// </remarks>
        static public ObservableCollection<PluginData> Plugins { get; } = new();

        public MainWindowViewModel()
        {
            LoadPlugins();
        }

        /// <summary>
        /// Loads plugin data from file and converts it into a format usuable by Avalonia UI. <br/>
        /// TODO: Handle cases where there are duplicates in a dodgily setup plugins.txt, preferring to stick with active plugins over non-active duplicates? <br/>
        /// TODO: See if you can create a plugin with an asterick at the start of the name and what happens. <br/>
        /// TODO: Show core plugins, and handle missing ones. With SE/AE it doesn't really matter as you can't disable DLC anyway. <br/>
        /// </summary>
        static void LoadPlugins()
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
                    plugins.Add(new PluginData(plugin.Substring(1), true, SavePlugins, Plugins));
                }
                else
                {
                    plugins.Add(new PluginData(plugin, false, SavePlugins, Plugins));
                }
            }

            // Remove core plugins from managed plugins as we can't do anything with them anyway. This is different for other Bethesda games.
            string[] corePlugins = new[] { "skyrim.esm", "update.esm", "dawnguard.esm", "hearthfires.esm", "dragonborn.esm" }; // Store as lower case for comparison's sake. TODO: Make a global specific to different games.

            Plugins.Clear(); // Clear to prevent duplicates if LoadPlugins is called more than once.
            foreach (var plugin in plugins.Distinct(PluginDuplicateEqualityComparer.Instance).Where(plugin => !corePlugins.Contains(plugin.Name.ToLower())))
            { 
                Plugins.Add(plugin); // Remove core plugins from plugins.
            }

            SavePlugins(); // SavePlugins in case we need to create the file or update with any newly found plugins.
        }

        /// <summary>
        /// Saves <see cref="Plugins"/> to file after converting into a format readable by the game.
        /// </summary>
        /// <remarks>
        /// Gets called every time a change is made to the plugin order.
        /// </remarks>
        static void SavePlugins()
        {
            string[] formattedPlugins = Plugins.Select(plugin => (plugin.IsActive ? "*" : "") + plugin.Name).ToArray(); // Add back the asterick to denote an active plugin.

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
    }
}