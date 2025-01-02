using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FireAxe.Views;

public partial class AddAddonTagWindow : Window
{
    public AddAddonTagWindow()
    {
        InitializeComponent();

        okButton.Click += OkButton_Click;
        cancelButton.Click += CancelButton_Click;
        existingTagsListBox.SelectionChanged += ExistingTagsListBox_SelectionChanged;
    }

    public string Input
    {
        get => inputBox.Text ?? "";
        set => inputBox.Text = value;
    }

    private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(inputBox.Text);
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void ExistingTagsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (existingTagsListBox.SelectedItem is string tag)
        {
            inputBox.Text = tag;
        }
    }
}