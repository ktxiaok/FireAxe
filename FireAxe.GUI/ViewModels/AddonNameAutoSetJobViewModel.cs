using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using FireAxe.Resources;
using ReactiveUI;

namespace FireAxe.ViewModels;

public sealed class AddonNameAutoSetJobViewModel : AddonNodeSimpleViewModel, IDisposable
{
    private bool _disposed = false;

    private readonly CancellationTokenSource _cts = new();

    private string? _targetName = null;

    private bool _isRunning = false;

    private bool _isFailed = false;
    private readonly ObservableAsPropertyHelper<bool> _isSucceeded;

    private string? _setNameErrorMessage = null;

    private bool _isApplied = false;

    public AddonNameAutoSetJobViewModel(AddonNode addon, object? settingArg) : base(addon)
    {
        SettingArg = settingArg;

        _isSucceeded = this.WhenAnyValue(x => x.IsRunning, x => x.IsFailed)
            .Select(args =>
            {
                var (isRunning, isFailed) = args;
                return !isRunning && !isFailed;
            })
            .ToProperty(this, nameof(IsSucceeded));

        this.WhenAnyValue(x => x.TargetName)
            .Subscribe(_ => IsApplied = false);

        var canApply = this.WhenAnyValue(x => x.IsApplied, x => x.Addon, x => x.TargetName)
            .Select(args =>
            {
                var (isApplied, addon, targetName) = args;
                if (isApplied)
                {
                    return false;
                }
                if (addon is null)
                {
                    return false;
                }
                if (string.IsNullOrEmpty(targetName))
                {
                    return false;
                }
                return true;
            });
        ApplyCommand = ReactiveCommand.Create(Apply, canApply);

        DoJob();
    }

    public object? SettingArg { get; }

    public string? TargetName
    {
        get => _targetName;
        set => this.RaiseAndSetIfChanged(ref _targetName, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public bool IsFailed
    {
        get => _isFailed;
        private set => this.RaiseAndSetIfChanged(ref _isFailed, value);
    }

    public bool IsSucceeded => _isSucceeded.Value;

    public string? SetNameErrorMessage
    {
        get => _setNameErrorMessage;
        private set => this.RaiseAndSetIfChanged(ref _setNameErrorMessage, value);
    }

    public bool IsApplied
    {
        get => _isApplied;
        private set => this.RaiseAndSetIfChanged(ref _isApplied, value);
    }

    public ReactiveCommand<Unit, bool> ApplyCommand { get; }

    public event Action? ApplyRequested = null;

    public bool Apply()
    {
        ThrowIfDisposed();

        var addon = Addon;
        if (addon is null)
        {
            return false;
        }

        ApplyRequested?.Invoke();

        var name = TargetName;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        bool success = false;
        try
        {
            addon.Name = name;
            success = true;
        }
        catch (Exception ex)
        {
            SetNameErrorMessage = ObjectExplanationManager.Default.TryGet(ex, ExceptionExplanationScene.Input) ?? Texts.Error;
        }
        if (success)
        {
            IsApplied = true;
        }

        return success;
    }

    public bool Retry()
    {
        ThrowIfDisposed();

        if (IsRunning)
        {
            return false;
        }

        DoJob();
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _cts.Dispose();

            _disposed = true;
        }
    }

    protected override void OnNewAddon(AddonNode addon, CompositeDisposable disposables)
    {
        base.OnNewAddon(addon, disposables);

        addon.WhenAnyValue(x => x.Name)
            .Subscribe(_ => IsApplied = false)
            .DisposeWith(disposables);
    }

    private async void DoJob()
    {
        var addon = Addon;
        if (addon is null)
        {
            return;
        }

        var cancellationToken = _cts.Token;

        IsFailed = false;
        SetNameErrorMessage = null;
        IsRunning = true;

        string? name = null;
        try
        {
            name = await addon.TryGetSuggestedNameAsync(SettingArg, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            IsRunning = false;
        }

        if (name is null)
        {
            IsFailed = true;
            return;
        }

        TargetName = name;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}