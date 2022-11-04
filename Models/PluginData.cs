using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModManager.Models
{
    public class PluginData
    {
        public string Name { get; set; }
        public bool IsActive { get; set; }

        public PluginData(string name, bool isActive)
        {
            Name = name;
            IsActive = isActive;
        }
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
