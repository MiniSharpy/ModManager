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
    public class Mod : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _IsActive;

        public string Name { get; set; }

        /// <summary>
        /// Setter alters <see cref="Plugins"/> by adding or removing <see cref="ManagedPlugins"/> when enabled or disabled.
        /// </summary>
        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                _IsActive = value;
                FileIO.SaveModOrder(Mods);

                FileIO.LoadPluginsAndSaveChanges(Plugins, Mods);

                // Tell Avalonia that IsActive has been updated, need to run on entire collection as the priority is linked to index
                foreach (var plugin in Plugins)
                {
                    Dispatcher.UIThread.Post(() => plugin.UpdatePriorityInAvalonia());
                }
            }   
        }

        /// <summary>
        /// Priority determines what files overwrite another in a mod order, with a lower value being a lower priority and getting overwritten by higher priority mods.
        /// </summary>
        /// <remarks>
        /// Priority is determined by the <see cref="Mod"/>'s index in <see cref="Mods"/>.
        /// </remarks>
        public int Priority
        {
            get { return Mods.IndexOf(this); }
            set
            {
                value = Math.Clamp(value, 0, Mods.Count - 1);

                // Use UI thread to avoid Null Reference Exception when Avalonia get confused by changes to the collection
                // and to ensure everything gets updated with the changed values.
                Dispatcher.UIThread.Post(() => Mods.RemoveAt(Priority));
                Dispatcher.UIThread.Post(() => Mods.Insert(value, this));
                Dispatcher.UIThread.Post(() => FileIO.SaveModOrder(Mods));

                // Tell Avalonia that Priority has been updated, need to run on entire collection as the priority is linked to index
                foreach (var mod in Mods)
                {
                    Dispatcher.UIThread.Post(() => mod.NotifyPropertyChanged());
                }
            }
        }

        /// <summary>
        /// The path to the directory containing all the files managed by a <see cref="Mod"/>.
        /// </summary>
        public string SourceDirectory => Path.Combine(FileIO.ModsDirectory, Name);

        /// <summary>
        /// The paths to each plugin file managed by a <see cref="Mod"/>.
        /// </summary>
        public IEnumerable<string> ManagedPlugins => FileIO.GetPluginNamesFromDirectory(SourceDirectory);

        /// <summary>
        /// A reference to <see cref="ModManager.ViewModels.MainWindowViewModel.Mods"/>.
        /// </summary>
        /// <remarks>
        /// Should always be the same reference as the original.
        /// </remarks>
        private ObservableCollection<Mod> Mods { get; }

        /// <summary>
        /// A reference to <see cref="ModManager.ViewModels.MainWindowViewModel.Plugins"/>.
        /// </summary>
        /// <remarks>
        /// Should always be the same reference as the original.
        /// </remarks>
        private ObservableCollection<Plugin> Plugins { get; }

        public Mod(string name, bool isActive, ObservableCollection<Mod> mods, ObservableCollection<Plugin> plugins)
        {
            Name = name;
            _IsActive = isActive; // Directly set backing field to avoid calling FileIO.Save in the property. This is to stop it running potentially hundreds of times during initial setup as it can be called manually anyway.
            Mods = mods;
            Plugins = plugins;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal class ModDuplicateEqualityComparer : IEqualityComparer<Mod>
    {
        public static ModDuplicateEqualityComparer Instance { get; } = new();

        private ModDuplicateEqualityComparer() { }

        public bool Equals(Mod? x, Mod? y)
        {
            return x?.Name.ToLower() == y?.Name.ToLower();
        }

        public int GetHashCode([DisallowNull] Mod obj)
        {
            return obj.Name.ToLower().GetHashCode();
        }
    }
}
