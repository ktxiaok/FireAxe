using System;
using System.Collections.Generic;
using System.Text;
using FireAxe.Resources;

namespace FireAxe.ViewModels;

public class AddonImportResultViewModel : ViewModelBase
{
    public AddonImportResultViewModel(AddonRoot.ImportResult importResult)
    {
        ArgumentNullException.ThrowIfNull(importResult);

        ImportResult = importResult;

        var successCount = importResult.SuccessCount;
        if (importResult.HasFailure)
        {
            Message = Texts.ImportCompletedUnsuccessfullyWithCount.FormatNoThrow(successCount, importResult.FailureCount);
        }
        else
        {
            if (successCount == 0)
            {
                Message = Texts.NoItemsToImport;
            }
            else
            {
                Message = Texts.ImportCompletedSuccessfullyWithCount.FormatNoThrow(successCount);
            }
        }

        var detailsBuilder = new StringBuilder();
        foreach (var item in importResult.Items)
        {
            var path = item.RelativeFilePath;
            if (item.NewRelativeFilePath is { } newPath)
            {
                detailsBuilder.AppendLine(Texts.RenamedItemTo.FormatNoThrow(path, newPath));
            }
            if (item.Success)
            {
                detailsBuilder.AppendLine(Texts.ItemImportedSuccessfully.FormatNoThrow(path));
            }
            else
            {
                detailsBuilder.AppendLine(Texts.ItemFailedToImportWithError.FormatNoThrow(path,
                    ObjectExplanationManager.Default.Get(item.Exception)));
            }
        }
        Details = detailsBuilder.ToString();
    }

    public AddonRoot.ImportResult ImportResult { get; }

    public string Message { get; }

    public string Details { get; }
}
