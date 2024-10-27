using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace L4D2AddonAssistant
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = null;

        protected void NotifyAndSetIfChanged<T>(ref T field, in T value, [CallerMemberName] string? propertyName = null)
        {
            if (propertyName == null)
            {
                return;
            }
            if (EqualityComparer<T>.Default.Equals(value, field))
            {
                return;
            }

            field = value;
            NotifyChanged(propertyName);
        }

        protected void NotifyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName == null)
            {
                return;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
