using Avalonia.Threading;
using ModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ModManager.ViewModels
{
    // TODO: Ensure view updates correctly when data is changed. E.G. When a mod is enabled, show plugin in plugin side.
    public class MainWindowViewModel : ReactiveObject
    {
        /// <summary>
        /// A list of all plugins where index is plugin priority. 
        /// </summary>
        /// <remarks>
        /// PluginData relies on a reference to Plugins, as of such setting after initalisation is disabled. <br/>
        /// Alter the collection (though not neccesarily the elements) via the UI thread (<see cref="Dispatcher.UIThread"/>). For example when removing an element by index do ``Dispatcher.UIThread.Post(() => Plugins.RemoveAt(Priority));`` 
        /// to avoid a <see cref="NullReferenceException"/>.
        /// </remarks>
        static public ObservableCollection<PluginData> Plugins { get; } = new();
        static public ObservableCollection<ModData> Mods { get; } = new();

        public MainWindowViewModel()
        {
            FileIO.LoadMods(Mods, Plugins);
            FileIO.LoadPlugins(Plugins, Mods);
        }

        public static void RunGame()
        {
            string? gamePathPartition = Path.GetPathRoot(FileIO.GameDataSourceDirectory);
            string? managerPathPartition = Path.GetPathRoot(Directory.GetCurrentDirectory());
            if (gamePathPartition != managerPathPartition) { return; } // Hardlinking is only supported on the same partition. TODO: Give an error. We kinda need this feature.

            if (Directory.Exists(FileIO.GameTargetDirectory))  // Clean up so deleted mods don't remain.
            {
                Directory.Delete(FileIO.GameTargetDirectory, true);
            }

            FileIO.CreateHardLinks(FileIO.GameSourceDirectory, FileIO.GameTargetDirectory); // Hardlink the vanilla game.

            IEnumerable<ModData> activeMods = Mods.Where(plugin => plugin.IsActive).Reverse(); // Hard Links can't overwrite, so just hard link backwards.
            foreach (ModData mod in activeMods) // Hardlink the mods. TODO: Check if root mod.
            {
                FileIO.CreateHardLinks(mod.SourceDirectory, FileIO.GameDataTargetDirectory);
            }

            string path = Path.Combine(FileIO.GameTargetDirectory, "Skyrim.ccc"); // Skyrim.ccc overides plugins.txt. TODO: Have a collection of files to delete?
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ProcessStartInfo info = new(Path.Combine(FileIO.GameTargetDirectory, "SkyrimSE.exe"));
            info.WorkingDirectory = FileIO.GameTargetDirectory;
            Process.Start(info);
        }

        public static string? CompressedModPath { get; set; }
        public static async void InstallMod()
        {
            string? sourceArchive = CompressedModPath;
            if (!File.Exists(sourceArchive)) // Shouldn't occur with picking through file dialog, but just in case.
            {
                // TODO: Report error.
                return;
            }

            string modName = Path.GetFileNameWithoutExtension(sourceArchive);
            string modDirectory = Path.Combine(FileIO.ModsDirectory, modName);
            Directory.CreateDirectory(modDirectory);

            string processName; // Thank you, https://github.com/Sonozuki.
            if (OperatingSystem.IsWindows())
                processName = "7z.exe";
            else if (OperatingSystem.IsLinux())
                processName = "7zzs";
            else
            {
                Console.WriteLine("Invalid OS");
                return;
            }

            var process = Process.Start(processName, $"e -spf \"{sourceArchive}\" -o\"{modDirectory}\"");
            process.PriorityClass = ProcessPriorityClass.High;
            await process.WaitForExitAsync();

            FileIO.LoadMods(Mods, Plugins);
            FileIO.LoadPlugins(Plugins, Mods);
        }
    }
}
