using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace FluentLauncher.Core
{
    public class UpdateService
    {
        private const string RepoOwner = "Heidels9877";
        private const string RepoName = "FluentLauncher";
        
        public bool HasUpdate { get; private set; }
        public string LatestVersion { get; private set; } = string.Empty;
        public string ReleaseUrl { get; private set; } = string.Empty;
        
        public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FluentLauncher", CurrentVersion));

                var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    
                    var tagName = doc.RootElement.GetProperty("tag_name").GetString();
                    ReleaseUrl = doc.RootElement.GetProperty("html_url").GetString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(tagName))
                    {
                        var latestVersionStr = tagName.TrimStart('v', 'V');
                        LatestVersion = latestVersionStr;

                        if (Version.TryParse(latestVersionStr, out var latest) && 
                            Version.TryParse(CurrentVersion, out var current))
                        {
                            HasUpdate = latest > current;
                            return HasUpdate;
                        }
                    }
                }
            }
            catch
            {
                // Ignore network errors during background check
            }
            return false;
        }

        public void OpenUpdatePage()
        {
            if (!string.IsNullOrEmpty(ReleaseUrl))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ReleaseUrl,
                    UseShellExecute = true
                });
            }
        }
    }
}
