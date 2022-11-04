using System;
using System.Collections.Generic;
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
}
