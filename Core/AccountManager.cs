using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using FluentLauncher.Models;

namespace FluentLauncher.Core
{
    public class AccountManager
    {
        private readonly CmlLib.Core.Auth.Microsoft.JELoginHandler _loginHandler;

        public ObservableCollection<Account> Accounts { get; } = new();
        
        public event Action SessionChanged;
        
        private MSession _currentSession;
        public MSession CurrentSession 
        { 
            get => _currentSession;
            set
            {
                _currentSession = value;
                SessionChanged?.Invoke();
            }
        }

        private Account _currentAccount;
        public Account CurrentAccount 
        {
            get => _currentAccount;
            set
            {
                _currentAccount = value;
                if (value != null)
                {
                    CurrentSession = new MSession
                    {
                        Username = value.Username,
                        UUID = value.UUID,
                        AccessToken = value.AccessToken
                    };
                }
            }
        }

        private readonly string _accountsFilePath;

        public AccountManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, ".fluentlauncher");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _accountsFilePath = Path.Combine(dir, "accounts.json");

            _loginHandler = CmlLib.Core.Auth.Microsoft.JELoginHandlerBuilder.BuildDefault();
            LoadAccounts();
        }

        private void LoadAccounts()
        {
            if (File.Exists(_accountsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_accountsFilePath);
                    var list = JsonSerializer.Deserialize<Account[]>(json);
                    if (list != null)
                    {
                        foreach (var acc in list) Accounts.Add(acc);
                    }
                    if (Accounts.Count > 0)
                        CurrentAccount = Accounts[0];
                }
                catch { }
            }
        }

        private void SaveAccounts()
        {
            var json = JsonSerializer.Serialize(Accounts);
            File.WriteAllText(_accountsFilePath, json);
        }

        public void SetCurrentAccount(Account account)
        {
            CurrentAccount = account;
            SaveAccounts();
        }

        public async Task<MSession> LoginMicrosoftAsync()
        {
            try
            {
                var session = await _loginHandler.AuthenticateInteractively();
                if (session != null)
                {
                    AddOrUpdateAccount(new Account
                    {
                        Username = session.Username,
                        UUID = session.UUID,
                        AccessToken = session.AccessToken,
                        Type = AccountType.Microsoft
                    });
                }
                return session;
            }
            catch { return null; }
        }

        public MSession LoginOffline(string username)
        {
            var session = MSession.CreateOfflineSession(username);
            AddOrUpdateAccount(new Account
            {
                Username = session.Username,
                UUID = session.UUID,
                AccessToken = session.AccessToken,
                Type = AccountType.Offline
            });
            return session;
        }

        public async Task<MSession> TryLoginSilentAsync()
        {
            // For Microsoft we might want to refresh tokens via JELoginHandler
            if (CurrentAccount != null && CurrentAccount.Type == AccountType.Microsoft)
            {
                try
                {
                    var session = await _loginHandler.AuthenticateSilently();
                    if (session != null)
                    {
                        CurrentAccount.AccessToken = session.AccessToken;
                        SaveAccounts();
                        CurrentSession = session;
                        return session;
                    }
                }
                catch { }
            }
            return CurrentSession;
        }

        private void AddOrUpdateAccount(Account acc)
        {
            var existing = Accounts.FirstOrDefault(a => a.UUID == acc.UUID || (a.Type == AccountType.Offline && a.Username == acc.Username));
            if (existing != null)
            {
                existing.AccessToken = acc.AccessToken;
                existing.Username = acc.Username;
                CurrentAccount = existing;
            }
            else
            {
                Accounts.Add(acc);
                CurrentAccount = acc;
            }
            SaveAccounts();
        }

        public void RemoveAccount(Account acc)
        {
            if (acc == null) return;
            Accounts.Remove(acc);
            if (CurrentAccount == acc)
            {
                CurrentAccount = Accounts.FirstOrDefault();
            }
            SaveAccounts();
        }
    }
}
