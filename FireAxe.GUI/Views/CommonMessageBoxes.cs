using System;
using System.Threading.Tasks;
using FireAxe.Resources;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using MsBox.Avalonia.Enums;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.IO;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace FireAxe.Views;

public static class CommonMessageBoxes
{
    public static Task<IReadOnlyList<string>> ChooseDirectories(Window ownerWindow, ChooseDirectoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(options);

        return ChooseDirectories(ownerWindow, options, true);
    }

    public static async Task<string?> ChooseDirectory(Window ownerWindow, ChooseDirectoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(options);

        var result = await ChooseDirectories(ownerWindow, options, false);
        if (result.Count == 1)
        {
            return result[0];
        }
        return null;
    }

    private static async Task<IReadOnlyList<string>> ChooseDirectories(Window ownerWindow, ChooseDirectoryOptions options, bool allowMultiple)
    {
        var storage = ownerWindow.StorageProvider;
        var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = options.Title,
            SuggestedStartLocation = options.StartDirectoryPath is null ? null : await TryGetExistingStorageFolderAsync(options.StartDirectoryPath, storage),
            SuggestedFileName = options.SuggestedFileName,
            AllowMultiple = allowMultiple
        });
        return GetPath(result);
    }

    public static Task<IReadOnlyList<string>> ChooseFiles(Window ownerWindow, ChooseFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(options);

        return ChooseFiles(ownerWindow, options, true);
    }

    public static async Task<string?> ChooseFile(Window ownerWindow, ChooseFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(options);

        var result = await ChooseFiles(ownerWindow, options, false);
        if (result.Count == 1)
        {
            return result[0];
        }
        return null;
    }

    private static async Task<IReadOnlyList<string>> ChooseFiles(Window ownerWindow, ChooseFileOptions options, bool allowMultiple)
    {
        var storage = ownerWindow.StorageProvider;
        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = options.Title,
            SuggestedStartLocation = options.StartDirectoryPath is null ? null : await TryGetExistingStorageFolderAsync(options.StartDirectoryPath, storage),
            SuggestedFileName = options.SuggestedFileName,
            AllowMultiple = allowMultiple,
            FileTypeFilter = options.FilePatterns?.Select(pattern => new FilePickerFileType(null) { Patterns = [pattern] }).ToArray() 
        });
        return GetPath(result);
    }

    public static async Task<string?> SaveFile(Window ownerWindow, SaveFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(options);

        var storage = ownerWindow.StorageProvider;
        var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = options.Title,
            SuggestedStartLocation = options.StartDirectoryPath is null ? null : await TryGetExistingStorageFolderAsync(options.StartDirectoryPath, storage),
            SuggestedFileName = options.SuggestedFileName,
            FileTypeChoices = options.FilePatterns?.Select(pattern => new FilePickerFileType(null) { Patterns = [pattern] }).ToArray(),
            DefaultExtension = options.DefaultFileExtension,
            ShowOverwritePrompt = true
        });
        if (result is not null && result.Path.IsFile)
        {
            return result.Path.LocalPath;
        }
        return null;
    }

    private static IReadOnlyList<string> GetPath(IEnumerable<IStorageItem> storageItems)
    {
        return storageItems.Where(storageFolder => storageFolder.Path.IsFile)
            .Select(storageFolder => storageFolder.Path.LocalPath)
            .ToArray();
    }

    private static async Task<IStorageFolder?> TryGetExistingStorageFolderAsync(string path, IStorageProvider storage)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return await storage.TryGetFolderFromPathAsync(path);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception occurred during {nameof(CommonMessageBoxes)}.{nameof(TryGetExistingStorageFolderAsync)}.");
        }
        return null;
    }

    public static async Task<bool> Confirm(Window ownerWindow, string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(message);
        title ??= Texts.Confirm;  

        var result = await MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams()
        {
            ContentTitle = title,
            ContentMessage = message,
            ButtonDefinitions = ButtonEnum.YesNo,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            EnterDefaultButton = ClickEnum.Yes
        }).ShowWindowDialogAsync(ownerWindow);
        return result == ButtonResult.Yes;
    }

    public static async Task<ErrorOperationReply> GetErrorOperationReply(Window ownerWindow, string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(message);

        if (title == null)
        {
            title = Texts.Error;
        }

        var textSkip = Texts.Skip;
        var textSkipAll = Texts.SkipAll;
        var textAbort = Texts.Abort;
        var result = await MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
        {
            ButtonDefinitions =
            [
                new ButtonDefinition{ Name = textSkip, IsDefault = true },
                new ButtonDefinition{ Name = textSkipAll },
                new ButtonDefinition{ Name = textAbort },
            ],
            ContentTitle = title,
            ContentMessage = message,
            Icon = Icon.Error,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        }).ShowWindowDialogAsync(ownerWindow);
        if (result == textSkipAll)
        {
            return ErrorOperationReply.SkipAll;
        }
        else if (result == textAbort)
        {
            return ErrorOperationReply.Abort;
        }
        else
        {
            return ErrorOperationReply.Skip;
        }
    }

    public static Task ShowException(Window ownerWindow, Exception ex, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(ex);

        string exceptionMessage = ObjectExplanationManager.Default.TryGet(ex) ?? ex.ToString();
        if (message == null)
        {
            message = exceptionMessage;
        }
        else
        {
            message = message + '\n' + exceptionMessage;
        }
        return MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
        {
            ButtonDefinitions =
            [
                new ButtonDefinition{ Name = Texts.Ok, IsDefault = true }
            ],
            ContentTitle = Texts.Error,
            ContentMessage = message,
            Icon = Icon.Error,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        }).ShowWindowDialogAsync(ownerWindow);
    }

    public static Task ShowInfo(Window ownerWindow, string message, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(message);
        title ??= "";

        return MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
        {
            ButtonDefinitions =
            [
                new ButtonDefinition { Name = Texts.Ok, IsDefault = true }
            ],
            ContentTitle = title,
            ContentMessage = message,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        }).ShowWindowDialogAsync(ownerWindow);
    }

    public static async Task<string?> Input(Window ownerWindow, string message, string title, string defaultValue = "")
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(defaultValue);

        var msgbox = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams()
        {
            InputParams = new InputParams()
            {
                DefaultValue = defaultValue
            },
            ButtonDefinitions = ButtonEnum.OkCancel,
            EnterDefaultButton = ClickEnum.Ok,
            EscDefaultButton = ClickEnum.Cancel,
            ContentMessage = message,
            ContentTitle = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        });
        var result = await msgbox.ShowWindowDialogAsync(ownerWindow);
        if (result == ButtonResult.Ok)
        {
            return msgbox.InputValue;
        }
        else
        {
            return null;
        }
    }
}

public abstract class FileSystemPickerOptions
{
    public string? Title { get; set; } = null;

    public string? StartDirectoryPath { get; set; } = null;

    public string? SuggestedFileName { get; set; } = null;
}

public class ChooseDirectoryOptions : FileSystemPickerOptions
{

}

public class ChooseFileOptions : FileSystemPickerOptions
{
    public IReadOnlyList<string>? FilePatterns { get; set; } = null; 
}

public class SaveFileOptions : FileSystemPickerOptions
{
    public IReadOnlyList<string>? FilePatterns { get; set; } = null;

    public string? DefaultFileExtension { get; set; } = null;
}