using ReactiveUI;
using System;
using System.Reactive;

namespace L4D2AddonAssistant
{
    public class CommonInteractions
    {
        public CommonInteractions()
        {

        }

        public Interaction<Unit, string?> ChooseDirectory { get; } = new();
    }
}
