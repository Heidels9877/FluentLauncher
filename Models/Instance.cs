using System;

namespace FluentLauncher.Models
{
    public enum ModLoaderType
    {
        Vanilla,
        Forge,
        Fabric,
        NeoForge,
        Quilt
    }

    public partial class Instance : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _id = Guid.NewGuid().ToString();

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _name = "New Instance";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [System.Text.Json.Serialization.JsonIgnore]
        private bool _hasIcon;

        private string _iconPath = "";
        public string IconPath
        {
            get => _iconPath;
            set
            {
                SetProperty(ref _iconPath, value);
                HasIcon = !string.IsNullOrEmpty(value);
            }
        }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _minecraftVersion = "1.20.4";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private ModLoaderType _modLoader = ModLoaderType.Vanilla;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _modLoaderVersion = "";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _lastPlayed = "Never";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private TimeSpan _playTime = TimeSpan.Zero;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _instancePath = "";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private int _allocatedRam = 4096;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _javaPath = "";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private bool _isRunning;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private System.Diagnostics.Process _runningProcess;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private string _instanceLogs = "";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private int _launchProgress;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private string _launchStatus = "";
    }
}
