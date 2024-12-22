using System;
using System.Threading.Tasks;
using L4D2AddonAssistant.Resources;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using MsBox.Avalonia.Enums;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace L4D2AddonAssistant.Views
{
    public static class CommonMessageBoxes
    {
        public static async Task<string?> ChooseDirectory(Window ownerWindow)
        {
            ArgumentNullException.ThrowIfNull(ownerWindow);

            var storage = ownerWindow.StorageProvider;
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            var folders = await storage.OpenFolderPickerAsync(options);
            if (folders.Count == 1)
            {
                var folder = folders[0];
                var path = folder.Path;
                if (path.IsFile)
                {
                    return path.LocalPath;
                }
            }

            return null;
        }

        public static async Task<bool> Confirm(Window ownerWindow, string message, string title)
        {
            ArgumentNullException.ThrowIfNull(ownerWindow);
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(title);

            var textYes = Texts.Yes;
            var textNo = Texts.No;
            var result = await MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
            {
                ButtonDefinitions =
                [
                    new ButtonDefinition { Name = textYes },
                    new ButtonDefinition { Name = textNo, IsDefault = true }
                ],
                ContentMessage = message,
                ContentTitle = title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }).ShowWindowDialogAsync(ownerWindow);
            if (result == textYes)
            {
                return true;
            }
            else
            {
                return false;
            }
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

        public static Task ShowInfo(Window ownerWindow, string message, string title)
        {
            ArgumentNullException.ThrowIfNull(ownerWindow);
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(title);

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
}
