using Avalonia.Threading;
using ModManager.Models;
using ReactiveUI;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace ModManager.ViewModels
{
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
            FileIO.LoadPlugins(Plugins);
            FileIO.LoadMods(Mods);
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
            foreach (string modDirectory in Directory.GetDirectories(FileIO.ModsDirectory)) // Hardlink the mods. TODO: Load in order of mod list, only adding active. TODO: Check if root mod.
            {
                FileIO.CreateHardLinks(modDirectory, FileIO.GameDataTargetDirectory);
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

        public static string? TestPath { get; set; }
        public static void InstallMod()
        {
            string? path = TestPath;
            if (!File.Exists(path)) // Shouldn't occur with picking through file dialog, but just in case.
            {
                // TODO: Report error.
                return;
            }

            using Stream stream = File.OpenRead(path);
            if (SevenZipArchive.IsSevenZipFile(stream))
            {
                using IArchive archive = ArchiveFactory.Open(stream);

                IReader reader = archive.ExtractAllEntries(); // Should not need disposing.
                ExtractToDirectory(reader, path);
            }
            else
            {
                using IReader reader = ReaderFactory.Open(stream);
                ExtractToDirectory(reader, path);
            }


            FileIO.LoadMods(Mods);

            void ExtractToDirectory(IReader reader, string path)
            {
                string modName = Path.GetFileName(path);
                string modPath = Path.Combine(FileIO.ModsDirectory, modName);
                Directory.CreateDirectory(modPath);

                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Console.WriteLine(reader.Entry.Key);
                        reader.WriteEntryToDirectory(modPath, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
        }
    }
}
