using System.Windows.Controls;
using FluentLauncher.ViewModels;

namespace FluentLauncher.Views
{
    public partial class CreateInstancePage : Page
    {
        public CreateInstanceViewModel ViewModel { get; }

        public CreateInstancePage(CreateInstanceViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
