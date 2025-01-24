using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FireAxe.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;
using System;
using Avalonia.Platform.Storage;
using System.IO;
using Serilog;

namespace FireAxe.Views;

public partial class AddonNodeCustomizeImageWindow : ReactiveWindow<AddonNodeCustomizeImageViewModel>
{
    public AddonNodeCustomizeImageWindow()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(viewModel =>
            {
                viewModel.SelectCustomImagePathInteraction.RegisterHandler(async (context) =>
                {
                    var storage = StorageProvider;
                    var addonRootDirectoryPath = viewModel.AddonNode.Root.DirectoryPath;
                    var filePickerOptions = new FilePickerOpenOptions()
                    {
                        AllowMultiple = false,
                        SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(addonRootDirectoryPath),
                        FileTypeFilter = [FilePickerFileTypes.ImageJpg, FilePickerFileTypes.ImagePng]
                    };
                    var pickedFiles = await storage.OpenFilePickerAsync(filePickerOptions);
                    string? result = null;
                    if (pickedFiles != null && pickedFiles.Count == 1)
                    {
                        var pickedUri = pickedFiles[0].Path;
                        if (pickedUri.IsFile)
                        {
                            var pickedPath = pickedUri.LocalPath;
                            try
                            {
                                result = FileUtils.NormalizePath(Path.GetRelativePath(addonRootDirectoryPath, pickedPath));
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Exception occurred during getting relative path of picked file: {FilePath}", pickedPath);
                            }
                        }
                    }
                    context.SetOutput(result);
                });
            });
    }
}