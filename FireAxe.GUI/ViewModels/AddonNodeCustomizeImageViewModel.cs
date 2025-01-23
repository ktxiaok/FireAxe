using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace FireAxe.ViewModels
{
    public class AddonNodeCustomizeImageViewModel : ViewModelBase, IActivatableViewModel
    {
        private AddonNode _addonNode;

        public AddonNodeCustomizeImageViewModel(AddonNode addonNode)
        {
            ArgumentNullException.ThrowIfNull(addonNode);
            _addonNode = addonNode;

            SelectCustomImagePathCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var path = await SelectCustomImagePathInteraction.Handle(Unit.Default);
                if (path == null)
                {
                    return;
                }
                CustomImagePath = path;
            });

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                _addonNode.WhenAnyValue(x => x.CustomImagePath)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(CustomImagePath)))
                .DisposeWith(disposables);
            });
        }

        public ViewModelActivator Activator { get; } = new();

        public AddonNode AddonNode => _addonNode;

        public string CustomImagePath
        {
            get => _addonNode.CustomImagePath ?? "";
            set
            {
                if (value.Length == 0)
                {
                    _addonNode.CustomImagePath = null;
                }
                else
                {
                    _addonNode.CustomImagePath = FileUtils.NormalizePath(value);
                }
            }
        }

        public ReactiveCommand<Unit, Unit> SelectCustomImagePathCommand { get; }

        public Interaction<Unit, string?> SelectCustomImagePathInteraction { get; } = new();
    }
}
