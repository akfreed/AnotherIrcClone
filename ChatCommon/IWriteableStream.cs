// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
//
// Contains the interface definitions for IWriteableStreamSync, IWriteableStreamAsync, and IWriteableStream
// ==================================================================

using System.Threading.Tasks;


namespace ChatCommon
{
    public interface IWriteableStreamSync
    {
        /// <summary>
        /// Write to an output stream.
        /// </summary>
        /// <param name="buffer">The buffer from which to take the bytes to write.</param>
        /// <param name="offset">The index at which to take the first byte.</param>
        /// <param name="length">The amount of bytes to write.</param>
        /// <returns>true if other end is still connected. false if the other end disconnected (received a 0-length read on underlying stream write).</returns>
        bool Write(byte[] buffer, int offset, int length);
    }

    public interface IWriteableStreamAsync
    {
        /// <summary>
        /// The Async variant of Write.
        /// </summary>
        /// <param name="buffer">The buffer from which to take the bytes to write.</param>
        /// <param name="offset">The index at which to take the first byte.</param>
        /// <param name="length">The amount of bytes to write.</param>
        /// /// <returns>true if other end is still connected. false if the other end disconnected (received a 0-length read on underlying stream write).</returns>
        Task<bool> WriteAsync(byte[] buffer, int offset, int length);
    }

    public interface IWriteableStream : IWriteableStreamSync, IWriteableStreamAsync
    { }
}
