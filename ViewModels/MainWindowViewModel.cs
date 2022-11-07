using Avalonia.Threading;
using ModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Text;

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
        }

        public static void RunGame()
        {
            string? gamePathPartition = Path.GetPathRoot(FileIO.GameDataPath);
            string? managerPathPartition = Path.GetPathRoot(Directory.GetCurrentDirectory());
            if (gamePathPartition != managerPathPartition) { return; } // Hardlinking is only supported on the same partition. TODO: Give an error. We kinda need this feature.

            Directory.Delete(FileIO.ModManagerGamePath, true); // Clean up so deleted mods don't remain.
            FileIO.CreateHardLinks(FileIO.GamePath);
            string path = FileIO.ModManagerGamePath + "Skyrim.ccc"; // Skyrim.ccc overides plugins.txt. TODO: Have a collection of files to delete?
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ProcessStartInfo info = new(FileIO.ModManagerGamePath + "SkyrimSE.exe");
            info.WorkingDirectory = FileIO.ModManagerGamePath;
            Process.Start(info);
        }
    }
}
