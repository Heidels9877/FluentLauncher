using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentLauncher.Core;
using FluentLauncher.Models;

namespace FluentLauncher.ViewModels
{
    public partial class CreateInstanceViewModel : ObservableObject
    {
        private readonly InstanceManager _instanceManager;
        private readonly LauncherService _launcherService;
        private readonly ModpackService _modpackService;

        [ObservableProperty]
        private string _instanceName = "New Instance";

        [ObservableProperty]
        private string _selectedVersion = "";

        [ObservableProperty]
        private string _iconPath = "";

        public ObservableCollection<string> MinecraftVersions { get; } = new();

        public ObservableCollection<ModLoaderType> ModLoaders { get; } = new(Enum.GetValues<ModLoaderType>());

        [ObservableProperty]
        private ModLoaderType _selectedModLoader = ModLoaderType.Vanilla;

        [ObservableProperty]
        private bool _isLoadingVersions = true;

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        private string _importStatus = "";

        [ObservableProperty]
        private int _importProgress;

        public CreateInstanceViewModel(InstanceManager instanceManager, LauncherService launcherService, ModpackService modpackService)
        {
            _instanceManager = instanceManager;
            _launcherService = launcherService;
            _modpackService = modpackService;
            _ = LoadVersionsAsync();
        }

        [RelayCommand]
        public async Task ImportModpack()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Modrinth Modpack|*.mrpack"
            };

            if (dialog.ShowDialog() == true)
            {
                IsImporting = true;
                ImportStatus = "Starting import...";
                ImportProgress = 0;

                try
                {
                    await Task.Run(async () =>
                    {
                        await _modpackService.ImportModrinthPackAsync(dialog.FileName, (status, progress) => {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                                ImportStatus = status;
                                ImportProgress = progress;
                            });
                        });
                    });
                    
                    var mainWindow = Application.Current.MainWindow as Views.MainWindow;
                    mainWindow?.RootNavigation.Navigate(typeof(Views.InstancesPage));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Import failed: " + ex.Message);
                }
                finally
                {
                    IsImporting = false;
                }
            }
        }

        [RelayCommand]
        public async Task ImportFLPack()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "FLPack (*.flpack)|*.flpack",
                Title = "Select FLPack"
            };

            if (dialog.ShowDialog() == true)
            {
                IsImporting = true;
                ImportStatus = "Starting import...";
                ImportProgress = 0;

                try
                {
                    await Task.Run(async () =>
                    {
                        await _modpackService.ImportFLPackAsync(dialog.FileName, (status, progress) =>
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                ImportStatus = status;
                                ImportProgress = progress;
                            });
                        });
                    });

                    System.Windows.MessageBox.Show("Import successful!", "Import FLPack", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    
                    var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
                    mainWindow?.RootNavigation.Navigate(typeof(Views.InstancesPage));
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Import failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsImporting = false;
                }
            }
        }

        [RelayCommand]
        public void SelectIcon()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            };
            if (dialog.ShowDialog() == true)
            {
                IconPath = dialog.FileName;
            }
        }

        private async Task LoadVersionsAsync()
        {
            IsLoadingVersions = true;
            try
            {
                var releases = await _launcherService.GetReleaseVersionsAsync();
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var r in releases)
                    {
                        MinecraftVersions.Add(r);
                    }
                    if (MinecraftVersions.Count > 0)
                        SelectedVersion = MinecraftVersions[0];
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    MessageBox.Show("Failed to load versions: " + ex.Message));
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    IsLoadingVersions = false);
            }
        }

        [RelayCommand]
        public void Create()
        {
            if (string.IsNullOrWhiteSpace(InstanceName) || string.IsNullOrWhiteSpace(SelectedVersion))
                return;

            var instance = new Instance
            {
                Name = InstanceName,
                MinecraftVersion = SelectedVersion,
                ModLoader = SelectedModLoader,
                IconPath = IconPath
            };

            _instanceManager.AddInstance(instance);
            
            _instanceManager.SelectedInstance = instance;
            var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
            mainWindow?.RootNavigation.Navigate(typeof(Views.InstanceDetailsPage));
        }

        [RelayCommand]
        public void Cancel()
        {
            var mainWindow = Application.Current.MainWindow as Views.MainWindow;
            mainWindow?.RootNavigation.Navigate(typeof(Views.InstancesPage));
        }
    }
}
