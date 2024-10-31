using Avalonia.Controls;
using Avalonia.Controls.Templates;
using L4D2AddonAssistant.Resources;
using System;

namespace L4D2AddonAssistant.DataTemplates
{
    public class ExceptionExplainer : IDataTemplate
    {
        public ExceptionExplanationScene Scene { get; set; } = ExceptionExplanationScene.Default;

        public Control? Build(object? data)
        {
            Exception ex = (Exception)data!;
            string explain = ObjectExplanationManager.Default.TryGet(ex, Scene) ?? Texts.Error;
            return new TextBlock { Text = explain };
        }

        public bool Match(object? data)
        {
            return data is Exception;
        }
    }
}
