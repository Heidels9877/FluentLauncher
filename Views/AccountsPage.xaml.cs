using System.Windows.Controls;
using FluentLauncher.ViewModels;

namespace FluentLauncher.Views
{
    public partial class AccountsPage : Page
    {
        public AccountsViewModel ViewModel { get; }

        public AccountsPage(AccountsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
