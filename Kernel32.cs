using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModManager
{
    public static class Kernel32
    {
        [DllImport("Kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern bool CreateHardLink(string newPath, string oldPath, IntPtr securityAttributes);
    }
}
