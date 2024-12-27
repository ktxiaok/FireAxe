using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace L4D2AddonAssistant.Views;

public partial class AddonTagCheckBox : UserControl
{
    public AddonTagCheckBox()
    {
        InitializeComponent();
    }

    public bool IsChecked
    {
        get => checkBox.IsChecked ?? false;
        set => checkBox.IsChecked = value;
    }
}