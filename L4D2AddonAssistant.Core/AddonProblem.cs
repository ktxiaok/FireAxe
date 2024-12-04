using System;

namespace L4D2AddonAssistant
{
    public abstract class AddonProblem
    {
        public AddonProblem(AddonNode source)
        {
            ArgumentNullException.ThrowIfNull(source);
            Source = source;
        }

        public AddonNode Source { get; }

        public virtual bool CanAutoSolve => false;

        public bool TryAutoSolve()
        {
            if (OnAutoSolve())
            {
                Source.RemoveProblem(this);
                return true;
            }
            else
            {
                return false;
            }
        }

        protected virtual bool OnAutoSolve()
        {
            return false;
        }
    }
}
