using System.Windows.Controls;
using FluentLauncher.ViewModels;

namespace FluentLauncher.Views
{
    public partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
