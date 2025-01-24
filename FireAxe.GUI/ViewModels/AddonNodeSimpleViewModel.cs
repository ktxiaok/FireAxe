using Avalonia.Media.Imaging;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels
{
    public class AddonNodeSimpleViewModel : ViewModelBase, IActivatableViewModel
    {
        private const int ImageWidthToDecode = 200;

        private bool _isActive = false;

        private readonly AddonNode _addonNode;

        private Bitmap? _image = null;
        private byte[]? _rawImage = null;

        private ObservableAsPropertyHelper<AddonNodeEnableState>? _enableState = null;

        private bool _hasProblem = false;

        private ObservableAsPropertyHelper<bool>? _shouldShowFolderIcon = null;
        private ObservableAsPropertyHelper<bool>? _shouldShowUnknownImage = null; 
        private ObservableAsPropertyHelper<bool>? _shouldShowImage = null;

        private ObservableAsPropertyHelper<string?>? _fileSizeReadable = null;

        private CancellationTokenSource? _cancellationTokenSource = null;

        public AddonNodeSimpleViewModel(AddonNode addonNode)
        {
            _addonNode = addonNode;
            Activator = new();

            ToggleEnabledCommand = ReactiveCommand.Create(() => 
            {
                _addonNode.IsEnabled = !_addonNode.IsEnabled;
            });

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                _isActive = true;

                var addon = AddonNode;

                UpdateHasProblem();
                NotifyCollectionChangedEventHandler problemsSubscription = (object? sender, NotifyCollectionChangedEventArgs e) =>
                {
                    UpdateHasProblem();
                };
                ((INotifyCollectionChanged)addon.Problems).CollectionChanged += problemsSubscription;
                

                _enableState = _addonNode.WhenAnyValue(x => x.IsEnabled, x => x.IsEnabledInHierarchy)
                .Select(((bool isEnabled, bool isEnabledInHierarchy) enableState) =>
                {
                    if (enableState.isEnabled)
                    {
                        if (enableState.isEnabledInHierarchy)
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

                _shouldShowFolderIcon = this.WhenAnyValue(x => x.Image)
                .Select(image => image == null && _addonNode is AddonGroup)
                .ToProperty(this, nameof(ShouldShowFolderIcon));

                _shouldShowUnknownImage = this.WhenAnyValue(x => x.Image)
                .Select(image => image == null && _addonNode is not AddonGroup)
                .ToProperty(this, nameof(ShouldShowUnknownImage));

                _shouldShowImage = this.WhenAnyValue(x => x.Image)
                    .Select(image => image != null)
                    .ToProperty(this, nameof(ShouldShowImage));

                _fileSizeReadable = this.WhenAnyValue(x => x.AddonNode.FileSize)
                .Select((fileSize) => fileSize.HasValue ? Utils.GetReadableBytes(fileSize.Value) : null)
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

                Refresh();

                Disposable.Create(() =>
                {
                    _isActive = false;

                    ((INotifyCollectionChanged)addon.Problems).CollectionChanged -= problemsSubscription;

                    _enableState.Dispose();
                    _enableState = null;

                    _shouldShowFolderIcon.Dispose();
                    _shouldShowFolderIcon = null;

                    _shouldShowUnknownImage.Dispose();
                    _shouldShowUnknownImage = null;

                    _shouldShowImage.Dispose();
                    _shouldShowImage = null;

                    _fileSizeReadable.Dispose();
                    _fileSizeReadable = null;

                    Image = null;

                    CancelTasks();
                })
                .DisposeWith(disposables);
            });
        }

        public ViewModelActivator Activator { get; }

        public AddonNode AddonNode => _addonNode;

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

        public AddonNodeEnableState EnableState => _enableState?.Value ?? AddonNodeEnableState.Disabled;

        public bool HasProblem
        {
            get => _hasProblem;
            private set => this.RaiseAndSetIfChanged(ref _hasProblem, value);
        }

        public bool ShouldShowFolderIcon => _shouldShowFolderIcon?.Value ?? false;

        public bool ShouldShowUnknownImage => _shouldShowUnknownImage?.Value ?? false;

        public bool ShouldShowImage => _shouldShowImage?.Value ?? false;

        public string? FileSizeReadable => _fileSizeReadable?.Value;

        public ReactiveCommand<Unit, Unit> ToggleEnabledCommand { get; }

        public void Refresh()
        {
            if (_isActive)
            {
                OnRefresh();
            }
        }

        public void ClearCaches()
        {
            AddonNode.ClearCaches();
            OnClearCaches();
            Refresh();
        }

        public void Check()
        {
            if (AddonNode is AddonGroup addonGroup)
            {
                addonGroup.CheckAll();
            }
            AddonNode.Check();
            Refresh();
        }

        public void ShowInFileExplorer()
        {
            Utils.ShowFileInExplorer(AddonNode.FullFilePath);
        }

        protected virtual void OnRefresh()
        {
            CancelTasks();
            _cancellationTokenSource = new();
            var cancellationToken = _cancellationTokenSource.Token;

            var addon = AddonNode;

            RefreshImage();

            async void RefreshImage()
            {
                Image = null;
                _rawImage = null;

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
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception occurred during reading custom image: {FilePath}", customImagePath);
                        }
                    }

                    if (imageData == null)
                    {
                        imageData = await addon.GetImageAllowCacheAsync(cancellationToken);
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
                        Image = Bitmap.DecodeToWidth(new MemoryStream(imageData), ImageWidthToDecode);
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

        private void CancelTasks()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void UpdateHasProblem()
        {
            HasProblem = AddonNode.Problems.Count > 0;
        }
    }
}
