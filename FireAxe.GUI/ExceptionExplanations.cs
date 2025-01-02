using FireAxe.Resources;
using System;

namespace FireAxe
{
    internal static class ExceptionExplanations
    {
        public static void Register(ObjectExplanationManager manager)
        {
            manager.Register<Exception>((exception, arg) =>
            {
                if (arg is ExceptionExplanationScene scene)
                {
                    if (scene == ExceptionExplanationScene.Input)
                    {
                        return Texts.InvalidInputMessage;
                    }
                }
                return Texts.ExceptionOccurMessage + '\n' + exception.ToString();
            });
            manager.Register<AddonNameExistsException>((exception, arg) => Texts.ItemNameExists);
            manager.Register<AddonNodeMoveDeniedException>((exception, arg) => string.Format(Texts.AddonMoveDeniedMessage, exception.AddonNode.FullName));
        }
    }
}
