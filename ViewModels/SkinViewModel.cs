using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace FluentLauncher.ViewModels
{
    public partial class PredefinedSkin : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Variant { get; set; } = "classic"; // classic or slim
        
        [ObservableProperty]
        private BitmapImage? _previewImage;

        public async Task LoadImageAsync(string url)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "FluentLauncher/1.0");
                var bytes = await client.GetByteArrayAsync(url);
                
                App.Current.Dispatcher.Invoke(() =>
                {
                    var image = new BitmapImage();
                    using (var mem = new System.IO.MemoryStream(bytes))
                    {
                        mem.Position = 0;
                        image.BeginInit();
                        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.UriSource = null;
                        image.StreamSource = mem;
                        image.EndInit();
                    }
                    image.Freeze();
                    PreviewImage = image;
                });
            }
            catch (Exception)
            {
                // Fallback or ignore
            }
        }
    }

    public partial class SkinViewModel : ObservableObject
    {
        private readonly Core.AccountManager _accountManager;
        private readonly Core.MinecraftApiService _apiService;

        [ObservableProperty]
        private bool _isOfflineAccount;

        [ObservableProperty]
        private bool _isWorking;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _selectedVariant = "classic"; // ComboBox selection

        [ObservableProperty]
        private string _currentSkinUrl = string.Empty;

        public ObservableCollection<PredefinedSkin> ReadyMadeSkins { get; } = new();

        public Action<string, string>? OnSkinChanged;

        public SkinViewModel(Core.AccountManager accountManager, Core.MinecraftApiService apiService)
        {
            _accountManager = accountManager;
            _apiService = apiService;

            // Initialize predefined skins
            var steve = new PredefinedSkin { Name = "Steve (Default)", Url = "https://minotar.net/skin/Steve", Variant = "classic" };
            _ = steve.LoadImageAsync("https://minotar.net/armor/bust/Steve/100.png");
            ReadyMadeSkins.Add(steve);
            
            var alex = new PredefinedSkin { Name = "Alex (Default)", Url = "https://minotar.net/skin/Alex", Variant = "slim" };
            _ = alex.LoadImageAsync("https://minotar.net/armor/bust/Alex/100.png");
            ReadyMadeSkins.Add(alex);
            
            var dream = new PredefinedSkin { Name = "Dream", Url = "https://minotar.net/skin/Dream", Variant = "classic" };
            _ = dream.LoadImageAsync("https://minotar.net/armor/bust/Dream/100.png");
            ReadyMadeSkins.Add(dream);
            
            var technoblade = new PredefinedSkin { Name = "Technoblade", Url = "https://minotar.net/skin/Technoblade", Variant = "classic" };
            _ = technoblade.LoadImageAsync("https://minotar.net/armor/bust/Technoblade/100.png");
            ReadyMadeSkins.Add(technoblade);
            
            var grian = new PredefinedSkin { Name = "Grian", Url = "https://minotar.net/skin/Grian", Variant = "classic" };
            _ = grian.LoadImageAsync("https://minotar.net/armor/bust/Grian/100.png");
            ReadyMadeSkins.Add(grian);
            
            var ldShadowLady = new PredefinedSkin { Name = "LDShadowLady", Url = "https://minotar.net/skin/LDShadowLady", Variant = "slim" };
            _ = ldShadowLady.LoadImageAsync("https://minotar.net/armor/bust/LDShadowLady/100.png");
            ReadyMadeSkins.Add(ldShadowLady);

            _accountManager.SessionChanged += CheckAccountState;
            CheckAccountState();
        }

        private void CheckAccountState()
        {
            if (_accountManager.CurrentAccount == null || _accountManager.CurrentAccount.Type == Models.AccountType.Offline)
            {
                IsOfflineAccount = true;
            }
            else
            {
                IsOfflineAccount = false;
                _ = LoadCurrentSkinAsync();
            }
        }

        private async Task LoadCurrentSkinAsync()
        {
            if (IsOfflineAccount || _accountManager.CurrentAccount == null) return;
            
            string token = _accountManager.CurrentAccount.AccessToken;
            if (string.IsNullOrEmpty(token)) return;

            var skinUrl = await _apiService.GetCurrentSkinUrlAsync(token);
            if (!string.IsNullOrEmpty(skinUrl))
            {
                if (skinUrl.StartsWith("http://"))
                {
                    skinUrl = skinUrl.Replace("http://", "https://");
                }
                
                // We don't know the exact variant from just the URL easily without full profile info, default to classic or current
                CurrentSkinUrl = skinUrl;
                OnSkinChanged?.Invoke(skinUrl, SelectedVariant);
            }
        }

        [RelayCommand]
        private async Task UploadSkinAsync()
        {
            if (IsOfflineAccount || _accountManager.CurrentAccount == null) return;

            var dialog = new OpenFileDialog
            {
                Filter = "PNG Images|*.png",
                Title = "Select Minecraft Skin"
            };

            if (dialog.ShowDialog() == true)
            {
                IsWorking = true;
                StatusMessage = "Uploading skin...";

                var success = await _apiService.UploadSkinAsync(_accountManager.CurrentAccount.AccessToken, dialog.FileName, SelectedVariant);
                
                IsWorking = false;
                
                if (success)
                {
                    StatusMessage = "Skin updated successfully!";
                    // Since it's a local file, we can either pass it as a blob/base64 to WebView or just reload from API
                    await LoadCurrentSkinAsync();
                }
                else
                {
                    StatusMessage = "Failed to upload skin. Try again.";
                }
            }
        }

        [RelayCommand]
        private async Task ApplyPredefinedSkinAsync(PredefinedSkin skin)
        {
            if (IsOfflineAccount || _accountManager.CurrentAccount == null || skin == null) return;

            IsWorking = true;
            StatusMessage = $"Applying {skin.Name} skin...";

            var success = await _apiService.UploadSkinFromUrlAsync(_accountManager.CurrentAccount.AccessToken, skin.Url, skin.Variant);
            
            IsWorking = false;
            
            if (success)
            {
                StatusMessage = "Skin updated successfully!";
                CurrentSkinUrl = skin.Url;
                OnSkinChanged?.Invoke(skin.Url, skin.Variant);
                SelectedVariant = skin.Variant;
            }
            else
            {
                StatusMessage = "Failed to apply skin. Try again.";
            }
        }
    }
}
