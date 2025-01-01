using Avalonia.Controls.Selection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace FireAxe
{
    public static class SelectionModelHelper
    {
        public static void Select(ISelectionModel selection, IEnumerable<object>? items, Func<object?, object?>? sourceConverter = null)
        {
            ArgumentNullException.ThrowIfNull(selection);

            var source = selection.Source;
            if (source == null)
            {
                return;
            }
            selection.Clear();
            if (items == null)
            {
                return;
            }
            var itemSet = items.ToImmutableHashSet();
            if (itemSet.Count == 0)
            {
                return;
            }
            int i = 0;
            foreach (var obj in source)
            {
                var objConverted = sourceConverter == null ? obj : sourceConverter(obj);
                if (objConverted != null && itemSet.Contains(objConverted))
                {
                    selection.Select(i);
                }
                i++;
            }
        }
    }
}
