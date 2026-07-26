using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentLauncher.Core;
using FluentLauncher.Models;

namespace FluentLauncher.ViewModels
{
    public partial class AccountsViewModel : ObservableObject
    {
        private readonly AccountManager _accountManager;

        public ObservableCollection<Account> Accounts => _accountManager.Accounts;

        [ObservableProperty]
        private Account _selectedAccount;

        [ObservableProperty]
        private string _offlineUsername = "";

        public AccountsViewModel(AccountManager accountManager)
        {
            _accountManager = accountManager;
            SelectedAccount = _accountManager.CurrentAccount;
        }

        partial void OnSelectedAccountChanged(Account value)
        {
            if (value != null)
            {
                _accountManager.SetCurrentAccount(value);
            }
        }

        [RelayCommand]
        public async Task LoginMicrosoftAsync()
        {
            await _accountManager.LoginMicrosoftAsync();
            SelectedAccount = _accountManager.CurrentAccount;
        }

        [RelayCommand]
        public void LoginOffline()
        {
            if (string.IsNullOrWhiteSpace(OfflineUsername)) return;
            _accountManager.LoginOffline(OfflineUsername);
            SelectedAccount = _accountManager.CurrentAccount;
            OfflineUsername = "";
        }

        [RelayCommand]
        public void RemoveAccount(Account acc)
        {
            if (acc != null)
            {
                _accountManager.RemoveAccount(acc);
                SelectedAccount = _accountManager.CurrentAccount;
            }
        }
    }
}
