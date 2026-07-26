using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using FluentLauncher.Models;

namespace FluentLauncher.Core
{
    public partial class ModrinthProject : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string Author { get; set; }
        public int Downloads { get; set; }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool _isInstalled;
    }

    public class ModrinthVersion
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string VersionNumber { get; set; }
        public List<ModrinthFile> Files { get; set; }
    }

    public class ModrinthFile
    {
        public string Url { get; set; }
        public string Filename { get; set; }
    }

    public class ModrinthGalleryImage
    {
        public string Url { get; set; }
        public bool Featured { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Created { get; set; }
        public int Ordering { get; set; }
    }

    public class ModrinthFullProject : ModrinthProject
    {
        public string Body { get; set; }
        public List<ModrinthGalleryImage> Gallery { get; set; } = new();
    }

    public class ModrinthApiService
    {
        private readonly HttpClient _client;

        public ModrinthApiService()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "FluentLauncher/1.0 (contact@fluentlauncher.com)");
        }

        public async Task<ModrinthFullProject> GetFullProjectAsync(string projectId)
        {
            try
            {
                var url = $"https://api.modrinth.com/v2/project/{projectId}";
                var response = await _client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                var project = new ModrinthFullProject
                {
                    ProjectId = root.GetProperty("id").GetString(),
                    Title = root.GetProperty("title").GetString(),
                    Description = root.GetProperty("description").GetString(),
                    IconUrl = root.TryGetProperty("icon_url", out var iconProp) && iconProp.ValueKind == JsonValueKind.String ? iconProp.GetString() : "",
                    Body = root.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String ? bodyProp.GetString() : ""
                };

                if (root.TryGetProperty("gallery", out var galleryArray) && galleryArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in galleryArray.EnumerateArray())
                    {
                        project.Gallery.Add(new ModrinthGalleryImage
                        {
                            Url = item.GetProperty("url").GetString(),
                            Title = item.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String ? titleProp.GetString() : "",
                            Description = item.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String ? descProp.GetString() : ""
                        });
                    }
                }

                return project;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<ModrinthProject>> SearchProjectsAsync(string query, string projectType, string mcVersion, ModLoaderType modLoader, string sortMethod = "relevance", int offset = 0)
        {
            var loaderString = modLoader.ToString().ToLower();
            if (loaderString == "vanilla") loaderString = "fabric"; 

            var facets = new List<string>
            {
                $"[\"project_type:{projectType}\"]",
                $"[\"versions:{mcVersion}\"]"
            };

            // Shaders and Resource packs don't strictly need loader facets, but adding them might filter out universal packs.
            // Only apply loader facet for mods
            if (projectType == "mod" && modLoader != ModLoaderType.Vanilla)
            {
                facets.Add($"[\"categories:{loaderString}\"]");
            }

            string facetsParam = $"[{string.Join(",", facets)}]";
            var queryEncoded = Uri.EscapeDataString(query ?? "");
            
            var url = $"https://api.modrinth.com/v2/search?query={queryEncoded}&facets={Uri.EscapeDataString(facetsParam)}&index={sortMethod}&limit=20&offset={offset}";

            var response = await _client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            
            var projects = new List<ModrinthProject>();
            if (doc.RootElement.TryGetProperty("hits", out var hits))
            {
                foreach (var hit in hits.EnumerateArray())
                {
                    projects.Add(new ModrinthProject
                    {
                        ProjectId = hit.GetProperty("project_id").GetString(),
                        Title = hit.GetProperty("title").GetString(),
                        Description = hit.GetProperty("description").GetString(),
                        IconUrl = hit.TryGetProperty("icon_url", out var icon) && icon.ValueKind != JsonValueKind.Null ? icon.GetString() : "",
                        Author = hit.GetProperty("author").GetString(),
                        Downloads = hit.GetProperty("downloads").GetInt32()
                    });
                }
            }

            return projects;
        }

        public async Task<ModrinthVersion> GetLatestVersionAsync(string projectId, string mcVersion, ModLoaderType modLoader, string projectType = "mod")
        {
            var loaderString = modLoader.ToString().ToLower();
            string loadersParam = (modLoader != ModLoaderType.Vanilla && projectType == "mod") ? $"&loaders=[\"{loaderString}\"]" : "";
            string url = $"https://api.modrinth.com/v2/project/{projectId}/version?game_versions=[\"{mcVersion}\"]{loadersParam}";
            
            var response = await _client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var v = doc.RootElement[0];
                var version = new ModrinthVersion
                {
                    Id = v.GetProperty("id").GetString(),
                    Name = v.GetProperty("name").GetString(),
                    VersionNumber = v.GetProperty("version_number").GetString(),
                    Files = new List<ModrinthFile>()
                };

                foreach (var f in v.GetProperty("files").EnumerateArray())
                {
                    version.Files.Add(new ModrinthFile
                    {
                        Url = f.GetProperty("url").GetString(),
                        Filename = f.GetProperty("filename").GetString()
                    });
                }
                return version;
            }

            return null;
        }

        public async Task DownloadFileAsync(string url, string destinationPath, IProgress<int> progress = null)
        {
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var totalRead = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            do
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (canReportProgress)
                    {
                        progress.Report((int)((totalRead * 100) / totalBytes));
                    }
                }
            } while (isMoreToRead);
        }

        public async Task<ModrinthProject> GetProjectFromHashAsync(string sha1Hash)
        {
            try
            {
                var url = $"https://api.modrinth.com/v2/version_file/{sha1Hash}?algorithm=sha1";
                var response = await _client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                
                var projectId = doc.RootElement.GetProperty("project_id").GetString();
                if (!string.IsNullOrEmpty(projectId))
                {
                    // Fetch project details to get Title and IconUrl
                    var projectUrl = $"https://api.modrinth.com/v2/project/{projectId}";
                    var projectResponse = await _client.GetStringAsync(projectUrl);
                    using var projectDoc = JsonDocument.Parse(projectResponse);
                    var root = projectDoc.RootElement;
                    
                    return new ModrinthProject
                    {
                        ProjectId = root.GetProperty("id").GetString(),
                        Title = root.GetProperty("title").GetString(),
                        Description = root.GetProperty("description").GetString(),
                        IconUrl = root.TryGetProperty("icon_url", out var iconProp) && iconProp.ValueKind == JsonValueKind.String ? iconProp.GetString() : "",
                    };
                }
            }
            catch { }
            return null;
        }
    }
}
