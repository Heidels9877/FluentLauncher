using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentLauncher.Core;

namespace FluentLauncher.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly AccountManager _accountManager;

        [ObservableProperty]
        private string _username = "Guest";

        [ObservableProperty]
        private string _avatarUrl = "https://minotar.net/helm/Guest/64.png";

        [ObservableProperty]
        private bool _hasUpdate;

        [ObservableProperty]
        private string _appVersion;

        private readonly UpdateService _updateService;

        public MainWindowViewModel(AccountManager accountManager, UpdateService updateService)
        {
            _accountManager = accountManager;
            _updateService = updateService;
            AppVersion = "v" + _updateService.CurrentVersion;

            _accountManager.SessionChanged += () =>
            {
                if (_accountManager.CurrentAccount != null)
                {
                    Username = _accountManager.CurrentAccount.Username;
                    AvatarUrl = _accountManager.CurrentAccount.AvatarUrl;
                }
            };
            _ = InitializeAuthAsync();
            
            if (AppSettings.Load().AutoCheckUpdates)
            {
                _ = CheckForUpdatesAsync();
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            HasUpdate = await _updateService.CheckForUpdatesAsync();
        }

        [RelayCommand]
        public void OpenUpdateUrl()
        {
            if (HasUpdate)
            {
                _updateService.OpenUpdatePage();
            }
        }

        private async Task InitializeAuthAsync()
        {
            var session = await _accountManager.TryLoginSilentAsync();
            if (session != null)
            {
                _accountManager.CurrentSession = session;
            }
        }

        [RelayCommand]
        public void Login()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
            mainWindow?.RootNavigation.Navigate(typeof(Views.AccountsPage));
        }
    }
}
