using System;
using System.Collections.Generic;

namespace L4D2AddonAssistant
{
    public interface IHierarchyNode<T> where T : IHierarchyNode<T>
    {
        bool IsNonterminal { get; }

        IEnumerable<T> Children { get; }
    }
}
