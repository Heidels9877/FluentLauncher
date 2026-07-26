using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Appearance;

namespace FluentLauncher.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public Core.AppSettings Settings { get; }
        private readonly Core.UpdateService _updateService;

        public SettingsViewModel(Core.AppSettings settings, Core.UpdateService updateService)
        {
            Settings = settings;
            _updateService = updateService;
            _selectedLanguageIndex = settings.Language == "uk" ? 1 : 0;
        }

        [ObservableProperty]
        private int _selectedThemeIndex = 0;

        [ObservableProperty]
        private int _selectedLanguageIndex = 0;

        partial void OnSelectedThemeIndexChanged(int value)
        {
            if (value == 0)
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            else
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
        }

        partial void OnSelectedLanguageIndexChanged(int value)
        {
            if (value == 0)
            {
                Settings.Language = "en";
                App.ApplyLanguage("en");
            }
            else
            {
                Settings.Language = "uk";
                App.ApplyLanguage("uk");
            }
        }

        [RelayCommand]
        public void GoToAccounts()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
            mainWindow?.RootNavigation.Navigate(typeof(Views.AccountsPage));
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            var hasUpdate = await _updateService.CheckForUpdatesAsync();
            var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
            if (mainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.HasUpdate = hasUpdate;
            }

            if (hasUpdate)
            {
                // Show snackbar
                System.Windows.MessageBox.Show("An update is available! Click the download icon in the bottom left.", "Update Available", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("You are on the latest version.", "Up to date", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
    }
}
