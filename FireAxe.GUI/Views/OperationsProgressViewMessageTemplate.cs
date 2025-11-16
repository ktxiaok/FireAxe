using System;

namespace FireAxe.Views;

public class OperationsProgressViewMessageTemplate
{
    public static OperationsProgressViewMessageTemplate Default { get; } = new()
    {
        Progress = "Operations are currently underway... ({0} / {1})",
        ProgressWithFailure = "Operations are currently underway... ({0} / {1}, {2} failures)",
        Done = "Done. {0} operations have been completed successfully.",
        DoneWithFailure = "Done. {0} operations have been completed successfully and {1} operations have failed.",
        OperationSucceeded = "Operation Succeeded: {0}",
        OperationFailed = "Operation Failed: {0}, Failure: {1}"
    };

    public required string Progress { get; init; }

    public required string ProgressWithFailure { get; init; }

    public required string Done { get; init; }

    public required string DoneWithFailure { get; init; }

    public required string OperationSucceeded { get; init; }

    public required string OperationFailed { get; init; }
}