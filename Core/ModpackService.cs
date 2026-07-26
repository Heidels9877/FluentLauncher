using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentLauncher.Models;

namespace FluentLauncher.Core
{
    public class ModpackService
    {
        private readonly InstanceManager _instanceManager;
        
        public ModpackService(InstanceManager instanceManager)
        {
            _instanceManager = instanceManager;
        }

        public async Task ImportModrinthPackAsync(string zipPath, Action<string, int> progressCallback)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FluentLauncher_Import", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            try
            {
                progressCallback("Extracting Modpack archive...", 0);
                ZipFile.ExtractToDirectory(zipPath, tempDir, true);

                string indexJsonPath = Path.Combine(tempDir, "modrinth.index.json");
                if (!File.Exists(indexJsonPath))
                {
                    throw new Exception("Not a valid Modrinth modpack (modrinth.index.json missing).");
                }

                var indexJson = await File.ReadAllTextAsync(indexJsonPath);
                using var doc = JsonDocument.Parse(indexJson);
                var root = doc.RootElement;
                
                string name = root.GetProperty("name").GetString() ?? "Imported Modpack";
                
                string mcVersion = "1.20.4";
                ModLoaderType loader = ModLoaderType.Vanilla;
                
                if (root.TryGetProperty("dependencies", out var deps))
                {
                    if (deps.TryGetProperty("minecraft", out var mcDep))
                        mcVersion = mcDep.GetString();
                        
                    if (deps.TryGetProperty("fabric-loader", out _))
                        loader = ModLoaderType.Fabric;
                    else if (deps.TryGetProperty("forge", out _))
                        loader = ModLoaderType.Forge;
                }

                var instance = new Instance
                {
                    Name = name,
                    MinecraftVersion = mcVersion,
                    ModLoader = loader
                };
                
                _instanceManager.AddInstance(instance);
                
                if (root.TryGetProperty("files", out var filesArr))
                {
                    int total = filesArr.GetArrayLength();
                    int current = 0;
                    
                    using var httpClient = new HttpClient();
                    foreach (var file in filesArr.EnumerateArray())
                    {
                        current++;
                        string filePath = file.GetProperty("path").GetString();
                        string url = file.GetProperty("downloads")[0].GetString();
                        
                        progressCallback($"Downloading {Path.GetFileName(filePath)}...", current * 100 / total);
                        
                        string destPath = Path.Combine(instance.InstancePath, filePath);
                        string destDir = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                        
                        var bytes = await httpClient.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(destPath, bytes);
                    }
                }
                
                progressCallback("Applying overrides...", 100);
                string overridesDir = Path.Combine(tempDir, "overrides");
                if (Directory.Exists(overridesDir))
                {
                    CopyDirectory(overridesDir, instance.InstancePath);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        
        public async Task ExportFLPackAsync(Instance instance, string destinationZipPath, Action<string, int> progressCallback)
        {
            await Task.Run(() =>
            {
                progressCallback("Preparing FLPack export...", 0);
                
                string tempDir = Path.Combine(Path.GetTempPath(), "FluentLauncher_Export", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                
                try
                {
                    // 1. Copy instance files to tempDir/instance
                    string instanceTempDir = Path.Combine(tempDir, "instance");
                    if (Directory.Exists(instance.InstancePath))
                    {
                        progressCallback("Copying instance files...", 20);
                        CopyDirectory(instance.InstancePath, instanceTempDir);
                    }
                    
                    // 2. Copy icon
                    string iconFilename = null;
                    if (instance.HasIcon && File.Exists(instance.IconPath))
                    {
                        iconFilename = "icon" + Path.GetExtension(instance.IconPath);
                        File.Copy(instance.IconPath, Path.Combine(tempDir, iconFilename), true);
                    }
                    
                    // 3. Create flpack.json
                    progressCallback("Generating metadata...", 60);
                    var metadata = new
                    {
                        name = instance.Name,
                        minecraftVersion = instance.MinecraftVersion,
                        modLoader = instance.ModLoader.ToString(),
                        modLoaderVersion = instance.ModLoaderVersion,
                        allocatedRam = instance.AllocatedRam,
                        icon = iconFilename
                    };
                    
                    string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(Path.Combine(tempDir, "flpack.json"), json);
                    
                    // 4. Zip it
                    progressCallback("Zipping FLPack...", 80);
                    if (File.Exists(destinationZipPath)) File.Delete(destinationZipPath);
                    ZipFile.CreateFromDirectory(tempDir, destinationZipPath);
                    
                    progressCallback("Export complete!", 100);
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
            });
        }
        
        public async Task ImportFLPackAsync(string zipPath, Action<string, int> progressCallback)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FluentLauncher_Import", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            try
            {
                progressCallback("Extracting FLPack...", 0);
                ZipFile.ExtractToDirectory(zipPath, tempDir, true);
                
                string jsonPath = Path.Combine(tempDir, "flpack.json");
                if (!File.Exists(jsonPath))
                {
                    throw new Exception("Not a valid FLPack (flpack.json missing).");
                }
                
                progressCallback("Reading metadata...", 40);
                var json = await File.ReadAllTextAsync(jsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                string name = root.GetProperty("name").GetString() ?? "Imported FLPack";
                string mcVersion = root.GetProperty("minecraftVersion").GetString() ?? "1.20.4";
                string modLoaderStr = root.TryGetProperty("modLoader", out var loaderProp) ? loaderProp.GetString() : "Vanilla";
                ModLoaderType modLoader = Enum.TryParse<ModLoaderType>(modLoaderStr, out var parsedLoader) ? parsedLoader : ModLoaderType.Vanilla;
                string modLoaderVersion = root.TryGetProperty("modLoaderVersion", out var loaderVerProp) ? loaderVerProp.GetString() : "";
                int allocatedRam = root.TryGetProperty("allocatedRam", out var ramProp) && ramProp.TryGetInt32(out int ram) ? ram : 4096;
                string iconFilename = root.TryGetProperty("icon", out var iconProp) ? iconProp.GetString() : null;
                
                var instance = new Instance
                {
                    Name = name,
                    MinecraftVersion = mcVersion,
                    ModLoader = modLoader,
                    ModLoaderVersion = modLoaderVersion,
                    AllocatedRam = allocatedRam
                };
                
                _instanceManager.AddInstance(instance);
                
                progressCallback("Applying instance files...", 70);
                string instanceFilesDir = Path.Combine(tempDir, "instance");
                if (Directory.Exists(instanceFilesDir))
                {
                    CopyDirectory(instanceFilesDir, instance.InstancePath);
                }
                
                if (!string.IsNullOrEmpty(iconFilename))
                {
                    string sourceIcon = Path.Combine(tempDir, iconFilename);
                    if (File.Exists(sourceIcon))
                    {
                        string targetIcon = Path.Combine(instance.InstancePath, iconFilename);
                        File.Copy(sourceIcon, targetIcon, true);
                        instance.IconPath = targetIcon;
                        _instanceManager.SaveInstances();
                    }
                }
                
                progressCallback("Import complete!", 100);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
