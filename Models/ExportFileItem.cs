using CommunityToolkit.Mvvm.ComponentModel;

namespace FluentLauncher.Models
{
    public partial class ExportFileItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private bool _isDirectory = false;

        public string FullPath { get; set; } = string.Empty;
    }
}
