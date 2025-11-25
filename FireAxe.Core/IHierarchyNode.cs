using System;
using System.Collections.Generic;

namespace FireAxe;

public interface IHierarchyNode<T> where T : IHierarchyNode<T>
{
    bool IsNonterminal { get; }

    IEnumerable<T> Children { get; }

    IHierarchyNode<T>? Parent { get; }
}
