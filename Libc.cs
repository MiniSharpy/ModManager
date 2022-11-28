using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModManager
{
    public static class Libc
    {
        [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern int link(string oldPath, string newPath);

        [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern int chmod(string pathName, int mode);
    }
}
