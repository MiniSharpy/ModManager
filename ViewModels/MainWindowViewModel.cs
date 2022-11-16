using ReactiveUI;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System;

namespace ModManager.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _Content;

        public ViewModelBase Content
        {
            get { return _Content; }
            set { this.RaiseAndSetIfChanged(ref _Content, value); }
        }

        public MainWindowViewModel()
        {
            if (FileIO.GameDataSourceDirectory == null || FileIO.PluginOrderFile == null)
            {
                SetupViewModel vm = new();

                vm.Accept
                .Subscribe(model =>
                {
                    if (model != null)
                    {
                        FileIO.PluginOrderFile = model[0];
                        FileIO.GameSourceDirectory = model[1];
                        FileIO.SaveGeneralConfig();
                    }

                    Content = new ModManagerViewModel();
                });

                Content = vm;
            }
            else
            {
                Content = new ModManagerViewModel();
            }
        }
    }
}
