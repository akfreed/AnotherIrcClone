// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Threading.Tasks;

using System.Threading;


namespace ChatCommon
{
    /// <summary>
    /// This is an "almost" RAII asynchronous lock.
    /// Because it's designed to allow an async lock call, it must be used with `SemaphoreSlim` (which has an Async wait method) and
    ///     the call to lock must be done separately, since the constructor can't be `async`. 
    /// 
    /// MUST use like this:
    ///     using (var lck = new AsyncLockManager(m_writeLock)) 
    ///     {
    ///         await lck.LockAsync();
    ///         ...
    ///     }
    ///     
    /// That is, you MUST call LockAsync() before the `using` block ends, since the `Dispose` method will automatically release the lock.
    /// 
    /// Regardless of the contraints, this is still useful because it helps with the difficult problem of unlocking with flow control / exception 
    /// handling, while the required manual locking at the beginning is easy and allows an async design.
    /// </summary>
    sealed class AsyncLockManager : IDisposable
    {
        private SemaphoreSlim m_lockReference;

        public AsyncLockManager(SemaphoreSlim lockReference)
        {
            m_lockReference = lockReference;
        }

        public async Task LockAsync()
        {
            await m_lockReference.WaitAsync();
        }

        public void Dispose()
        {
            m_lockReference.Release();
        }

        
    }
}
