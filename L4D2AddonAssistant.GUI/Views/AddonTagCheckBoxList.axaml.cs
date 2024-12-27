using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using L4D2AddonAssistant.ViewModels;
using System.Collections.Generic;

namespace L4D2AddonAssistant.Views;

public partial class AddonTagCheckBoxList : UserControl
{
    public AddonTagCheckBoxList()
    {
        InitializeComponent();

        allButton.Click += AllButton_Click;
        noneButton.Click += NoneButton_Click;
        checkBoxesControl.AddHandler(CheckBox.IsCheckedChangedEvent, CheckBoxesControl_IsCheckedChanged);
    }

    private void AllButton_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var control in checkBoxesControl.GetLogicalDescendants())
        {
            if (control is AddonTagCheckBox tagCheckBox)
            {
                tagCheckBox.IsChecked = true;
            }
        }
    }

    private void NoneButton_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var control in checkBoxesControl.GetLogicalDescendants())
        {
            if (control is AddonTagCheckBox tagCheckBox)
            {
                tagCheckBox.IsChecked = false;
            }
        }
    }

    private void CheckBoxesControl_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as AddonNodeExplorerViewModel;
        if (viewModel == null)
        {
            return;
        }

        var selectedTags = new List<string>();
        foreach (var control in checkBoxesControl.GetLogicalDescendants())
        {
            if (control is AddonTagCheckBox tagCheckBox)
            {
                if (tagCheckBox.IsChecked)
                {
                    if (tagCheckBox.DataContext is string tag)
                    {
                        selectedTags.Add(tag);
                    }
                }
            }
        }
        viewModel.SelectedTags = selectedTags;
    }
}