using FireAxe.Resources;
using System;

namespace FireAxe;

public static class ExceptionExplanations
{
    public static void Register(ObjectExplanationManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        manager.Register<Exception>((exception, arg) =>
        {
            if (arg is ExceptionExplanationScene scene)
            {
                if (scene == ExceptionExplanationScene.Input)
                {
                    return $"{Texts.InvalidInputMessage} ({exception.GetType().Name})";
                }
            }
            return Texts.ExceptionOccurMessage + '\n' + exception.ToString();
        });
        manager.Register<ArgumentException>((exception, arg) =>
        {
            if (arg is ExceptionExplanationScene scene)
            {
                if (scene == ExceptionExplanationScene.Input)
                {
                    return exception.Message;
                }
            }
            return null;
        });
        manager.Register<ArgumentOutOfRangeException>((exception, arg) =>
        {
            if (arg is ExceptionExplanationScene scene)
            {
                if (scene == ExceptionExplanationScene.Input)
                {
                    return Texts.ValueMustBeWithinValidRange;
                }
            }
            return null;
        });
        manager.Register<FileNameExistsException>((exception, arg) => Texts.FileNameExists);
        manager.Register<InvalidFilePathException>((exception, arg) => Texts.InvalidFilePath);
        manager.Register<AddonNameExistsException>((exception, arg) => Texts.ItemNameExists);
        manager.Register<AddonNodeInvalidMoveException>((exception, arg) => Texts.AddonNodeInvalidMoveMessage);
        manager.Register<AddonNodeMoveDeniedException>((exception, arg) => Texts.AddonMoveDeniedMessage.FormatNoThrow(exception.AddonNode.NodePath));
        manager.Register<FileOutOfAddonRootException>((exception, arg) => Texts.FileMustBeInCurrentDirectory);
    }
}
