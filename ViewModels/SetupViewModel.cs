using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace ModManager.ViewModels
{
    public class SetupViewModel : ViewModelBase
    {
        private string? _PluginOrderFile;

        public string? PluginOrderFile
        { 
            get => _PluginOrderFile; 
            set => this.RaiseAndSetIfChanged(ref _PluginOrderFile, value); 
        } 

        private string? _GameSourceDirectory;

        public string? GameSourceDirectory
        {
            get { return _GameSourceDirectory; }
            set { this.RaiseAndSetIfChanged(ref _GameSourceDirectory, value); }
        }

        public ReactiveCommand<Unit, string[]> Accept { get; }

        public SetupViewModel()
        {
            // With different version of game, even with AE, allow user to pick plugins folder defaulting to local appddata.
            Accept = ReactiveCommand.Create(() => new string[2] { PluginOrderFile!, GameSourceDirectory! });
        }

        async void SetGameSourceDirectory()
        {
            OpenFolderDialog dialogue = new OpenFolderDialog
            {
                Title = "Pick Game Directory",
            };
            Window mainWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow; // Should always exist and implement that interface as we're only supporting Windows, and maybe Linux.

            string? selectedDirectory = await dialogue.ShowAsync(mainWindow);
            if (selectedDirectory != null && File.Exists(Path.Combine(selectedDirectory, "SkyrimSE.exe")))
            {
                GameSourceDirectory = selectedDirectory;
            }
        }

        async void SetPluginOrderDirectory() // This is a bit of an awkward one. It's only really Linux and GOG that'll need to set this. There must be a better way. Could have a drop down selection for game that determines where to check, and only on Linux allow setting.
        {
            string? defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); // TODO: Set a default for Linux.

            OpenFolderDialog dialogue = new OpenFolderDialog
            {
                Title = "Pick Game Directory",
                Directory = defaultPath

            };
            Window mainWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current!.ApplicationLifetime!).MainWindow; // Should always exist and implement that interface as we're only supporting Windows, and maybe Linux.

            string? result = await dialogue.ShowAsync(mainWindow);
            if (result != null)
            {
                PluginOrderFile = Path.Combine(result, "plugins.txt");
            }
        }
    }
}
