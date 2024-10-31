using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.ReactiveUI;
using L4D2AddonAssistant.Resources;
using L4D2AddonAssistant.ViewModels;
using MsBox.Avalonia;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;

namespace L4D2AddonAssistant.Views
{
    public partial class AddonNodeExplorerView : ReactiveUserControl<AddonNodeExplorerViewModel>
    {
        private IDisposable? _viewModelDisposable = null;

        public AddonNodeExplorerView()
        {
            this.WhenActivated((CompositeDisposable disposables) =>
            {
                this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Subscribe((viewModel) =>
                {
                    ConnectViewModel(viewModel);
                })
                .DisposeWith(disposables);

                Disposable.Create(() =>
                {
                    DisconnectViewModel();
                })
                .DisposeWith(disposables);
            });

            DoubleTapped += AddonNodeExplorerView_DoubleTapped;
            AddHandler(ListBox.SelectionChangedEvent, AddonNodeExplorerView_SelectionChanged);

            InitializeComponent();
        }

        private void ConnectViewModel(AddonNodeExplorerViewModel viewModel)
        {
            DisconnectViewModel();
            var disposables = new CompositeDisposable();
            _viewModelDisposable = disposables;

            var setSelection = (IEnumerable<AddonNode> nodes) =>
            {
                var listBox = FindActiveListBox();
                if (listBox == null)
                {
                    return;
                }
                var selection = listBox.Selection;
                SelectionModelHelper.Select(selection, nodes, (obj) =>
                {
                    if (obj is AddonNodeSimpleViewModel viewModel)
                    {
                        return viewModel.AddonNode;
                    }
                    return null;
                });
            };
            viewModel.SetSelection += setSelection;

            viewModel.ReportExceptionInteraction.RegisterHandler(async (context) =>
            {
                await CommonMessageBoxes.ShowException(FindWindow(), context.Input);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);

            viewModel.ConfirmDeleteInteraction.RegisterHandler(async (context) =>
            {
                bool retainFile = context.Input;
                string message = Texts.ConfirmDeleteMessage;
                if (retainFile)
                {
                    message += '\n' + Texts.RetainFileMessage;
                }
                bool result = await CommonMessageBoxes.Confirm(FindWindow(), message, Texts.ConfirmDelete);
                context.SetOutput(result);
            }).DisposeWith(disposables);

            viewModel.ReportInvalidMoveInteraction.RegisterHandler(async (context) =>
            {
                string message = string.Format(Texts.CantMoveItemWithName, context.Input) + '\n' + Texts.InvalidMoveMessage;
                var reply = await CommonMessageBoxes.GetErrorOperationReply(FindWindow(), message);
                context.SetOutput(reply);
            }).DisposeWith(disposables);

            viewModel.ReportNameExistsForMoveInteraction.RegisterHandler(async (context) =>
            {
                string message = string.Format(Texts.CantMoveItemWithName, context.Input) + '\n' + Texts.ItemNameExists;
                var reply = await CommonMessageBoxes.GetErrorOperationReply(FindWindow(), message);
                context.SetOutput(reply);
            }).DisposeWith(disposables);

            viewModel.ReportExceptionForMoveInteraction.RegisterHandler(async (context) =>
            {
                var input = context.Input;
                string name = input.Item1;
                Exception ex = input.Item2;
                string exceptionMessage = ObjectExplanationManager.Default.TryGet(ex) ?? ex.ToString();
                string message = string.Format(Texts.CantMoveItemWithName, name) + '\n' + exceptionMessage;
                var reply = await CommonMessageBoxes.GetErrorOperationReply(FindWindow(), message);
                context.SetOutput(reply);
            }).DisposeWith(disposables);

            Disposable.Create(() =>
            {
                viewModel.SetSelection -= setSelection;
            }).DisposeWith(disposables);
        }

        private void DisconnectViewModel()
        {
            if (_viewModelDisposable != null)
            {
                _viewModelDisposable.Dispose();
                _viewModelDisposable = null;
            }
        }

        private Window FindWindow()
        {
            var visualRoot = VisualRoot;
            if (visualRoot == null)
            {
                throw new Exception("VisualRoot is null.");
            }
            return (Window)visualRoot;
        }

        private ListBox? FindActiveListBox()
        {
            return Find(this);
            
            ListBox? Find(ILogical current)
            {
                if (current is ListBox listBox)
                {
                    if (listBox.IsVisible)
                    {
                        return listBox;
                    }
                }
                foreach (var child in current.LogicalChildren)
                {
                    var result = Find(child);
                    if (result != null)
                    {
                        return result;
                    }
                }
                return null;
            }
        }

        private void AddonNodeExplorerView_DoubleTapped(object? sender, TappedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel != null)
            {
                if (e.Source is AddonNodeSimpleView nodeView)
                {
                    var nodeViewModel = nodeView.ViewModel;
                    if (nodeViewModel != null)
                    {
                        var node = nodeViewModel.AddonNode;
                        if (node is AddonGroup group)
                        {
                            viewModel.GotoGroup(group);
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private void AddonNodeExplorerView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel != null)
            {
                if (e.Source is ListBox listBox)
                {
                    viewModel.Selection = listBox.Selection.SelectedItems
                        .Select(item => item as AddonNodeSimpleViewModel)
                        .Where(x => x != null)
                        .ToArray()!;
                }
            }
        }
    }
}
