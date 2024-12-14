using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace L4D2AddonAssistant.ViewModels
{
    public class AddonNodeExplorerViewModelDesign : AddonNodeExplorerViewModel
    {
        public AddonNodeExplorerViewModelDesign() : base(DesignHelper.CreateTestAddonRoot(), null!)
        {

        }
    }
}
