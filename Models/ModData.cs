using Avalonia.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.IO;
using System.Linq;
using System;

namespace ModManager.Models
{
    public class ModData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _IsActive;
        public string Name { get; set; }

        /// <summary>
        /// Alters <see cref="AllPlugins"/> by adding or removing a plugin when changed.
        /// </summary>
        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                _IsActive = value;
                FileIO.SaveModOrder(AllMods);

                // Updates plugins.
                if (IsActive) // Add plugin to collection, disabled by default.
                {
                    foreach (var plugin in Plugins)
                    {
                        AllPlugins.Add(new PluginData(plugin, false, AllPlugins));
                    }
                }
                else // Remove plugin from collection.
                {
                    IEnumerable<PluginData> plugins = AllPlugins.Where(plugin => Plugins.Contains(plugin.Name));

                    foreach (var plugin in plugins)
                    {

                        Dispatcher.UIThread.Post(() => AllPlugins.Remove(plugin));

                    }
                }
                Dispatcher.UIThread.Post(() => FileIO.SavePluginOrder(AllPlugins));

                // Tell Avalonia that IsActive has been updated, need to run on entire collection as the priority is linked to index
                foreach (var plugin in AllPlugins)
                {
                    Dispatcher.UIThread.Post(() => plugin.UpdatePriorityInAvalonia());
                }
            }   
        }

        public int Priority
        {
            get { return AllMods.IndexOf(this); }
            set
            {
                value = Math.Max(value, 0); // Stop out of range exceptions.
                value = Math.Min(value, AllMods.Count - 1); // -1 as we're about to remove an element.

                // Use UI thread to avoid Null Reference Exception when Avalonia get confused by changes to the collection
                // and to ensure everything gets updated with the changed values.
                Dispatcher.UIThread.Post(() => AllMods.RemoveAt(Priority));
                Dispatcher.UIThread.Post(() => AllMods.Insert(value, this));
                Dispatcher.UIThread.Post(() => FileIO.SaveModOrder(AllMods));

                // Tell Avalonia that Priority has been updated, need to run on entire collection as the priority is linked to index
                foreach (var mod in AllMods)
                {
                    Dispatcher.UIThread.Post(() => mod.NotifyPropertyChanged());
                }
            }
        }

        public string SourceDirectory => Path.Combine(FileIO.ModsDirectory, Name);

        public IEnumerable<string> Plugins => FileIO.GetPluginNamesFromDirectory(SourceDirectory);

        /// <remarks>
        /// Should always have the same values as the original.
        /// </remarks>
        private ObservableCollection<ModData> AllMods { get; }
        private ObservableCollection<PluginData> AllPlugins { get; }

        public ModData(string name, bool isActive, ObservableCollection<ModData> mods, ObservableCollection<PluginData> plugins)
        {
            Name = name;
            _IsActive = isActive; // Directly set backing field to avoid calling FileIO.Save in the property. This is to stop it running potentially hundreds of times during initial setup as it can be called manually anyway.
            AllMods = mods;
            AllPlugins = plugins;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    internal class ModDuplicateEqualityComparer : IEqualityComparer<ModData>
    {
        public static ModDuplicateEqualityComparer Instance { get; } = new();

        private ModDuplicateEqualityComparer() { }

        public bool Equals(ModData? x, ModData? y)
        {
            return x?.Name.ToLower() == y?.Name.ToLower();
        }

        public int GetHashCode([DisallowNull] ModData obj)
        {
            return obj.Name.ToLower().GetHashCode();
        }
    }
}
