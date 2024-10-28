using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace L4D2AddonAssistant
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = null;

        protected bool NotifyAndSetIfChanged<T>(ref T field, in T value, [CallerMemberName] string? propertyName = null)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (EqualityComparer<T>.Default.Equals(value, field))
            {
                return false;
            }

            field = value;
            NotifyChanged(propertyName);
            return true;
        }

        protected void NotifyChanged([CallerMemberName] string? propertyName = null)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
