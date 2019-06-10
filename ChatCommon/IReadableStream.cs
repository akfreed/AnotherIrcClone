// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
//
// Contains the interface definitions for IReadableStreamSync, IReadableStreamAsync, and IReadableStream
// ==================================================================

using System.Threading.Tasks;


namespace ChatCommon
{
    public interface IReadableStreamSync
    {
        /// <summary>
        /// Read from an input stream.
        /// Implementations shall block until all specified bytes are read.
        /// </summary>
        /// <param name="buffer">The buffer into which to place the read bytes.</param>
        /// <param name="offset">The index at which to put the first byte.</param>
        /// <param name="length">The amount of bytes to read.</param>
        /// <returns>true if other end is still connected. false if the other end disconnected (received a 0-length read on underlying stream read).</returns>
        bool ReadAll(byte[] buffer, int offset, int length);
    }

    public interface IReadableStreamAsync
    { 
        /// <summary>
        /// The Async variant of Read.
        /// Implementations shall not return until all specified bytes are read.
        /// </summary>
        /// <param name="buffer">The buffer into which to place the read bytes.</param>
        /// <param name="offset">The index at which to put the first byte.</param>
        /// <param name="length">The amount of bytes to read.</param>
        /// <returns>true if other end is still connected. false if the other end disconnected (received a 0-length read on underlying stream read).</returns>
        Task<bool> ReadAllAsync(byte[] buffer, int offset, int length);
    }

    public interface IReadableStream : IReadableStreamSync, IReadableStreamAsync
    { }
}
