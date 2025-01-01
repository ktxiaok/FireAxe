using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireAxe.ViewModels
{
    public class AddonNodeExplorerViewModelDesign : AddonNodeExplorerViewModel
    {
        public AddonNodeExplorerViewModelDesign() : base(DesignHelper.CreateTestAddonRoot(), null!)
        {

        }
    }
}
