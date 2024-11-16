using System;

namespace L4D2AddonAssistant
{
    public class DownloadFailedProblem : AddonProblem
    {
        public DownloadFailedProblem(AddonNode source) : base(source)
        {

        }

        public required string Url { get; init; }

        public required string FilePath { get; init; }

        public Exception? Exception { get; init; } = null;
    }
}
