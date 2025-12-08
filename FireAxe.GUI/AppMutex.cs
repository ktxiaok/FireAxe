using System;
using System.Threading;

namespace FireAxe;

internal static class AppMutex
{
    private const string MutexName = "cecf873b6deb4f2790bbddd75964bbca";

    private static Mutex? s_mutex = null;
    private static object s_mutexLock = new();

    public static bool TryEnter()
    {
        lock (s_mutexLock)
        {
            if (s_mutex is not null)
            {
                return true;
            }

            var mutex = new Mutex(false, MutexName);
            bool entered = false;
            try
            {
                entered = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                entered = true;
            }

            if (entered)
            {
                s_mutex = mutex;
                return true;
            }
            else
            {
                mutex.Dispose();
                return false;
            }
        }
    }

    public static void Release()
    {
        lock (s_mutexLock)
        {
            if (s_mutex is not null)
            {
                s_mutex.ReleaseMutex();
                s_mutex.Dispose();
                s_mutex = null;
            }
        }
    }
}