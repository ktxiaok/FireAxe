using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FireAxe;

public static class ArgumentNullExceptionExtensions
{
    extension(ArgumentNullException)
    {
        public static void ThrowIfContainsNull<T>(IEnumerable<T?>? elements, [CallerArgumentExpression(nameof(elements))] string? paramName = null)
        {
            if (elements is not null)
            {
                foreach (var element in elements)
                {
                    if (element is null)
                    {
                        throw new ArgumentNullException(paramName, $"{paramName} contains a null value.");
                    }
                }
            }
        }

        public static void ThrowIfNullOrContainsNull<T>(IEnumerable<T?>? elements, [CallerArgumentExpression(nameof(elements))] string? paramName = null)
        {
            if (elements is null)
            {
                throw new ArgumentNullException(paramName);
            }
            ThrowIfContainsNull(elements, paramName);
        }
    }
}