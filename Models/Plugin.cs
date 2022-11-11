using Avalonia.Logging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModManager.Models
{
    public class Plugin : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _IsActive;

        public string Name { get; set; }

        /// <remarks>
        /// Setter automatically calls <see cref="FileIO.SavePluginOrder"/> with <see cref="Plugins"/> as the parameter. 
        /// </remarks>
        public bool IsActive
        {
            get { return _IsActive; }
            set { _IsActive = value; FileIO.SavePluginOrder(Plugins); }
        }

        /// <summary>
        /// Priority determines what records overwrite another in a plugin/load order, with a lower value being a lower priority and getting overwritten by higher priority plugins.
        /// </summary>
        /// <remarks>
        /// Priority is determined by the <see cref="Plugin"/>'s index in <see cref="Plugins"/>.
        /// </remarks>
        public int Priority
        {
            get { return Plugins.IndexOf(this); }
            set
            {
                value = Math.Clamp(value, 0, Plugins.Count - 1);

                // Use UI thread to avoid Null Reference Exception when Avalonia get confused by changes to the collection
                // and to ensure everything gets updated with the changed values.
                Dispatcher.UIThread.Post(() => Plugins.RemoveAt(Priority));
                Dispatcher.UIThread.Post(() => Plugins.Insert(value, this));
                Dispatcher.UIThread.Post(() => FileIO.SavePluginOrder(Plugins));

                // Tell Avalonia that Priority has been updated, need to run on entire collection as the priority is linked to index
                foreach (var plugin in Plugins)
                {
                    Dispatcher.UIThread.Post(() => plugin.NotifyPropertyChanged());
                }
            }
        }

        /// <summary>
        /// A reference to <see cref="ModManager.ViewModels.MainWindowViewModel.Plugins"/>.
        /// </summary>
        /// <remarks>
        /// Should always be the same reference as the original.
        /// </remarks>
        private ObservableCollection<Plugin> Plugins { get; }

        public Plugin(string name, bool isActive, ObservableCollection<Plugin> plugins)
        {
            Name = name;
            _IsActive = isActive; // Directly set backing field to avoid calling FileIO.SavePlugins(Plugins) in the property. This is to stop it running potentially hundreds of times during initial setup as it can be called manually anyway.
            Plugins = plugins;

        }

        public void UpdatePriorityInAvalonia() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal class PluginDuplicateEqualityComparer : IEqualityComparer<Plugin>
    {
        public static PluginDuplicateEqualityComparer Instance { get; } = new();

        private PluginDuplicateEqualityComparer() { }

        public bool Equals(Plugin? x, Plugin? y)
        {
            return x?.Name.ToLower() == y?.Name.ToLower();
        }

        public int GetHashCode([DisallowNull] Plugin obj)
        {
            return obj.Name.ToLower().GetHashCode();
        }
    }
}
