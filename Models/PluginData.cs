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
    public class PluginData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _IsActive;

        private Action SavePlugins;

        public string Name { get; set; }

        public bool IsActive
        {
            get { return _IsActive; }
            set { _IsActive = value; SavePlugins?.Invoke(); }
        }

        /// <summary>
        /// Priority determines what records overwrite another in a plugin/load order, with a lower value being a lower priority and getting overwritten by higher priority plugins.
        /// </summary>
        /// <remarks>
        /// Priority is determined by the PluginData's index in <see cref="Plugins"/>.
        /// </remarks>
        public int Priority
        {
            get { return Plugins.IndexOf(this); }
            set
            {
                // Use UI thread to avoid Null Reference Exception when Avalonia get confused by changes to the collection
                // and to ensure everything gets updated with the changed values.
                Dispatcher.UIThread.Post(() => Plugins.RemoveAt(Priority)); 
                Dispatcher.UIThread.Post(() => Plugins.Insert(value, this));
                Dispatcher.UIThread.Post(() => SavePlugins?.Invoke());

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
        /// Should always have the same values as the original.
        /// </remarks>
        private ObservableCollection<PluginData> Plugins { get; }

        public PluginData(string name, bool isActive, Action savePlugins, ObservableCollection<PluginData> plugins)
        {
            Name = name;
            IsActive = isActive;
            SavePlugins = savePlugins;
            Plugins = plugins;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal class PluginDuplicateEqualityComparer : IEqualityComparer<PluginData>
    {
        public static PluginDuplicateEqualityComparer Instance { get; } = new();

        private PluginDuplicateEqualityComparer() { }

        public bool Equals(PluginData? x, PluginData? y)
        {
            return x?.Name.ToLower() == y?.Name.ToLower();
        }

        public int GetHashCode([DisallowNull] PluginData obj)
        {
            return obj.Name.ToLower().GetHashCode();
        }
    }
}
