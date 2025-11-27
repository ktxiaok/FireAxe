using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireAxe;

public enum AddonFileFormatProblemType
{
    None,
    ShouldBeDirectory,
    ShouldBeFile,
}

public class AddonFileNotExistProblem : AddonProblem
{
    public AddonFileNotExistProblem(AddonProblemSource problemSource) : base(problemSource)
    {
        FilePath = Addon.FullFilePath;
    }

    public string FilePath { get; }

    public AddonFileFormatProblemType FormatProblemType { get; init; } = AddonFileFormatProblemType.None;
}
