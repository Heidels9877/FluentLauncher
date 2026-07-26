using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentLauncher.Core;
using FluentLauncher.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Text.Json;

namespace FluentLauncher.ViewModels
{
    public partial class InstancesViewModel : ObservableObject
    {
        private readonly InstanceManager _instanceManager;
        private readonly LauncherService _launcherService;
        private readonly AccountManager _accountManager;

        public ObservableCollection<Instance> Instances { get; } = new();

        [ObservableProperty]
        private bool _hasInstances;

        [ObservableProperty]
        private bool _isEmpty = true;

        [ObservableProperty]
        private bool _isLaunching;

        [ObservableProperty]
        private string _launchStatus = "";

        [ObservableProperty]
        private int _launchProgress = 0;

        public InstancesViewModel(InstanceManager instanceManager, LauncherService launcherService, AccountManager accountManager)
        {
            _instanceManager = instanceManager;
            _launcherService = launcherService;
            _accountManager = accountManager;
            LoadInstances();

            if (!string.IsNullOrEmpty(App.StartupFlpackPath))
            {
                var path = App.StartupFlpackPath;
                App.StartupFlpackPath = null;
                Task.Run(() => ProcessImportAsync(path));
            }
        }

        private void LoadInstances()
        {
            Instances.Clear();
            foreach (var instance in _instanceManager.Instances)
            {
                Instances.Add(instance);
            }
            HasInstances = Instances.Count > 0;
            IsEmpty = !HasInstances;
        }

        [RelayCommand]
        public void CreateInstance()
        {
            if (System.Windows.Application.Current.MainWindow is Views.MainWindow mw)
            {
                mw.RootNavigation.Navigate(typeof(Views.CreateInstancePage));
            }
        }

        [RelayCommand]
        public async Task ImportFlpackAsync()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "FluentLauncher Pack (*.flpack)|*.flpack",
                Title = "Import Modpack"
            };

            if (ofd.ShowDialog() == true)
            {
                await ProcessImportAsync(ofd.FileName);
            }
        }

        public async Task ProcessImportAsync(string filePath)
        {
            try
            {
                string instanceId = Guid.NewGuid().ToString();
                string targetDir = Path.Combine(_instanceManager.InstancesDirectory, instanceId);
                Directory.CreateDirectory(targetDir);

                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(filePath, targetDir, true);
                });

                string jsonPath = Path.Combine(targetDir, "instance.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var importedInstance = JsonSerializer.Deserialize<Instance>(json);
                    
                    if (importedInstance != null)
                    {
                        importedInstance.Id = instanceId;
                        
                        string updatedJson = JsonSerializer.Serialize(importedInstance, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(jsonPath, updatedJson);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _instanceManager.LoadInstances();
                            if (System.Windows.Application.Current.MainWindow is Views.MainWindow mw)
                            {
                                System.Windows.MessageBox.Show("Modpack imported successfully.", "Import Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                            }
                        });
                    }
                }
                else
                {
                    Directory.Delete(targetDir, true);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (System.Windows.Application.Current.MainWindow is Views.MainWindow mw)
                        {
                            System.Windows.MessageBox.Show("Invalid FLPack. Missing instance.json.", "Import Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow is Views.MainWindow mw)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "Import Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                });
            }
        }

        [RelayCommand]
        public void PlayInstance(Instance instance)
        {
            if (instance == null) return;
            
            if (instance.IsRunning)
            {
                try { instance.RunningProcess?.Kill(); } catch { }
                return;
            }

            if (_accountManager.CurrentAccount == null)
            {
                System.Windows.MessageBox.Show("Please select an account in Settings/Accounts first.");
                return;
            }

            _ = _launcherService.StartGameProcessAsync(instance, _accountManager.CurrentSession, _accountManager.CurrentAccount.Type == FluentLauncher.Models.AccountType.Offline);
            
            var settings = Core.AppSettings.Load();
            if (settings.OpenLogsOnLaunch)
            {
                _instanceManager.SelectedInstance = instance;
                var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.RootNavigation.Navigate(typeof(Views.InstanceDetailsPage));
                    // Force navigation to Logs tab will be handled in InstanceDetailsViewModel
                }
            }
        }

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private Instance _editingInstance;

        [RelayCommand]
        public void EditInstance(Instance instance)
        {
            if (instance == null) return;
            _instanceManager.SelectedInstance = instance;
            var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
            mainWindow?.RootNavigation.Navigate(typeof(Views.InstanceDetailsPage));
        }

        [RelayCommand]
        public void SaveEdit()
        {
            if (EditingInstance != null)
            {
                _instanceManager.SaveInstances();
                LoadInstances();
            }
            IsEditing = false;
            EditingInstance = null;
        }

        [RelayCommand]
        public void CancelEdit()
        {
            _instanceManager.LoadInstances();
            LoadInstances();
            IsEditing = false;
            EditingInstance = null;
        }

        [RelayCommand]
        public void DeleteInstance(Instance instance)
        {
            var result = System.Windows.MessageBox.Show($"Are you sure you want to delete {instance.Name}?", "Delete Instance", System.Windows.MessageBoxButton.YesNo);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _instanceManager.RemoveInstance(instance);
                LoadInstances();
            }
        }

        [RelayCommand]
        public void SelectEditIcon()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
            };
            
            if (dialog.ShowDialog() == true && EditingInstance != null)
            {
                // We should copy the icon to instance dir or let the model hold it. 
                // Let's copy it here just like in InstanceManager
                string ext = System.IO.Path.GetExtension(dialog.FileName);
                string newIconPath = System.IO.Path.Combine(EditingInstance.InstancePath, "icon" + ext);
                if (dialog.FileName != newIconPath)
                {
                    System.IO.File.Copy(dialog.FileName, newIconPath, true);
                    EditingInstance.IconPath = newIconPath;
                }
            }
        }
    }
}
