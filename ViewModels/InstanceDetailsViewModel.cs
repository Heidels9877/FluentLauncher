using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentLauncher.Core;
using FluentLauncher.Models;

namespace FluentLauncher.ViewModels
{
    public partial class InstanceDetailsViewModel : ObservableObject
    {
        private readonly LauncherService _launcherService;
        private readonly AccountManager _accountManager;
        private readonly ModrinthApiService _modrinthService;
        private readonly InstanceManager _instanceManager;
        private readonly ModpackService _modpackService;

        [ObservableProperty]
        private Instance _currentInstance;

        [ObservableProperty]
        private bool _isWorking;

        [ObservableProperty]
        private string _workStatus = "";

        [ObservableProperty]
        private int _workProgress = 0;

        // Mod Browser Properties
        [ObservableProperty]
        private string _modSearchQuery = "";
        
        private string _addFilterMode = "mod";
        public string AddFilterMode
        {
            get => _addFilterMode;
            set
            {
                if (SetProperty(ref _addFilterMode, value))
                {
                    _ = SearchModsAsync();
                }
            }
        }

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    if (value == 1 && ModSearchResults.Count == 0)
                    {
                        _ = SearchModsAsync();
                    }
                }
            }
        }

        private string _addSortMode = "relevance";
        public string AddSortMode
        {
            get => _addSortMode;
            set
            {
                if (SetProperty(ref _addSortMode, value))
                {
                    _ = SearchModsAsync();
                }
            }
        }

        [ObservableProperty]
        private string _installedFilterMode = "mod";

        [ObservableProperty]
        private string _instanceLogs = "";

        [ObservableProperty]
        private ModrinthFullProject _selectedMod;

        [ObservableProperty]
        private bool _isModDetailsOpen;

        public class ModMetadata
        {
            public string ProjectId { get; set; }
            public string Title { get; set; }
            public string IconUrl { get; set; }
        }

        public class InstalledFile
        {
            public string FileName { get; set; }
            public string ProjectId { get; set; }
            public string Title { get; set; }
            public string IconUrl { get; set; }
            public string DisplayName => !string.IsNullOrEmpty(Title) ? Title : FileName;
            public bool HasIcon => !string.IsNullOrEmpty(IconUrl);
            public bool HasNoIcon => string.IsNullOrEmpty(IconUrl);
        }

        public ObservableCollection<ModrinthProject> ModSearchResults { get; } = new();
        public ObservableCollection<InstalledFile> InstalledFiles { get; } = new();

        public InstanceDetailsViewModel(LauncherService launcherService, AccountManager accountManager, ModrinthApiService modrinthService, InstanceManager instanceManager, ModpackService modpackService)
        {
            _launcherService = launcherService;
            _accountManager = accountManager;
            _modrinthService = modrinthService;
            _instanceManager = instanceManager;
            _modpackService = modpackService;
        }

        public void OnNavigatedTo()
        {
            if (_instanceManager.SelectedInstance != null)
            {
                CurrentInstance = _instanceManager.SelectedInstance;
                LoadInstalledFiles();
                _ = SearchModsAsync();

                var settings = Core.AppSettings.Load();
                if (CurrentInstance.IsRunning && settings.OpenLogsOnLaunch)
                {
                    SelectedTabIndex = 3; // Navigate to Logs tab
                }
            }
        }

        public void OnNavigatedFrom() { }

        public void SetInstance(Instance instance)
        {
            CurrentInstance = instance;
            LoadInstalledFiles();
            _ = SearchModsAsync();
        }

        partial void OnInstalledFilterModeChanged(string value)
        {
            LoadInstalledFiles();
        }

        [RelayCommand]
        public void LoadInstalledFiles()
        {
            InstalledFiles.Clear();
            if (CurrentInstance == null) return;

            string folderName = InstalledFilterMode switch {
                "resourcepack" => "resourcepacks",
                "shader" => "shaderpacks",
                _ => "mods"
            };

            string dir = Path.Combine(CurrentInstance.InstancePath, folderName);
            if (Directory.Exists(dir))
            {
                var metadataPath = Path.Combine(dir, "metadata.json");
                var metadata = new System.Collections.Generic.Dictionary<string, ModMetadata>();
                if (File.Exists(metadataPath))
                {
                    try { metadata = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ModMetadata>>(File.ReadAllText(metadataPath)); } catch { }
                }

                bool missingMetadata = false;
                foreach(var file in Directory.GetFiles(dir))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName == "metadata.json") continue;
                    
                    if (!metadata.TryGetValue(fileName, out ModMetadata meta) || string.IsNullOrEmpty(meta?.IconUrl))
                    {
                        missingMetadata = true;
                    }

                    InstalledFiles.Add(new InstalledFile { 
                        FileName = fileName, 
                        IconUrl = meta?.IconUrl,
                        ProjectId = meta?.ProjectId,
                        Title = meta?.Title
                    });
                }

                if (missingMetadata)
                {
                    _ = FetchMissingIconsAsync(dir, metadataPath, metadata);
                }
            }
            UpdateSearchInstalledStatus();
        }

        private async Task FetchMissingIconsAsync(string dir, string metadataPath, System.Collections.Generic.Dictionary<string, ModMetadata> metadata)
        {
            bool updated = false;
            foreach (var installedFile in InstalledFiles)
            {
                if (installedFile.HasNoIcon)
                {
                    try
                    {
                        string filePath = Path.Combine(dir, installedFile.FileName);
                        string sha1Str = "";
                        using (var stream = File.OpenRead(filePath))
                        using (var sha1 = System.Security.Cryptography.SHA1.Create())
                        {
                            var hash = await sha1.ComputeHashAsync(stream);
                            sha1Str = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }

                        var project = await _modrinthService.GetProjectFromHashAsync(sha1Str);
                        if (project != null)
                        {
                            installedFile.IconUrl = project.IconUrl;
                            installedFile.ProjectId = project.ProjectId;
                            installedFile.Title = project.Title;
                            OnPropertyChanged(nameof(InstalledFiles));
                            
                            metadata[installedFile.FileName] = new ModMetadata 
                            { 
                                ProjectId = project.ProjectId,
                                Title = project.Title,
                                IconUrl = project.IconUrl
                            };
                            updated = true;
                        }
                    }
                    catch { }
                }
            }
            
            if (updated)
            {
                try { File.WriteAllText(metadataPath, System.Text.Json.JsonSerializer.Serialize(metadata)); } catch { }
                Application.Current.Dispatcher.Invoke(() => {
                    LoadInstalledFiles();
                    UpdateSearchInstalledStatus();
                });
            }
        }

        private void UpdateSearchInstalledStatus()
        {
            if (CurrentInstance == null) return;

            string folderName = AddFilterMode switch {
                "resourcepack" => "resourcepacks",
                "shader" => "shaderpacks",
                _ => "mods"
            };

            string dir = Path.Combine(CurrentInstance.InstancePath, folderName);
            var metadataPath = Path.Combine(dir, "metadata.json");
            var metadata = new System.Collections.Generic.Dictionary<string, ModMetadata>();
            if (File.Exists(metadataPath))
            {
                try { metadata = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ModMetadata>>(File.ReadAllText(metadataPath)); } catch { }
            }

            var fileNames = Directory.Exists(dir) ? Directory.GetFiles(dir).Select(Path.GetFileName).ToList() : new System.Collections.Generic.List<string>();

            foreach(var proj in ModSearchResults)
            {
                proj.IsInstalled = metadata.Values.Any(m => m != null && m.ProjectId == proj.ProjectId) ||
                                   fileNames.Any(f => f.IndexOf(proj.Title, StringComparison.OrdinalIgnoreCase) >= 0 || f.IndexOf(proj.ProjectId, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        [RelayCommand]
        public void DeleteInstalledFile(string fileName)
        {
            if (CurrentInstance == null || string.IsNullOrWhiteSpace(fileName)) return;
            string folderName = InstalledFilterMode switch {
                "resourcepack" => "resourcepacks",
                "shader" => "shaderpacks",
                _ => "mods"
            };
            string filePath = Path.Combine(CurrentInstance.InstancePath, folderName, fileName);
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
                LoadInstalledFiles();
            }
        }

        [RelayCommand]
        public void InstallJar()
        {
            if (CurrentInstance == null) return;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Jar Files (*.jar)|*.jar|All files (*.*)|*.*",
                Title = "Select Jar File"
            };

            if (dialog.ShowDialog() == true)
            {
                string folderName = InstalledFilterMode switch {
                    "resourcepack" => "resourcepacks",
                    "shader" => "shaderpacks",
                    _ => "mods"
                };
                string destFolder = Path.Combine(CurrentInstance.InstancePath, folderName);
                if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

                string destPath = Path.Combine(destFolder, Path.GetFileName(dialog.FileName));
                try 
                {
                    File.Copy(dialog.FileName, destPath, true);
                    LoadInstalledFiles();
                    UpdateSearchInstalledStatus();
                    System.Windows.MessageBox.Show("File installed successfully!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                } 
                catch (Exception ex) 
                { 
                    System.Windows.MessageBox.Show($"Failed to install file: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public async Task InstallGameFilesAsync()
        {
            if (CurrentInstance == null || IsWorking) return;
            if (_accountManager.CurrentAccount == null)
            {
                MessageBox.Show("Please select an account in Settings/Accounts first.");
                return;
            }
            try
            {
                IsWorking = true;
                WorkStatus = "Downloading game files...";
                WorkProgress = 0;

                await _launcherService.InstallOnlyAsync(CurrentInstance, _accountManager.CurrentSession,
                    (s, e) => {
                        Application.Current.Dispatcher.Invoke(() => {
                            WorkStatus = $"[{e.EventType}] {e.Name}";
                            WorkProgress = e.TotalTasks > 0 ? (e.ProgressedTasks * 100 / e.TotalTasks) : 0;
                        });
                    },
                    null);
                    
                WorkStatus = "Download complete!";
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to download files: " + ex.Message);
            }
            finally
            {
                IsWorking = false;
                WorkStatus = "";
                WorkProgress = 0;
            }
        }

        [RelayCommand]
        public void PlayInstance()
        {
            if (CurrentInstance == null) return;

            if (CurrentInstance.IsRunning)
            {
                try { CurrentInstance.RunningProcess?.Kill(); } catch { }
                return;
            }

            if (_accountManager.CurrentAccount == null)
            {
                MessageBox.Show("Please select an account in Settings/Accounts first.");
                return;
            }
            
            _ = _launcherService.StartGameProcessAsync(CurrentInstance, _accountManager.CurrentSession);
            
            var settings = Core.AppSettings.Load();
            if (settings.OpenLogsOnLaunch)
            {
                SelectedTabIndex = 3; // Navigate to Logs tab
            }
        }

        [RelayCommand]
        public void DeleteInstance()
        {
            if (CurrentInstance != null)
            {
                var result = MessageBox.Show($"Are you sure you want to delete {CurrentInstance.Name}?", "Delete Instance", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _instanceManager.RemoveInstance(CurrentInstance);
                    var mainWindow = Application.Current.MainWindow as Views.MainWindow;
                    mainWindow?.RootNavigation.GoBack();
                }
            }
        }
        
        [RelayCommand]
        public void OpenFolder()
        {
            if (CurrentInstance != null && Directory.Exists(CurrentInstance.InstancePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", CurrentInstance.InstancePath);
            }
        }

        [RelayCommand]
        public void BrowseJava()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Java Executable (javaw.exe)"
            };

            if (dialog.ShowDialog() == true)
            {
                CurrentInstance.JavaPath = dialog.FileName;
            }
        }

        [RelayCommand]
        public void ChangeInstanceIcon()
        {
            if (CurrentInstance == null) return;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
                Title = "Select Instance Icon"
            };

            if (dialog.ShowDialog() == true)
            {
                CurrentInstance.IconPath = dialog.FileName;
                _instanceManager.SaveInstances();
            }
        }

        [RelayCommand]
        public async Task ExportFLPackAsync()
        {
            if (CurrentInstance == null) return;
            
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "FLPack (*.flpack)|*.flpack",
                Title = "Export FLPack",
                FileName = $"{CurrentInstance.Name}.flpack"
            };

            if (dialog.ShowDialog() == true)
            {
                IsWorking = true;
                WorkStatus = "Exporting FLPack...";
                WorkProgress = 0;
                
                try
                {
                    await _modpackService.ExportFLPackAsync(CurrentInstance, dialog.FileName, (status, progress) => 
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            WorkStatus = status;
                            WorkProgress = progress;
                        });
                    });
                    
                    System.Windows.MessageBox.Show("Export successful!", "Export FLPack", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsWorking = false;
                }
            }
        }

        private int _currentSearchOffset = 0;
        private bool _isLoadingMore = false;

        [RelayCommand]
        public async Task SearchModsAsync()
        {
            if (CurrentInstance == null) return;
            IsWorking = true;
            WorkStatus = "Searching...";
            _currentSearchOffset = 0;

            try
            {
                var results = await _modrinthService.SearchProjectsAsync(ModSearchQuery ?? "", AddFilterMode, CurrentInstance.MinecraftVersion, CurrentInstance.ModLoader, AddSortMode, _currentSearchOffset);
                ModSearchResults.Clear();
                foreach (var res in results)
                {
                    ModSearchResults.Add(res);
                }
                UpdateSearchInstalledStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Search failed: " + ex.Message);
            }
            finally
            {
                IsWorking = false;
                WorkStatus = "";
            }
        }

        [RelayCommand]
        public async Task LoadMoreSearchModsAsync()
        {
            if (CurrentInstance == null || _isLoadingMore) return;
            
            _isLoadingMore = true;
            IsWorking = true;
            WorkStatus = "Loading more...";
            _currentSearchOffset += 20;

            try
            {
                var results = await _modrinthService.SearchProjectsAsync(ModSearchQuery ?? "", AddFilterMode, CurrentInstance.MinecraftVersion, CurrentInstance.ModLoader, AddSortMode, _currentSearchOffset);
                foreach (var res in results)
                {
                    ModSearchResults.Add(res);
                }
                UpdateSearchInstalledStatus();
            }
            catch (Exception ex)
            {
                // Optionally handle fail silently
            }
            finally
            {
                _isLoadingMore = false;
                IsWorking = false;
                WorkStatus = "";
            }
        }

        [RelayCommand]
        public async Task InstallModAsync(ModrinthProject project)
        {
            if (CurrentInstance == null || project == null) return;
            IsWorking = true;
            WorkStatus = $"Fetching {project.Title}...";
            WorkProgress = 0;
            
            try
            {
                var version = await _modrinthService.GetLatestVersionAsync(project.ProjectId, CurrentInstance.MinecraftVersion, CurrentInstance.ModLoader, AddFilterMode);
                if (version != null && version.Files.Count > 0)
                {
                    var file = version.Files[0];
                    string folderName = AddFilterMode switch {
                        "resourcepack" => "resourcepacks",
                        "shader" => "shaderpacks",
                        _ => "mods"
                    };
                    string targetDir = Path.Combine(CurrentInstance.InstancePath, folderName);
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    
                    string dest = Path.Combine(targetDir, file.Filename);
                    
                    WorkStatus = $"Downloading {file.Filename}...";
                    var progress = new Progress<int>(p => {
                        Application.Current.Dispatcher.Invoke(() => WorkProgress = p);
                    });
                    
                    await _modrinthService.DownloadFileAsync(file.Url, dest, progress);
                    
                    // Save metadata for icon
                    var metadataPath = Path.Combine(targetDir, "metadata.json");
                    var metadata = new System.Collections.Generic.Dictionary<string, ModMetadata>();
                    if (File.Exists(metadataPath))
                    {
                        try { metadata = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ModMetadata>>(File.ReadAllText(metadataPath)); } catch { }
                    }
                    if (metadata != null)
                    {
                        metadata[file.Filename] = new ModMetadata 
                        {
                            ProjectId = project.ProjectId,
                            Title = project.Title,
                            IconUrl = project.IconUrl
                        };
                        File.WriteAllText(metadataPath, System.Text.Json.JsonSerializer.Serialize(metadata));
                    }

                    // Update installed state if we downloaded successfully
                    project.IsInstalled = true;
                    // Also refresh installed list if we are viewing the same mode
                    if (InstalledFilterMode == AddFilterMode) LoadInstalledFiles();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Install failed: " + ex.Message);
            }
            finally
            {
                IsWorking = false;
                WorkStatus = "";
                WorkProgress = 0;
            }
        }
        
        [RelayCommand]
        public void GoBack()
        {
            _instanceManager.SaveInstances(); // Save any settings changes
            var mainWindow = Application.Current.MainWindow as Views.MainWindow;
            mainWindow?.RootNavigation.GoBack();
        }

        [RelayCommand]
        public async Task OpenModDetailsAsync(ModrinthProject project)
        {
            if (project == null) return;
            
            IsWorking = true;
            WorkStatus = "Loading mod details...";
            
            try
            {
                var fullProject = await _modrinthService.GetFullProjectAsync(project.ProjectId);
                if (fullProject != null)
                {
                    fullProject.IsInstalled = project.IsInstalled;
                    SelectedMod = fullProject;
                    IsModDetailsOpen = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load details: " + ex.Message);
            }
            finally
            {
                IsWorking = false;
                WorkStatus = "";
            }
        }

        [RelayCommand]
        public void CloseModDetails()
        {
            IsModDetailsOpen = false;
            SelectedMod = null;
        }
        [ObservableProperty]
        private bool _isExportDialogOpen;

        [ObservableProperty]
        private string _exportPackName = "";

        public ObservableCollection<ExportFileItem> ExportFiles { get; } = new();

        [RelayCommand]
        public void OpenExportDialog()
        {
            if (CurrentInstance == null) return;
            ExportPackName = CurrentInstance.Name;
            ExportFiles.Clear();
            string instancePath = Path.Combine(_instanceManager.InstancesDirectory, CurrentInstance.Id);
            if (Directory.Exists(instancePath))
            {
                var dirInfo = new DirectoryInfo(instancePath);
                
                foreach (var dir in dirInfo.GetDirectories())
                {
                    bool isSelected = dir.Name == "mods" || dir.Name == "config" || dir.Name == "resourcepacks" || dir.Name == "saves";
                    ExportFiles.Add(new ExportFileItem { Name = dir.Name, FullPath = dir.FullName, IsDirectory = true, IsSelected = isSelected });
                }
                
                foreach (var file in dirInfo.GetFiles())
                {
                    bool isSelected = file.Name == "instance.json" || file.Name == "icon.png";
                    ExportFiles.Add(new ExportFileItem { Name = file.Name, FullPath = file.FullName, IsDirectory = false, IsSelected = isSelected });
                }
            }
            IsExportDialogOpen = true;
        }

        [RelayCommand]
        public void CloseExportDialog()
        {
            IsExportDialogOpen = false;
        }

        [RelayCommand]
        public async Task ConfirmExportAsync()
        {
            if (CurrentInstance == null || string.IsNullOrWhiteSpace(ExportPackName)) return;

            var sfd = new SaveFileDialog
            {
                Filter = "FluentLauncher Pack (*.flpack)|*.flpack",
                FileName = ExportPackName + ".flpack",
                Title = "Export Modpack"
            };

            if (sfd.ShowDialog() == true)
            {
                IsWorking = true;
                WorkStatus = "Exporting pack...";
                IsExportDialogOpen = false;

                try
                {
                    await Task.Run(() =>
                    {
                        if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
                        
                        using (var archive = ZipFile.Open(sfd.FileName, ZipArchiveMode.Create))
                        {
                            foreach (var item in ExportFiles)
                            {
                                if (!item.IsSelected) continue;

                                if (item.IsDirectory)
                                {
                                    var files = Directory.GetFiles(item.FullPath, "*.*", SearchOption.AllDirectories);
                                    foreach (var file in files)
                                    {
                                        string instancePath = Path.Combine(_instanceManager.InstancesDirectory, CurrentInstance.Id);
                                        var relPath = file.Substring(instancePath.Length).TrimStart('\\', '/');
                                        archive.CreateEntryFromFile(file, relPath, CompressionLevel.Optimal);
                                    }
                                }
                                else
                                {
                                    archive.CreateEntryFromFile(item.FullPath, item.Name, CompressionLevel.Optimal);
                                }
                            }
                        }
                    });
                    
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        if (Application.Current.MainWindow is Views.MainWindow mw)
                        {
                            System.Windows.MessageBox.Show("Modpack exported successfully.", "Export Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        if (Application.Current.MainWindow is Views.MainWindow mw)
                        {
                            System.Windows.MessageBox.Show(ex.Message, "Export Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        }
                    });
                }
                finally
                {
                    IsWorking = false;
                }
            }
        }
    }
}
