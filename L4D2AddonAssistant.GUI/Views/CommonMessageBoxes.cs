using System;
using System.Threading.Tasks;
using L4D2AddonAssistant.Resources;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using MsBox.Avalonia.Enums;
using Avalonia.Controls;

namespace L4D2AddonAssistant.Views
{
    public static class CommonMessageBoxes
    {
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

        public static async Task<ErrorOperationReply> GetErrorOperationReplyAsync(Window ownerWindow, string message, string? title = null)
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

        public static Task ShowExceptionAsync(Window ownerWindow, Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ownerWindow);
            ArgumentNullException.ThrowIfNull(ex);

            return MessageBoxManager.GetMessageBoxCustom(new MessageBoxCustomParams
            {
                ButtonDefinitions =
                [
                    new ButtonDefinition{ Name = Texts.Ok, IsDefault = true }
                ],
                ContentTitle = Texts.Error,
                ContentMessage = Texts.ExceptionOccurMessage + '\n' + ex.ToString(),
                Icon = Icon.Error,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }).ShowWindowDialogAsync(ownerWindow);
        }
    }
}
