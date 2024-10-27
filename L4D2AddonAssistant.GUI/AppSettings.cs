using Newtonsoft.Json;
using ReactiveUI;
using Serilog;
using System;
using System.IO;

namespace L4D2AddonAssistant
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class AppSettings : ReactiveObject
    {
        public const string SettingsFileName = "Settings.json";

        private string? _lastOpenDirectory = null;

        private string _settingsFilePath;

        public AppSettings(App app)
        {
            ArgumentNullException.ThrowIfNull(app);

            _settingsFilePath = Path.Join(app.DocumentDirectoryPath, SettingsFileName);

            ReadFile();
        }

        [JsonProperty]
        public string? LastOpenDirectory
        {
            get => _lastOpenDirectory;
            set => this.RaiseAndSetIfChanged(ref _lastOpenDirectory, value);
        }

        public void ReadFile()
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

        public void WriteFile()
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
