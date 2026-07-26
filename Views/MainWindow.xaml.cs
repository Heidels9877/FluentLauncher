using System;
using System.Windows;
using FluentLauncher.ViewModels;
using Wpf.Ui.Controls;

namespace FluentLauncher.Views
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(MainWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

            RootNavigation.SetServiceProvider(App.Current.Services);
            Loaded += (s, e) => RootNavigation.Navigate(typeof(InstancesPage));
        }
    }
}
