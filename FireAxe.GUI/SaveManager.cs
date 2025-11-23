using Avalonia.Threading;
using Serilog;
using System;
using System.Collections.Generic;

namespace FireAxe;

public sealed class SaveManager : IDisposable
{
    public static readonly TimeSpan DefaultAutoSaveInterval = TimeSpan.FromSeconds(3);

    private bool _disposed = false;
    private bool _active = false;

    private readonly App _app;
    
    private readonly List<ISaveable> _saveables = new();

    private readonly DispatcherTimer _autoSaveTimer;

    public SaveManager(App app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _app = app;

        _autoSaveTimer = new(DefaultAutoSaveInterval, DispatcherPriority.Normal, (sender, e) =>
        {
            SaveAll();
        });
        _app.ShutdownRequested += OnAppShutdownRequested;
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _autoSaveTimer.Stop();

            _app.ShutdownRequested -= OnAppShutdownRequested;

            _disposed = true;
        }
    }

    private void OnAppShutdownRequested()
    {
        SaveAll(true);
    }
}
