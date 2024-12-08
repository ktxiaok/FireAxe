using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace L4D2AddonAssistant
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class AppSettings : ObservableObject, ISaveable
    {
        public const string SettingsFileName = "Settings.json";

        private string? _lastOpenDirectory = null;
        private string? _language = null;
        private string _gamePath = "";
        private bool _isAutoUpdateWorkshopItem = true;
        private string? _suppressedUpdateRequestVersion = null;

        private string _settingsFilePath;

        public AppSettings(App app)
        {
            ArgumentNullException.ThrowIfNull(app);

            _settingsFilePath = Path.Join(app.DocumentDirectoryPath, SettingsFileName);

            LoadFile();
            LanguageManager.CurrentLanguage = Language;
        }

        public bool RequestSave { get; set; } = true;

        [JsonProperty]
        public string? LastOpenDirectory
        {
            get => _lastOpenDirectory;
            set 
            {
                if (value == _lastOpenDirectory)
                {
                    return;
                }
                if (value != null)
                {
                    if (!FileUtils.IsValidPath(value) || !Path.IsPathRooted(value))
                    {
                        throw new ArgumentException("The path is invalid.");
                    }
                }
                _lastOpenDirectory = value;
                NotifyChanged();
                RequestSave = true;
            }
        }

        [JsonProperty]
        public string? Language
        {
            get => _language;
            set
            {
                if (NotifyAndSetIfChanged(ref _language, value))
                {
                    RequestSave = true;
                }
            }
        }

        [JsonProperty]
        public string GamePath
        {
            get => _gamePath;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (value == _gamePath)
                {
                    return;
                }
                if (value.Length > 0)
                {
                    if (!FileUtils.IsValidPath(value) || !Path.IsPathRooted(value))
                    {
                        throw new ArgumentException("The path is invalid.");
                    }
                }
                _gamePath = value;
                NotifyChanged();
                RequestSave = true;
            }
        }

        [JsonProperty]
        public bool IsAutoUpdateWorkshopItem
        {
            get => _isAutoUpdateWorkshopItem;
            set
            {
                if (NotifyAndSetIfChanged(ref _isAutoUpdateWorkshopItem, value))
                {
                    RequestSave = true;
                }
            }
        }

        [JsonProperty]
        public string? SuppressedUpdateRequestVersion
        {
            get => _suppressedUpdateRequestVersion;
            set
            {
                if (NotifyAndSetIfChanged(ref _suppressedUpdateRequestVersion, value))
                {
                    RequestSave = true;
                }
            }
        }

        public void LoadFile()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    JsonConvert.PopulateObject(File.ReadAllText(_settingsFilePath), this);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception in AppSettings.ReadFile.");
            }
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(_settingsFilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Exception in AppSettings.WriteFile.");
            }
        }
    }
}
