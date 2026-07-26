using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace FluentLauncher.Core
{
    public class MinecraftApiService
    {
        private readonly HttpClient _httpClient;

        public MinecraftApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string?> GetCurrentSkinUrlAsync(string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("skins", out var skinsArray) && skinsArray.GetArrayLength() > 0)
                    {
                        var firstSkin = skinsArray[0];
                        if (firstSkin.TryGetProperty("url", out var urlElement))
                        {
                            return urlElement.GetString();
                        }
                    }
                }
            }
            catch { }
            
            return null;
        }

        public async Task<bool> UploadSkinAsync(string accessToken, string filePath, string variant)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.minecraftservices.com/minecraft/profile/skins");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var content = new MultipartFormDataContent();
                
                // Add variant
                content.Add(new StringContent(variant), "variant");
                
                // Add file
                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                content.Add(fileContent, "file", Path.GetFileName(filePath));

                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UploadSkinFromUrlAsync(string accessToken, string skinUrl, string variant)
        {
            try
            {
                // Download the skin first to ensure we can upload it as multipart form data
                // as some APIs might block direct URL setting if they don't trust the source
                var imageBytes = await _httpClient.GetByteArrayAsync(skinUrl);
                var tempFile = Path.GetTempFileName();
                await File.WriteAllBytesAsync(tempFile, imageBytes);
                
                var success = await UploadSkinAsync(accessToken, tempFile, variant);
                
                try { File.Delete(tempFile); } catch { }
                
                return success;
            }
            catch
            {
                return false;
            }
        }
    }
}
