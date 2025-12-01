using System;

namespace FireAxe;

public class AddonDownloadFailedProblem : AddonProblem
{
    public AddonDownloadFailedProblem(AddonNode addon) : base(addon)
    {

    }

    public required string Url { get; init; }

    public required string FilePath { get; init; }

    public Exception? Exception { get; init; } = null;
}
