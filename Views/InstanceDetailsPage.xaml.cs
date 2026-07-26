using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using FluentLauncher.ViewModels;

namespace FluentLauncher.Views
{
    public partial class InstanceDetailsPage : Page
    {
        public InstanceDetailsViewModel ViewModel { get; }

        public InstanceDetailsPage(InstanceDetailsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
            
            Loaded += (s, e) => 
            {
                ViewModel.OnNavigatedTo();
                
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.SizeChanged += (ws, we) => 
                    {
                        RootGrid.MaxHeight = Math.Max(0, window.ActualHeight - 80);
                    };
                    RootGrid.MaxHeight = Math.Max(0, window.ActualHeight - 80);
                }
            };
        }

        private void ModSearchListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = e.OriginalSource as ScrollViewer;
            if (scrollViewer != null)
            {
                // Trigger load more when we scroll exactly to the bottom
                if (scrollViewer.VerticalOffset > 0 && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1)
                {
                    if (ViewModel.LoadMoreSearchModsCommand.CanExecute(null))
                    {
                        ViewModel.LoadMoreSearchModsCommand.Execute(null);
                    }
                }
            }
        }
    }
}
