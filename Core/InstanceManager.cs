using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using FluentLauncher.Models;

namespace FluentLauncher.Core
{
    public class InstanceManager
    {
        private readonly string _instancesFilePath;
        public string InstancesDirectory { get; private set; }

        public ObservableCollection<Instance> Instances { get; private set; } = new();

        public Instance SelectedInstance { get; set; }

        public InstanceManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            InstancesDirectory = Path.Combine(appData, ".fluentlauncher", "instances");
            _instancesFilePath = Path.Combine(appData, ".fluentlauncher", "instances.json");
            
            if (!Directory.Exists(InstancesDirectory))
            {
                Directory.CreateDirectory(InstancesDirectory);
            }

            LoadInstances();
        }

        public void LoadInstances()
        {
            if (File.Exists(_instancesFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_instancesFilePath);
                    var list = JsonSerializer.Deserialize<List<Instance>>(json);
                    Instances.Clear();
                    if (list != null)
                    {
                        foreach (var inst in list) Instances.Add(inst);
                    }
                }
                catch
                {
                    Instances.Clear();
                }
            }
        }

        public void SaveInstances()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Instances, options);
            File.WriteAllText(_instancesFilePath, json);
        }

        public void AddInstance(Instance instance)
        {
            instance.InstancePath = Path.Combine(InstancesDirectory, instance.Id);
            if (!Directory.Exists(instance.InstancePath))
            {
                Directory.CreateDirectory(instance.InstancePath);
            }

            if (!string.IsNullOrEmpty(instance.IconPath) && File.Exists(instance.IconPath))
            {
                string ext = Path.GetExtension(instance.IconPath);
                string newIconPath = Path.Combine(instance.InstancePath, "icon" + ext);
                if (instance.IconPath != newIconPath)
                {
                    File.Copy(instance.IconPath, newIconPath, true);
                    instance.IconPath = newIconPath;
                }
            }

            Instances.Add(instance);
            SaveInstances();
        }

        public void RemoveInstance(Instance instance)
        {
            if (Directory.Exists(instance.InstancePath))
            {
                Directory.Delete(instance.InstancePath, true);
            }
            Instances.Remove(instance);
            SaveInstances();
        }
    }
}
