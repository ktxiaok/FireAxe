using Avalonia.Controls;
using System;

namespace L4D2AddonAssistant
{
    public class WindowReference<T> where T : Window
    {
        private T? _target;

        public WindowReference(T target)
        {
            ArgumentNullException.ThrowIfNull(target);

            _target = target;
            target.Closed += Target_Closed;
        }

        public T? Get() => _target;

        private void Target_Closed(object? sender, EventArgs e)
        {
            _target = null;
        }
    }
}
