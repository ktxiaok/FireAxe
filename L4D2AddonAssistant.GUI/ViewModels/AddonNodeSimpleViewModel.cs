using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonNodeSimpleViewModel : ViewModelBase, IActivatableViewModel
    {
        private const int ImageWidthToDecode = 200;

        private readonly AddonNode _addonNode;

        private Bitmap? _image = null;

        private ObservableAsPropertyHelper<AddonNodeEnableState>? _enableState = null;

        private readonly bool _shouldShowFolderIcon;
        private ObservableAsPropertyHelper<bool>? _shouldShowUnknownImage = null; 
        private ObservableAsPropertyHelper<bool>? _shouldShowImage = null;

        private CancellationTokenSource? _cancellationTokenSource = null;

        public AddonNodeSimpleViewModel(AddonNode addonNode)
        {
            _addonNode = addonNode;
            Activator = new();
            _shouldShowFolderIcon = _addonNode is AddonGroup;

            ToggleEnabledCommand = ReactiveCommand.Create(() => 
            {
                _addonNode.IsEnabled = !_addonNode.IsEnabled;
            });

            this.WhenActivated((CompositeDisposable disposables) =>
            {
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

                _shouldShowUnknownImage = (_shouldShowFolderIcon ?
                Observable.Return(false) :
                this.WhenAnyValue(x => x.Image)
                .Select(image => image == null))
                .ToProperty(this, nameof(ShouldShowUnknownImage));

                _shouldShowImage = (_shouldShowFolderIcon ?
                    Observable.Return(false) :
                    this.WhenAnyValue(x => x.Image)
                    .Select(image => image != null))
                    .ToProperty(this, nameof(ShouldShowImage));

                Refresh();

                Disposable.Create(() =>
                {
                    _enableState.Dispose();
                    _enableState = null;

                    _shouldShowUnknownImage.Dispose();
                    _shouldShowUnknownImage = null;

                    _shouldShowImage.Dispose();
                    _shouldShowImage = null;

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

        public bool ShouldShowFolderIcon => _shouldShowFolderIcon;

        public bool ShouldShowUnknownImage => _shouldShowUnknownImage?.Value ?? false;

        public bool ShouldShowImage => _shouldShowImage?.Value ?? false;

        public ReactiveCommand<Unit, Unit> ToggleEnabledCommand { get; }

        public virtual async void Refresh(bool hard = false)
        {
            CancelTasks();
            _cancellationTokenSource = new();
            var cancellationToken = _cancellationTokenSource.Token;

            byte[]? imageData = null;
            Task<byte[]?>? getImageTask = null;
            
            if (!hard)
            {
                imageData = _addonNode.ImageCache;
            }
            if (imageData == null)
            {
                getImageTask = _addonNode.GetImageAsync(cancellationToken);
            }

            if (getImageTask != null)
            {
                try
                {
                    imageData = await getImageTask;
                }
                catch (OperationCanceledException)
                {

                }
            }

            if (imageData != null)
            {
                Image = Bitmap.DecodeToWidth(new MemoryStream(imageData), ImageWidthToDecode);
            }
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
    }
}
