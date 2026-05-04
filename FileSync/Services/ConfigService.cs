using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using FileSync.Models;
using Newtonsoft.Json;

namespace FileSync.Services
{
    public class ConfigService
    {
        private static readonly Lazy<ConfigService> _instance = new Lazy<ConfigService>(() => new ConfigService());
        public static ConfigService Instance => _instance.Value;

        private readonly string _configPath;
        private readonly string _settingsPath;
        private List<SyncTask> _tasks = null!;
        private AppSettings _settings = null!;

        private ConfigService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configDir = Path.Combine(appData, "FileSync");
            Directory.CreateDirectory(configDir);
            _configPath = Path.Combine(configDir, "tasks.json");
            _settingsPath = Path.Combine(configDir, "settings.json");
            Load();
        }

        public IReadOnlyList<SyncTask> Tasks => _tasks.AsReadOnly();
        public AppSettings Settings => _settings;

        public void Load()
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                _tasks = JsonConvert.DeserializeObject<List<SyncTask>>(json) ?? new List<SyncTask>();
            }
            else
            {
                _tasks = new List<SyncTask>();
            }

            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                _settings = new AppSettings();
            }
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(_tasks, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }

        public void SaveSettings()
        {
            string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
        }

        public void AddTask(SyncTask task)
        {
            _tasks.Add(task);
            Save();
        }

        public void UpdateTask(SyncTask task)
        {
            var existing = _tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
                int index = _tasks.IndexOf(existing);
                _tasks[index] = task;
                Save();
            }
        }

        public void DeleteTask(Guid taskId)
        {
            _tasks.RemoveAll(t => t.Id == taskId);
            Save();
        }

        public SyncTask GetTask(Guid taskId)
        {
            return _tasks.FirstOrDefault(t => t.Id == taskId);
        }

        public void SetAutoStart(bool enable)
        {
            const string keyName = "FileSync";
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null)
                    return;

                if (enable)
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(keyName, exePath);
                    }
                }
                else
                {
                    key.DeleteValue(keyName, false);
                }

                _settings.AutoStart = enable;
                SaveSettings();
            }
            catch
            {
                // 静默失败
            }
        }
    }
}
