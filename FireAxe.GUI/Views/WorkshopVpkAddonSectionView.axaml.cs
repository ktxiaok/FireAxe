using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FireAxe.ViewModels;
using FireAxe.Resources;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI.Avalonia;
using System.Reactive.Disposables.Fluent;

namespace FireAxe.Views;

public partial class WorkshopVpkAddonSectionView : ReactiveUserControl<WorkshopVpkAddonViewModel>
{
    public WorkshopVpkAddonSectionView()
    {
        InitializeComponent();

        this.WhenActivated((CompositeDisposable disposables) =>
        {

        });

        this.RegisterViewModelConnection((viewModel, disposables) =>
        {
            viewModel.ShowExceptionInteraction.RegisterHandler(async context =>
            {
                await CommonMessageBoxes.ShowException(this.GetRootWindow(), context.Input);
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
            viewModel.ConfirmDeleteRedundantVpkFilesInteraction.RegisterHandler(async context =>
            {
                var report = context.Input;
                if (report.IsEmpty)
                {
                    await CommonMessageBoxes.ShowInfo(this.GetRootWindow(), Texts.WorkshopVpkAddonDeleteRedundantVpkFilesReportEmptyMessage);
                    context.SetOutput(false);
                    return;
                }
                bool confirm = await CommonMessageBoxes.Confirm(this.GetRootWindow(),
                    Texts.WorkshopVpkAddonDeleteRedundantVpkFilesReportConfirmMessage.FormatNoThrow(report.Files.Count, Utils.GetReadableBytes(report.TotalFileSize)));
                context.SetOutput(confirm);
            }).DisposeWith(disposables);
            viewModel.ShowDeleteRedundantVpkFilesSuccessInteraction.RegisterHandler(async context =>
            {
                var report = context.Input;
                await CommonMessageBoxes.ShowInfo(this.GetRootWindow(), 
                    Texts.WorkshopVpkAddonDeleteRedundantVpkFilesReportSuccessMessage.FormatNoThrow(report.Files.Count, Utils.GetReadableBytes(report.TotalFileSize)));
                context.SetOutput(Unit.Default);
            }).DisposeWith(disposables);
        });
    }
}