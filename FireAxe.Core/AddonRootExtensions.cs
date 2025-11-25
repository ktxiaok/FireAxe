using System;

namespace FireAxe;

public static class AddonRootExtensions
{
    public static ValidTaskCreator<AddonRoot> GetValidTaskCreator(this AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        return new ValidTaskCreator<AddonRoot>(addonRoot, new TaskFactory(addonRoot.TaskScheduler));
    }
}