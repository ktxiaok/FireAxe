using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views;

[TemplatePart(TemplatePartName_TextBlock, typeof(TextBlock))]
[TemplatePart(TemplatePartName_EditButton, typeof(Button))]
[TemplatePart(TemplatePartName_TextBox, typeof(TextBox))]
[TemplatePart(TemplatePartName_SubmitButton, typeof(Button))]
[TemplatePart(TemplatePartName_CancelButton, typeof(Button))]
[TemplatePart(TemplatePartName_DisplayView, typeof(Control))]
[TemplatePart(TemplatePartName_EditView, typeof(Control))]
public class EditableTextBlock : TemplatedControl
{
    public const string TemplatePartName_TextBlock = "PART_TextBlock";
    public const string TemplatePartName_EditButton = "PART_EditButton";
    public const string TemplatePartName_TextBox = "PART_TextBox";
    public const string TemplatePartName_SubmitButton = "PART_SubmitButton";
    public const string TemplatePartName_CancelButton = "PART_CancelButton";
    public const string TemplatePartName_DisplayView = "PART_DisplayView";
    public const string TemplatePartName_EditView = "PART_EditView";

    public static readonly StyledProperty<string> ValueProperty = 
        AvaloniaProperty.Register<EditableTextBlock, string>(nameof(Value), defaultValue: "", defaultBindingMode: BindingMode.TwoWay, coerce: CoerceValue,enableDataValidation: true);

    private TextBlock? _textBlock = null;
    private Button? _editButton = null;
    private TextBox? _textBox = null;
    private Button? _submitButton = null;
    private Button? _cancelButton = null;
    private Control? _displayView = null;
    private Control? _editView = null;
    private CompositeDisposable? _partDisposables = null;

    private bool _isEditing = false;

    private bool _hasError = false;

    public EditableTextBlock()
    {
        this.GetObservable(DataContextProperty).Subscribe((dataContext) =>
        {
            _isEditing = false;
            UpdateVisibility();
        });
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _partDisposables?.Dispose();
        _partDisposables = new();

        var nameScope = e.NameScope;
        var textBlock = nameScope.Get<TextBlock>(TemplatePartName_TextBlock);
        var editButton = nameScope.Get<Button>(TemplatePartName_EditButton);
        var textBox = nameScope.Get<TextBox>(TemplatePartName_TextBox);
        var submitButton = nameScope.Get<Button>(TemplatePartName_SubmitButton);
        var cancelButton = nameScope.Get<Button>(TemplatePartName_CancelButton);
        var displayView = nameScope.Get<Control>(TemplatePartName_DisplayView);
        var editView = nameScope.Get<Control>(TemplatePartName_EditView);

        textBlock.Bind(TextBlock.TextProperty, this.GetObservable(ValueProperty)).DisposeWith(_partDisposables);
        editButton.Click += EditButton_Click;
        submitButton.Click += SubmitButton_Click;
        cancelButton.Click += CancelButton_Click;
        Disposable.Create(() =>
        {
            editButton.Click -= EditButton_Click;
            submitButton.Click -= SubmitButton_Click;
            cancelButton.Click -= CancelButton_Click;
        }).DisposeWith(_partDisposables);

        _textBlock = textBlock;
        _editButton = editButton;
        _textBox = textBox;
        _submitButton = submitButton;
        _cancelButton = cancelButton;
        _displayView = displayView;
        _editView = editView;

        UpdateVisibility();
    }

    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        base.UpdateDataValidation(property, state, error);

        if (property == ValueProperty)
        {
            if (_textBox != null)
            {
                DataValidationErrors.SetError(_textBox, error);
            }
            _hasError = error != null;
        }
    }

    private void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        _isEditing = true;
        if (_textBlock != null && _textBox != null)
        {
            _textBox.Text = _textBlock.Text;
        }
        UpdateVisibility();
    }

    private void SubmitButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_textBox != null)
        {
            Value = _textBox.Text!;
        }
        if (!_hasError)
        {
            _isEditing = false;
            UpdateVisibility();
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        _isEditing = false;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_isEditing)
        {
            if (_displayView != null)
            {
                _displayView.IsVisible = false;
            }
            if (_editView != null)
            {
                _editView.IsVisible = true;
            }
        }
        else
        {
            if (_displayView != null)
            {
                _displayView.IsVisible = true;
            }
            if (_editView != null)
            {
                _editView.IsVisible = false;
            }
            if (_textBox != null)
            {
                _textBox.Text = "";
            }
        }
    }

    private static string CoerceValue(AvaloniaObject sender, string? value)
    {
        if (value == null)
        {
            return "";
        }
        return value;
    }
}

public class EditableTextBlockDesignDataContext : ReactiveObject
{
    private string _value = "test";

    public string Value
    {
        get => _value;
        set
        {
            if (value.Length == 0)
            {
                throw new ArgumentException("The value can't be empty");
            }
            _value = value;
            this.RaisePropertyChanged();
        }
    }
}
