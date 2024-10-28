using Avalonia.Threading;
using Serilog;
using System;
using System.Collections.Generic;

namespace L4D2AddonAssistant
{
    public class SaveManager
    {
        public static readonly TimeSpan DefaultAutoSaveInterval = TimeSpan.FromSeconds(3);

        private bool _active = false;
        
        private List<ISaveable> _saveables = new();

        private DispatcherTimer _autoSaveTimer;

        public SaveManager(App app)
        {
            _autoSaveTimer = new(DefaultAutoSaveInterval, DispatcherPriority.Normal, (sender, e) =>
            {
                SaveAll();
            });
            app.ShutdownRequested += () =>
            {
                SaveAll();
            };
        }

        public IEnumerable<ISaveable> Saveables => _saveables;

        public TimeSpan AutoSaveInterval
        {
            get => _autoSaveTimer.Interval;
            set => _autoSaveTimer.Interval = value;
        }

        public void Run()
        {
            if (_active)
            {
                return;
            }

            _autoSaveTimer.Start();
            _active = true;
        }

        public void Register(ISaveable saveable)
        {
            _saveables.Add(saveable);
        }

        public void SaveAll(bool forceSave = false)
        {
            foreach (var saveable in _saveables)
            {
                if (forceSave || saveable.RequestSave)
                {
                    try
                    {
                        saveable.Save();
                        saveable.RequestSave = false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during ISaveable.Save. (ClassName: {ClassName})", saveable.GetType().FullName);
                    }
                }
            }
        }
    }
}
