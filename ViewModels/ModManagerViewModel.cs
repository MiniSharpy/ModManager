using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using ModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModManager.ViewModels
{
    internal class ModManagerViewModel : ViewModelBase
    {
        /// <summary>
        /// A list of all plugins where index is plugin priority.
        /// </summary>
        /// <remarks>
        /// <see cref="Plugin"/> relies on a reference to <see langword="this"/>, as of such setting after initialisation is disabled.<br/>
        /// Alter the collection (though not necessarily the elements) via the UI thread (<see cref="Dispatcher.UIThread"/>). For example when removing an element by index do <c>Dispatcher.UIThread.Post(() => Plugins.RemoveAt(Priority));</c>
        /// to avoid a <see cref="NullReferenceException"/> and other unexpected behaviour.
        /// </remarks>
        static public ObservableCollection<Plugin> Plugins { get; } = new();

        /// <summary>
        /// A list of all mods where index is mod priority. 
        /// </summary>
        /// <remarks>
        /// <see cref="Mod"/> relies on a reference to <see langword="this"/>, as of such setting after initialisation is disabled.<br/>
        /// Alter the collection (though not necessarily the elements) via the UI thread (<see cref="Dispatcher.UIThread"/>). For example when removing an element by index do <c>Dispatcher.UIThread.Post(() => Plugins.RemoveAt(Priority));</c>
        /// to avoid a <see cref="NullReferenceException"/> and other unexpected behaviour.
        /// </remarks>
        static public ObservableCollection<Mod> Mods { get; } = new();

        public ModManagerViewModel()
        {
            FileIO.LoadModsAndSaveChanges(Mods, Plugins);
            FileIO.LoadPluginsAndSaveChanges(Plugins, Mods);
        }

        public static void RunGame()
        {
            string? gamePathPartition = Path.GetPathRoot(FileIO.GameDataSourceDirectory);
            string? managerPathPartition = Path.GetPathRoot(FileIO.ModManagerDirectory);
            if (gamePathPartition != managerPathPartition) { return; } // Hardlinking is only supported on the same partition. TODO: Give an error. We kinda need this feature.

            if (Directory.Exists(FileIO.GameTargetDirectory))  // Clean up so deleted mods don't remain.
            {
                Directory.Delete(FileIO.GameTargetDirectory, true);
            }


            IEnumerable<Mod> activeMods = Mods.Where(plugin => plugin.IsActive).Reverse(); // When creating hard links through the Win32 API if a file exists it won't be overwrote, so reverse the mods to be hard linked and then do the source game directory last.
            foreach (Mod mod in activeMods) // Hardlink the mods. TODO: Check if root mod.
            {
                FileIO.CreateHardLinks(mod.SourceDirectory, FileIO.GameDataTargetDirectory);
            }
            FileIO.CreateHardLinks(FileIO.GameSourceDirectory, FileIO.GameTargetDirectory); // Hardlink the vanilla game.

            string path = Path.Combine(FileIO.GameTargetDirectory, "Skyrim.ccc"); // Skyrim.ccc overrides plugins.txt. TODO: Have a collection of files to delete?
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ProcessStartInfo info = new(Path.Combine(FileIO.GameTargetDirectory, "SkyrimSE.exe"));
            info.WorkingDirectory = FileIO.GameTargetDirectory;
            Process.Start(info);
        }

        async void InstallModFileDialogue()
        {
            FileDialogFilter supportedExtensions = new();
            supportedExtensions.Extensions = new List<string> { "7z", "zip", "rar" };

            OpenFileDialog dialogue = new OpenFileDialog
            {
                Title = "Pick Compressed Archive",
                // AllowMultiple = true, // I don't think I can trust users to not hang their computer with this feature.
                Filters = new List<FileDialogFilter> { supportedExtensions }
            };

            Window mainWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow; // Should always exist and implement that interface as we're only supporting Windows, and maybe Linux.

            string[]? files = await dialogue.ShowAsync(mainWindow);
            if (files == null) { return; }

            foreach (string file in files) // TODO: Support multiple file selection, but extract one at a time. This'll work best with a progress bar denoting that some are 0.
            {
                InstallMod(file);
            }
        }

        public static async void InstallMod(string sourceArchive)
        {
            if (!File.Exists(sourceArchive)) // Shouldn't occur with picking through file dialogue, but just in case.
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

            FileIO.LoadModsAndSaveChanges(Mods, Plugins);
            FileIO.LoadPluginsAndSaveChanges(Plugins, Mods);
        }
    }
}
