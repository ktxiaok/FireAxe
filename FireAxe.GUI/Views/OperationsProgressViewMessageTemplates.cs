using System;
using FireAxe.Resources;

namespace FireAxe.Views;

public static class OperationsProgressViewMessageTemplates
{
    public static OperationsProgressViewMessageTemplate Deletion { get; } = new()
    {
        Progress = Texts.OperationsProgress_Deletion_Progress,
        ProgressWithFailure = Texts.OperationsProgress_Deletion_ProgressWithFailure,
        Done = Texts.OperationsProgress_Deletion_Done,
        DoneWithFailure = Texts.OperationsProgress_Deletion_DoneWithFailure,
        OperationSucceeded = Texts.OperationsProgress_Deletion_OperationSucceeded,
        OperationFailed = Texts.OperationsProgress_Deletion_OperationFailed
    };
}