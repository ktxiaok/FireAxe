using System;

namespace FireAxe;

internal static class DisposableUtils
{
    private class Disposable : IDisposable
    {
        private Action _dispose;

        internal Disposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose();
        }
    }

    public static IDisposable Create(Action dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);

        return new Disposable(dispose);
    }
}
