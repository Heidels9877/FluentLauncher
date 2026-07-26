using System;
using System.IO;
using System.Windows.Controls;
using FluentLauncher.ViewModels;

namespace FluentLauncher.Views
{
    public partial class SkinPage : Page
    {
        public SkinViewModel ViewModel { get; }

        public SkinPage(SkinViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();

            InitializeWebView();

            ViewModel.OnSkinChanged = (url, variant) =>
            {
                UpdateWebViewSkin(url, variant);
            };
        }

        private bool _isWebViewLoaded = false;

        private async void InitializeWebView()
        {
            await SkinWebView.EnsureCoreWebView2Async(null);
            SkinWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            if (Directory.Exists(assetsPath))
            {
                SkinWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "fluentlauncher.local", assetsPath, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                
                SkinWebView.NavigationCompleted += (s, e) => 
                {
                    _isWebViewLoaded = true;
                    if (!string.IsNullOrEmpty(ViewModel.CurrentSkinUrl))
                    {
                        UpdateWebViewSkin(ViewModel.CurrentSkinUrl, ViewModel.SelectedVariant);
                    }
                };

                SkinWebView.CoreWebView2.Navigate("https://fluentlauncher.local/skinviewer.html");
            }
        }

        private void UpdateWebViewSkin(string url, string variant)
        {
            if (_isWebViewLoaded && SkinWebView.CoreWebView2 != null && !string.IsNullOrEmpty(url))
            {
                // Execute JS to update the skin in skinview3d
                SkinWebView.CoreWebView2.ExecuteScriptAsync($"loadSkin('{url}', '{variant}');");
            }
        }
    }
}
