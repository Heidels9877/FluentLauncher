using System.Windows.Controls;
using FluentLauncher.ViewModels;

namespace FluentLauncher.Views
{
    public partial class InstancesPage : Page
    {
        public InstancesViewModel ViewModel { get; }

        public InstancesPage(InstancesViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
