using System;

namespace FireAxe;

public interface ISaveable
{
    bool RequestSave { get; set; }

    void Save();
}
