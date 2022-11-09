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
    public class ModData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _IsActive;
        public string Name { get; set; }

        public bool IsActive
        {
            get { return _IsActive; }
            set { _IsActive = value; FileIO.SaveModOrder(Mods); }
        }

        public int Priority
        {
            get { return Mods.IndexOf(this); }
            set
            {
                if (value < 0 || value >= Mods.Count ) { return; } // Stop out of range exception.

                // Use UI thread to avoid Null Reference Exception when Avalonia get confused by changes to the collection
                // and to ensure everything gets updated with the changed values.
                Dispatcher.UIThread.Post(() => Mods.RemoveAt(Priority)); 
                Dispatcher.UIThread.Post(() => Mods.Insert(value, this)); 
                Dispatcher.UIThread.Post(() => FileIO.SaveModOrder(Mods));

                // Tell Avalonia that Priority has been updated, need to run on entire collection as the priority is linked to index
                foreach (var plugin in Mods)
                { 
                    Dispatcher.UIThread.Post(() => plugin.NotifyPropertyChanged());
                }
            }
        }

        /// <remarks>
        /// Should always have the same values as the original.
        /// </remarks>
        private ObservableCollection<ModData> Mods { get; }

        public ModData(string name, bool isActive, ObservableCollection<ModData> mods)
        {
            Name = name;
            _IsActive = isActive; // Directly set backing field to avoid calling FileIO.Save in the property. This is to stop it running potentially hundreds of times during initial setup as it can be called manually anyway.
            Mods = mods;

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
