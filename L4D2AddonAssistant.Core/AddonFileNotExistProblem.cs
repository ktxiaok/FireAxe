using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace L4D2AddonAssistant
{
    public class AddonFileNotExistProblem : AddonProblem
    {
        private string _filePath;

        public AddonFileNotExistProblem(AddonNode source) : base(source)
        {
            _filePath = source.FilePath;
        }

        public string FilePath => _filePath;
    }
}
