using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireAxe;

public enum AddonFileTypeMismatch
{
    None,
    ShouldBeDirectory,
    ShouldBeFile,
}

public class AddonFileMissingProblem : AddonProblem
{
    public AddonFileMissingProblem(AddonNode addon, string filePath) : base(addon)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        FilePath = filePath;
    }

    public string FilePath { get; }

    public AddonFileTypeMismatch FileTypeMismatch { get; init; } = AddonFileTypeMismatch.None;
}
