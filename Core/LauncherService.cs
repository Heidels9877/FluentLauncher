using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using CmlLib.Core;
using CmlLib.Core.Version;

namespace FluentLauncher.Core
{
    public class LauncherService
    {
        public MinecraftLauncher Launcher { get; }
        public MinecraftPath MinecraftPath { get; }

        public LauncherService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            MinecraftPath = new MinecraftPath(Path.Combine(appData, ".fluentlauncher"));
            Launcher = new MinecraftLauncher(MinecraftPath);
        }

        private MinecraftPath GetConfiguredMinecraftPath(string instancePath)
        {
            var path = new MinecraftPath(instancePath);
            var settings = AppSettings.Load();
            
            if (!string.IsNullOrWhiteSpace(settings.ExistingMinecraftPath) && Directory.Exists(settings.ExistingMinecraftPath))
            {
                var globalPath = new MinecraftPath(settings.ExistingMinecraftPath);
                path.Library = globalPath.Library;
                path.Assets = globalPath.Assets;
                path.Versions = globalPath.Versions;
                path.Runtime = globalPath.Runtime;
            }
            
            return path;
        }

        private void ValidateOfflineMode(bool isOffline)
        {
            var settings = AppSettings.Load();
            
            if (isOffline)
            {
                bool hasExistingPath = !string.IsNullOrWhiteSpace(settings.ExistingMinecraftPath) && Directory.Exists(settings.ExistingMinecraftPath);
                if (!hasExistingPath)
                {
                    throw new Exception("Offline accounts cannot download files from official servers. Please provide an Existing Minecraft Path in Settings.");
                }
            }
        }

        public async Task<List<string>> GetReleaseVersionsAsync()
        {
            var versionsObj = await Launcher.GetAllVersionsAsync();
            var releases = new List<string>();
            foreach (var v in versionsObj)
            {
                var type = v.GetType().GetProperty("Type")?.GetValue(v)?.ToString();
                if (type == null) type = v.GetType().GetProperty("MType")?.GetValue(v)?.ToString();
                
                if (type == "release")
                {
                    var name = v.GetType().GetProperty("Name")?.GetValue(v)?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        releases.Add(name);
                    }
                }
            }
            return releases;
        }

        public async Task InstallOnlyAsync(FluentLauncher.Models.Instance instanceInfo, bool isOffline,
            System.EventHandler<CmlLib.Core.Installers.InstallerProgressChangedEventArgs> fileChanged = null,
            System.EventHandler<CmlLib.Core.ByteProgress> progressChanged = null)
        {
            ValidateOfflineMode(isOffline);
            if (isOffline)
            {
                throw new Exception("Offline accounts cannot install or download instances. You must use an online account to download files.");
            }

            var path = GetConfiguredMinecraftPath(instanceInfo.InstancePath);
            var launcher = new MinecraftLauncher(path);

            if (fileChanged != null) launcher.FileProgressChanged += fileChanged;
            if (progressChanged != null) launcher.ByteProgressChanged += progressChanged;

            string launchVersion = await SetupModLoaderAsync(instanceInfo, path, launcher, isOffline);
            
            // Just install, don't run
            await launcher.InstallAsync(launchVersion);
        }

        private async Task<string> SetupModLoaderAsync(FluentLauncher.Models.Instance instanceInfo, MinecraftPath path, MinecraftLauncher launcher, bool isOffline)
        {
            string launchVersion = instanceInfo.MinecraftVersion;

            if (isOffline && instanceInfo.ModLoader != FluentLauncher.Models.ModLoaderType.Vanilla)
            {
                // Only allow modloader download if the vanilla game is already present
                string vanillaDir = System.IO.Path.Combine(path.Versions, instanceInfo.MinecraftVersion);
                string vanillaJson = System.IO.Path.Combine(vanillaDir, instanceInfo.MinecraftVersion + ".json");
                string vanillaJar = System.IO.Path.Combine(vanillaDir, instanceInfo.MinecraftVersion + ".jar");
                
                if (!System.IO.File.Exists(vanillaJson) || !System.IO.File.Exists(vanillaJar))
                {
                    throw new Exception($"Cannot install {instanceInfo.ModLoader} in offline mode: The base game files ({instanceInfo.MinecraftVersion}) are missing. You must already have the base game installed in your Existing Minecraft Path.");
                }
            }

            if (instanceInfo.ModLoader == FluentLauncher.Models.ModLoaderType.Forge)
            {
                var forge = new CmlLib.Core.Installer.Forge.ForgeInstaller(launcher);
                launchVersion = await forge.Install(instanceInfo.MinecraftVersion);
            }
            else if (instanceInfo.ModLoader == FluentLauncher.Models.ModLoaderType.Fabric)
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var loaderMetaJson = await httpClient.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader");
                using var doc = System.Text.Json.JsonDocument.Parse(loaderMetaJson);
                string loaderVersion = doc.RootElement[0].GetProperty("version").GetString();

                string fabricVersionName = $"fabric-loader-{loaderVersion}-{instanceInfo.MinecraftVersion}";
                string versionDir = System.IO.Path.Combine(path.Versions, fabricVersionName);
                if (!System.IO.Directory.Exists(versionDir))
                {
                    System.IO.Directory.CreateDirectory(versionDir);
                    string profileJson = await httpClient.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{instanceInfo.MinecraftVersion}/{loaderVersion}/profile/json");
                    await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(versionDir, fabricVersionName + ".json"), profileJson);
                }
                
                launchVersion = fabricVersionName;
            }
            return launchVersion;
        }

        public async Task<System.Diagnostics.Process> LaunchAsync(FluentLauncher.Models.Instance instanceInfo, CmlLib.Core.Auth.MSession session, bool isOffline,
            System.EventHandler<CmlLib.Core.Installers.InstallerProgressChangedEventArgs> fileChanged = null,
            System.EventHandler<CmlLib.Core.ByteProgress> progressChanged = null)
        {
            ValidateOfflineMode(isOffline);

            var path = GetConfiguredMinecraftPath(instanceInfo.InstancePath);
            var launcher = new MinecraftLauncher(path);

            if (fileChanged != null) launcher.FileProgressChanged += fileChanged;
            if (progressChanged != null) launcher.ByteProgressChanged += progressChanged;

            string launchVersion = await SetupModLoaderAsync(instanceInfo, path, launcher, isOffline);

            var launchOption = new CmlLib.Core.ProcessBuilder.MLaunchOption
            {
                Session = session,
                MaximumRamMb = instanceInfo.AllocatedRam > 0 ? instanceInfo.AllocatedRam : 4096
            };

            if (!string.IsNullOrWhiteSpace(instanceInfo.JavaPath))
            {
                launchOption.JavaPath = instanceInfo.JavaPath;
            }

            System.Diagnostics.Process process;
            if (isOffline)
            {
                // Strictly build process only - no downloads allowed
                process = await launcher.BuildProcessAsync(launchVersion, launchOption);
            }
            else
            {
                // Allow downloading missing files for online accounts
                process = await launcher.CreateProcessAsync(launchVersion, launchOption);
            }
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            
            process.Start();
            return process;
        }

        public async Task StartGameProcessAsync(FluentLauncher.Models.Instance instance, CmlLib.Core.Auth.MSession session, bool isOffline)
        {
            if (instance.IsRunning) return;

            instance.IsRunning = true;
            instance.LaunchStatus = "Preparing to launch...";
            instance.LaunchProgress = 0;
            instance.InstanceLogs = "";

            try
            {
                var process = await LaunchAsync(instance, session, isOffline,
                    (s, e) => {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                            instance.LaunchStatus = $"[{e.EventType}] {e.Name}";
                            instance.LaunchProgress = e.TotalTasks > 0 ? (e.ProgressedTasks * 100 / e.TotalTasks) : 0;
                        });
                    },
                    (s, e) => {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                            instance.LaunchProgress = e.TotalBytes > 0 ? (int)(e.ProgressedBytes * 100 / e.TotalBytes) : 0;
                        });
                    });

                instance.RunningProcess = process;
                instance.LaunchStatus = "Running...";
                instance.LaunchProgress = 0;

                process.OutputDataReceived += (s, e) => {
                    if (e.Data != null)
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { instance.InstanceLogs += e.Data + Environment.NewLine; });
                };
                process.ErrorDataReceived += (s, e) => {
                    if (e.Data != null)
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { instance.InstanceLogs += "[ERROR] " + e.Data + Environment.NewLine; });
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.Exited += (s, e) => {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                        instance.IsRunning = false;
                        instance.RunningProcess = null;
                        instance.LaunchStatus = "Exited";
                    });
                };
                process.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                instance.IsRunning = false;
                instance.LaunchStatus = $"Error: {ex.Message}";
                instance.InstanceLogs += $"[FATAL ERROR] {ex.Message}\n";
                
                if (ex.Message.Contains("copyright safety"))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        System.Windows.MessageBox.Show(ex.Message, "Offline Mode Restriction", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    });
                }
            }
        }
    }
}
