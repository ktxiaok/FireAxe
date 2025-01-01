using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FireAxe.Views;

public partial class AddonNodeSectionViewDecorator : UserControl
{
    public AddonNodeSectionViewDecorator()
    {
        InitializeComponent();
    }

    public AddonNodeSectionViewDecorator(Control control) : this()
    {
        decorator.Child = control;
    }
}