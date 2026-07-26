using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FluentLauncher.Views;
using FluentLauncher.ViewModels;
using Wpf.Ui;

namespace FluentLauncher
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public new static App Current => (App)Application.Current;

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            
            services.AddSingleton<Core.AccountManager>();
            services.AddSingleton<Core.InstanceManager>();
            services.AddSingleton<Core.LauncherService>();
            services.AddSingleton<Core.ModpackService>();
            services.AddSingleton<Core.ModrinthApiService>();
            services.AddSingleton<Core.MinecraftApiService>();
            services.AddSingleton<Core.UpdateService>();
            services.AddSingleton(Core.AppSettings.Load());

            // ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<ViewModels.AccountsViewModel>();
            services.AddTransient<ViewModels.InstancesViewModel>();
            services.AddTransient<ViewModels.SettingsViewModel>();
            services.AddTransient<ViewModels.CreateInstanceViewModel>();
            services.AddTransient<ViewModels.InstanceDetailsViewModel>();
            services.AddTransient<ViewModels.SkinViewModel>();

            // Pages
            services.AddSingleton<MainWindow>();
            services.AddTransient<Views.AccountsPage>();
            services.AddTransient<Views.InstancesPage>();
            services.AddTransient<Views.SettingsPage>();
            services.AddTransient<Views.CreateInstancePage>();
            services.AddTransient<Views.InstanceDetailsPage>();
            services.AddTransient<Views.SkinPage>();

            return services.BuildServiceProvider();
        }

        public static string? StartupFlpackPath { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = Services.GetRequiredService<Core.AppSettings>();
            ApplyLanguage(settings.Language);
            
            // Register file association
            Core.FileAssociation.RegisterFlpackAssociation();

            // Check if launched from a .flpack file
            if (e.Args.Length > 0 && e.Args[0].EndsWith(".flpack", StringComparison.OrdinalIgnoreCase))
            {
                StartupFlpackPath = e.Args[0];
            }

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        public static void ApplyLanguage(string languageCode)
        {
            var dict = new ResourceDictionary();
            dict.Source = new Uri($"pack://application:,,,/Resources/Strings.{languageCode}.xaml");
            
            var existing = Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Resources/Strings."));
            if (existing != null)
                Current.Resources.MergedDictionaries.Remove(existing);
                
            Current.Resources.MergedDictionaries.Add(dict);
        }
    }
}
