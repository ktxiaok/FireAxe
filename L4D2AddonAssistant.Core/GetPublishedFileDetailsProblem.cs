using System;

namespace L4D2AddonAssistant
{
    public class GetPublishedFileDetailsProblem : AddonProblem
    {
        public GetPublishedFileDetailsProblem(AddonNode source, bool isInvalidPublishedFileId) : base(source)
        {
            IsInvalidPublishedFileId = isInvalidPublishedFileId;
        }

        public bool IsInvalidPublishedFileId { get; }
    }
}
