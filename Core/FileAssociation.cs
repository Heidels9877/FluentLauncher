using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace FluentLauncher.Core
{
    public static class FileAssociation
    {
        public static void RegisterFlpackAssociation()
        {
            try
            {
                string extension = ".flpack";
                string progId = "FluentLauncher.Pack";
                string description = "Fluent Launcher Modpack";
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (string.IsNullOrEmpty(exePath)) return;

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}"))
                {
                    key.SetValue("", progId);
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    key.SetValue("", description);
                    
                    using (RegistryKey defaultIcon = key.CreateSubKey("DefaultIcon"))
                    {
                        defaultIcon.SetValue("", $"\"{exePath}\",0");
                    }
                    
                    using (RegistryKey command = key.CreateSubKey(@"shell\open\command"))
                    {
                        command.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }
            }
            catch
            {
                // Ignore errors if registry writing fails (e.g., access denied, though CurrentUser should usually be accessible)
            }
        }
    }
}
