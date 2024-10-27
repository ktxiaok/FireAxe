using System;

namespace L4D2AddonAssistant
{
    public abstract class AddonProblem
    {
        protected AddonProblem(AddonNode source)
        {
            ArgumentNullException.ThrowIfNull(source);
            Source = source;
        }

        public AddonNode Source { get; }

        public abstract bool TrySolve();
    }
}
