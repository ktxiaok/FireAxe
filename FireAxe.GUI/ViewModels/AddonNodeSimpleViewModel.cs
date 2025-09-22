using Avalonia.Media.Imaging;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace FireAxe.ViewModels;

public class AddonNodeSimpleViewModel : ViewModelBase, IActivatableViewModel
{
    private bool _isActive = false;

    private readonly AddonRoot _addonRoot;

    private AddonNode? _addon = null;
    private CompositeDisposable? _addonDisposables = null;
    private Guid _lastAddonId = Guid.Empty;

    private Bitmap? _image = null;
    private byte[]? _rawImage = null;

    private ObservableAsPropertyHelper<AddonNodeEnableState>? _enableState = null;

    private ObservableAsPropertyHelper<bool>? _shouldShowFolderIcon = null;
    private ObservableAsPropertyHelper<bool>? _shouldShowUnknownImage = null; 
    private ObservableAsPropertyHelper<bool>? _shouldShowImage = null;

    private ObservableAsPropertyHelper<string?>? _fileSizeReadable = null;

    private object? _currentRefreshId = null;
    private CancellationTokenSource? _refreshCts = null;

    public AddonNodeSimpleViewModel(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        _addonRoot = addon.Root;
        Addon = addon;

        ToggleEnabledCommand = ReactiveCommand.Create(() => 
        {
            if (_addon == null)
            {
                return;
            }

            _addon.IsEnabled = !_addon.IsEnabled;
        });

        this.WhenActivated((CompositeDisposable disposables) =>
        {
            _isActive = true;

            {
                var addon = Addon;
                if (addon == null)
                {
                    if (_addonRoot.TryGetDescendantNodeById(LastAddonId, out addon))
                    {
                        Addon = addon;
                    }
                }
            }

            Action<AddonNode> addonRootNewNodeIdRegisteredListener = addon =>
            {
                if (Addon != null)
                {
                    return;
                }

                if (addon.Id == LastAddonId)
                {
                    Addon = addon;
                }
            };
            _addonRoot.NewNodeIdRegistered += addonRootNewNodeIdRegisteredListener;

            var addonObservable = this.WhenAnyValue(x => x.Addon);

            addonObservable.Subscribe(addon =>
            {
                _addonDisposables?.Dispose();

                if (addon == null)
                {
                    _addonDisposables = null;
                }
                else
                {
                    _addonDisposables = new();
                    OnNewAddon(addon, _addonDisposables);
                }
            }).DisposeWith(disposables);

            var hasImageObservable = this.WhenAnyValue(x => x.Image).Select(img => img != null);
            _shouldShowUnknownImage = addonObservable.CombineLatest(hasImageObservable)
                .Select(((AddonNode? Addon, bool HasImage) args) => args.Addon is not null && !args.HasImage && args.Addon is not AddonGroup)
                .ToProperty(this, nameof(ShouldShowUnknownImage));
            _shouldShowImage = addonObservable.CombineLatest(hasImageObservable)
                .Select(((AddonNode? Addon, bool HasImage) args) => args.Addon is not null && args.HasImage)
                .ToProperty(this, nameof(ShouldShowImage));
            _shouldShowFolderIcon = addonObservable.CombineLatest(hasImageObservable)
                .Select(((AddonNode? Addon, bool HasImage) args) => args.Addon is not null && !args.HasImage && args.Addon is AddonGroup)
                .ToProperty(this, nameof(ShouldShowFolderIcon));

            Refresh();

            Disposable.Create(() =>
            {
                _isActive = false;

                _addonRoot.NewNodeIdRegistered -= addonRootNewNodeIdRegisteredListener;

                Utils.DisposeAndSetNull(ref _addonDisposables);
                Utils.DisposeAndSetNull(ref _shouldShowUnknownImage);
                Utils.DisposeAndSetNull(ref _shouldShowImage);
                Utils.DisposeAndSetNull(ref _shouldShowFolderIcon);

                Image = null;

                CancelRefreshTasks();
            }).DisposeWith(disposables);
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public AddonRoot AddonRoot => _addonRoot;

    public AddonNode? Addon
    {
        get 
        {
            if (_addon != null && !_addon.IsValid)
            {
                Addon = null;
            }

            return _addon;
        }
        private set
        {
            if (value == _addon)
            {
                return;
            }
            if (value != null)
            {
                if (value.Root != _addonRoot)
                {
                    throw new ArgumentException("Different AddonRoot instance");
                }
                if (!value.IsValid || !value.GetType().IsAssignableTo(AddonType))
                {
                    value = null;
                }
            }

            _addon = value;
            this.RaisePropertyChanged();
            if (_addon == null)
            {
                OnNullAddon();
            }
        }
    }

    public virtual Type AddonType => typeof(AddonNode);

    public Guid LastAddonId
    {
        get => _lastAddonId;
        private set => this.RaiseAndSetIfChanged(ref _lastAddonId, value);
    }

    public Bitmap? Image
    {
        get => _image;
        private set
        {
            if (value == _image)
            {
                return;
            }
            if (_image != null)
            {
                _image.Dispose();
            }
            _image = value;
            this.RaisePropertyChanged();
        }
    }

    public virtual int ImageDecodingWidth => 50;

    public AddonNodeEnableState EnableState => _enableState?.Value ?? AddonNodeEnableState.Disabled;

    public bool ShouldShowFolderIcon => _shouldShowFolderIcon?.Value ?? false;

    public bool ShouldShowUnknownImage => _shouldShowUnknownImage?.Value ?? false;

    public bool ShouldShowImage => _shouldShowImage?.Value ?? false;

    public string? FileSizeReadable => _fileSizeReadable?.Value;

    public ReactiveCommand<Unit, Unit> ToggleEnabledCommand { get; }

    protected object? CurrentRefreshId => _currentRefreshId;

    public void Refresh()
    {
        if (_isActive)
        {
            CancelRefreshTasks();
            _refreshCts = new();
            var cancellationToken = _refreshCts.Token;
            _currentRefreshId = new();
            OnRefresh(cancellationToken);
        }
    }

    public void ClearCaches()
    {
        Addon?.ClearCaches();
        OnClearCaches();
        Refresh();
    }

    public void Check()
    {
        var addon = Addon;
        if (addon == null)
        {
            return;
        }

        if (addon is AddonGroup addonGroup)
        {
            addonGroup.CheckDescendants();
        }
        addon.Check();

        Refresh();
    }

    public void ShowInFileExplorer()
    {
        var addon = Addon;
        if (addon == null)
        {
            return;
        }

        Utils.ShowFileInExplorer(addon.FullFilePath);
    }

    protected virtual void OnNewAddon(AddonNode addon, CompositeDisposable disposables)
    {
        addon.WhenAnyValue(x => x.Id)
            .Subscribe(id => LastAddonId = id)
            .DisposeWith(disposables);
        addon.WhenAnyValue(x => x.IsValid)
            .Subscribe(isValid =>
            {
                if (!isValid)
                {
                    Addon = null;
                }
            })
            .DisposeWith(disposables);
        _enableState = addon.WhenAnyValue(x => x.IsEnabled, x => x.IsEnabledInHierarchy)
            .Select(((bool IsEnabled, bool IsEnabledInHierarchy) enableState) =>
            {
                if (enableState.IsEnabled)
                {
                    if (enableState.IsEnabledInHierarchy)
                    {
                        return AddonNodeEnableState.Enabled;
                    }
                    else
                    {
                        return AddonNodeEnableState.EnabledSuppressed;
                    }
                }
                else
                {
                    return AddonNodeEnableState.Disabled;
                }
            })
            .ToProperty(this, nameof(EnableState));
        _fileSizeReadable = addon.WhenAnyValue(x => x.FileSize)
            .Select(fileSize => fileSize.HasValue ? Utils.GetReadableBytes(fileSize.Value) : null)
            .ToProperty(this, nameof(FileSizeReadable));
        addon.WhenAnyValue(x => x.CustomImagePath)
            .Skip(1)
            .Throttle(TimeSpan.FromSeconds(0.5))
            .Subscribe(_ => Refresh())
            .DisposeWith(disposables);
        if (addon is VpkAddon vpkAddon)
        {
            vpkAddon.WhenAnyValue(x => x.FullVpkFilePath)
                .Skip(1)
                .Subscribe(_ => Refresh())
                .DisposeWith(disposables);
        }

        Disposable.Create(() =>
        {
            Utils.DisposeAndSetNull(ref _enableState);
            Utils.DisposeAndSetNull(ref _fileSizeReadable);
        }).DisposeWith(disposables);
    }

    protected virtual void OnNullAddon()
    {

    }

    protected virtual void OnRefresh(CancellationToken cancellationToken)
    {
        var addon = Addon;
        var refreshId = CurrentRefreshId;

        RefreshImage();

        async void RefreshImage()
        {
            Image = null;
            _rawImage = null;

            if (addon == null)
            {
                return;
            }

            byte[]? imageData = null;

            try
            {
                var customImagePath = addon.CustomImageFullPath;
                if (customImagePath != null)
                {
                    try
                    {
                        if (File.Exists(customImagePath))
                        {
                            imageData = await File.ReadAllBytesAsync(customImagePath, cancellationToken);
                            if (refreshId != CurrentRefreshId)
                            {
                                return;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception occurred during reading custom image: {FilePath}", customImagePath);
                    }
                }

                if (imageData == null)
                {
                    imageData = await addon.GetImageAllowCacheAsync(cancellationToken);
                    if (refreshId != CurrentRefreshId)
                    {
                        return;
                    }
                    _rawImage = imageData;
                }
            }
            catch (OperationCanceledException) 
            {
                return;
            }

            if (imageData != null)
            {
                try
                {
                    Image = Bitmap.DecodeToWidth(new MemoryStream(imageData), ImageDecodingWidth);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during Bitmap.DecodeToWidth at AddonNodeSimpleViewModel.OnRefresh.");
                }
            }
        }
    }

    protected virtual void OnClearCaches()
    {
        Image = null;
    }

    private void CancelRefreshTasks()
    {
        if (_refreshCts != null)
        {
            _refreshCts.Cancel();
            _refreshCts.Dispose();
            _refreshCts = null;
        }
    }
}
