using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;

namespace FireAxe.Views;

public partial class AddonIconView : ReactiveUserControl<AddonNodeSimpleViewModel>
{
    public static readonly StyledProperty<double> ImageSizeProperty =
        AvaloniaProperty.Register<AddonIconView, double>(nameof(ImageSize), defaultValue: 50);

    public static readonly StyledProperty<double> SpecialIconSizeProperty =
        AvaloniaProperty.Register<AddonIconView, double>(nameof(SpecialIconSize), defaultValue: 50);

    public AddonIconView()
    {
        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        InitializeComponent();

        var imageSizeObservable = this.WhenAnyValue(x => x.ImageSize);
        imageSizeObservable.BindTo(image, x => x.Width);
        imageSizeObservable.BindTo(image, x => x.Height);
        var specialIconSizeObservable = this.WhenAnyValue(x => x.SpecialIconSize);
        specialIconSizeObservable.BindTo(unknownIcon, x => x.Width);
        specialIconSizeObservable.BindTo(unknownIcon, x => x.Height);
        specialIconSizeObservable.BindTo(folderIcon, x => x.Width);
        specialIconSizeObservable.BindTo(folderIcon, x => x.Height);
    }

    public double ImageSize
    {
        get => GetValue(ImageSizeProperty);
        set => SetValue(ImageSizeProperty, value);
    }

    public double SpecialIconSize
    {
        get => GetValue(SpecialIconSizeProperty);
        set => SetValue(SpecialIconSizeProperty, value);
    }
}