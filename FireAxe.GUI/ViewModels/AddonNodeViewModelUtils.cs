using System;
using System.Reactive.Linq;
using ReactiveUI;

namespace FireAxe.ViewModels;

public static class AddonNodeViewModelUtils
{
    public const string
        DefaultShouldShowUnknownImagePropertyName = "ShouldShowUnknownImage",
        DefaultShouldShowImagePropertyName = "ShouldShowImage",
        DefaultShouldShowFolderIconPropertyName = "ShouldShowFolderIcon";

    public static void CreateIconShowingObservableProperties<TSource>(
        TSource source, IObservable<AddonNode?> addon, IObservable<bool> hasImage,
        out ObservableAsPropertyHelper<bool> shouldShowUnknownImage, out ObservableAsPropertyHelper<bool> shouldShowImage, out ObservableAsPropertyHelper<bool> shouldShowFolderIcon
        string? shouldShowUnknownImagePropertyName = null, string? shouldShowImagePropertyName = null, string? shouldShowFolderIconPropertyName = null)
        where TSource : class, IReactiveObject
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(addon);
        ArgumentNullException.ThrowIfNull(hasImage);
        shouldShowUnknownImagePropertyName ??= DefaultShouldShowUnknownImagePropertyName;
        shouldShowImagePropertyName ??= DefaultShouldShowImagePropertyName;
        shouldShowFolderIconPropertyName ??= DefaultShouldShowFolderIconPropertyName;

        shouldShowUnknownImage = addon.CombineLatest(hasImage)
            .Select(((AddonNode? Addon, bool HasImage) args) => args.Addon is not null && !args.HasImage && args.Addon is not AddonGroup)
            .ToProperty(source, shouldShowUnknownImagePropertyName);
        shouldShowImage = addon.CombineLatest(hasImage)
            .Select(((AddonNode? Addon, bool HasImage) args) => args.Addon is not null && args.HasImage)
            .ToProperty(source, shouldShowImagePropertyName);
        shouldShowFolderIcon = addon.CombineLatest(hasImage)
            .Select(((AddonNode? Addon, bool HasImage) args) => args.Addon is not null && !args.HasImage && args.Addon is AddonGroup)
            .ToProperty(source, shouldShowFolderIconPropertyName);
    }
}