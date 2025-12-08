using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FireAxe.Views;

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

    public static readonly StyledProperty<string?> ValueProperty = 
        AvaloniaProperty.Register<EditableTextBlock, string?>(nameof(Value), defaultValue: null, defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);
    
    public static readonly StyledProperty<string?> DisplayProperty =
        AvaloniaProperty.Register<EditableTextBlock, string?>(nameof(Display), defaultValue: null);
    
    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<EditableTextBlock, string>(nameof(Watermark), defaultValue: "", coerce: CoerceWatermark);

    public static readonly DirectProperty<EditableTextBlock, bool> IsEditingProperty =
        AvaloniaProperty.RegisterDirect<EditableTextBlock, bool>(nameof(IsEditing), t => t.IsEditing);

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
        this.WhenAnyValue(x => x.DataContext)
            .Subscribe(_ => EndEditing());
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set => SetAndRaise(IsEditingProperty, ref _isEditing, value);
    }

    public TextBox? TextBox => _textBox;

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Display
    {
        get => GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public void StartEditing()
    {
        if (IsEditing)
        {
            return;
        }

        UpdateVisibility(true);
        if (_textBox is not null)
        {
            _textBox.Text = Value;
            _textBox.Focus();
            _textBox.SelectAll();
        }
        IsEditing = true;
    }

    public void EndEditing()
    {
        if (!IsEditing)
        {
            return;
        }

        UpdateVisibility(false);
        IsEditing = false;
    }

    public void Submit()
    {
        if (!IsEditing)
        {
            return;
        }

        if (_textBox is not null)
        {
            var oldValue = Value;
            Value = _textBox.Text;
            if (_hasError)
            {
                Value = oldValue;
            }
        }
        if (!_hasError)
        {
            EndEditing();
        }
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

        this.WhenAnyValue(x => x.Value, x => x.Display, (value, display) => display ?? value)
            .Subscribe(text => textBlock.Text = text)
            .DisposeWith(_partDisposables);
        textBox.Bind(TextBox.WatermarkProperty, this.WhenAnyValue(x => x.Watermark))
            .DisposeWith(_partDisposables);
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

        UpdateVisibility(IsEditing);
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            Submit();
        }
        else if (e.Key == Key.Escape)
        {
            EndEditing();
        }
    }

    private void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        StartEditing();
    }

    private void SubmitButton_Click(object? sender, RoutedEventArgs e)
    {
        Submit();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        EndEditing();
    }

    private void UpdateVisibility(bool editing)
    {
        if (editing)
        {
            if (_displayView is not null)
            {
                _displayView.IsVisible = false;
            }
            if (_editView is not null)
            {
                _editView.IsVisible = true;
            }
        }
        else
        {
            if (_displayView is not null)
            {
                _displayView.IsVisible = true;
            }
            if (_editView is not null)
            {
                _editView.IsVisible = false;
            }
            if (_textBox is not null)
            {
                _textBox.Text = "";
                DataValidationErrors.SetError(_textBox, null);
            }
        }
    }

    private static string CoerceWatermark(AvaloniaObject sender, string? value)
    {
        value ??= "";
        return value;
    }
}

internal class EditableTextBlockDesignDataContext : ReactiveObject
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
