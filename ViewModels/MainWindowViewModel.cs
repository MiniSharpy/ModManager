using ModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Text;

namespace ModManager.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        static readonly string PluginFilePath = "C:\\Users\\Bradley\\AppData\\Local\\Skyrim Special Edition GOG\\plugins.txt";
        static public List<PluginData> Plugins { get; set; } = LoadPlugins();

        /// <summary>
        /// Loads plugin data from file and converts it into a format usuably with Avalonia UI.
        /// TODO: Compare with the game's data directory to see if anything is missing.
        /// TODO: If plugin.txt doesn't exist create it.
        /// </summary>
        /// <returns></returns>
        static List<PluginData> LoadPlugins()
        {
            string[] unformattedPlugins = File.ReadAllLines(PluginFilePath);

            List<PluginData> plugins = new();
            foreach (string plugin in unformattedPlugins)
            {
                if (plugin[0] == '*') // Asterick at start of plugin name means it's an active plugin. TODO: Check if you can create a plugin with an asterick at the start of the name and see what happens.
                {
                    plugins.Add(new PluginData(plugin.Remove(0, 1), true));
                }
                else
                {
                    plugins.Add(new PluginData(plugin, false));
                }
            }

            return plugins;
        }
        /// <summary>
        /// Saves plugin data to file after converting the mod manager's representation back into a format readable by the game.
        /// Gets called everytime a change is made to the plugin order.
        /// </summary>
        static void SavePluginOrder()
        {
            string[] formattedPlugins = new string[Plugins.Count];

            for (int i = 0; i < Plugins.Count; i++)
            {
                string plugin = Plugins[i].Name;
                if (Plugins[i].IsActive == true) // Add back the asterick to denote an active plugin.
                {
                    plugin = '*' + plugin;
                }
                formattedPlugins[i] = plugin;
            }

            File.WriteAllLines(PluginFilePath, formattedPlugins);
        }
    }
}
