using System;

namespace L4D2AddonAssistant
{
    public interface ISaveable
    {
        bool RequestSave { get; set; }

        void Save();
    }
}
