using MsBox.Avalonia;
using System;

namespace L4D2AddonAssistant
{
    internal class MessageBoxService : IMessageBoxService
    {
        public void ShowInfo(string message)
        {
            MessageBoxManager.GetMessageBoxStandard("", message, MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.None).ShowAsync();
        }
    }
}
