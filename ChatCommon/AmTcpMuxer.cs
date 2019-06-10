// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using System.IO;
using System.Net;
using System.Net.Security;
using System.Diagnostics;
using System.Threading;


namespace ChatCommon
{
    /// <summary>
    /// Handles the multiplexing side of Asymmetric Multiplexing over TCP (AM/TCP).
    /// This end can write to one of two virtual lines. A small header is used to indicate which line on which a message belongs to.
    /// This end only has one line to read from. (Thus asymmetric.)
    /// This design allows the server to use one socket per user. One virtual line is used for synchronous communication and the other 
    /// virtual line is used to end random event messages.
    /// </summary>
    public class AmTcpMuxer
    {
        public const int WRITE_MAX = ProdConsBuffer.BUFFER_SIZE;

        public enum SerializerTag
        {
            MAIN  = 0,
            EVENT = 1
        }

        // private class
        private class WriteClosure : IWriteableStreamAsync
        {
            private Func<byte[], int, int, Task<bool>> m_writeAsyncFunctor;

            public WriteClosure(Func<byte[], int, int, Task<bool>> writeAsyncFunctor)
            {
                m_writeAsyncFunctor = writeAsyncFunctor;
            }

            public async Task<bool> WriteAsync(byte[] buffer, int offset, int length)
            {
                return await m_writeAsyncFunctor(buffer, offset, length);
            }
        }

        // private data
        private SslStream m_client;
        private ReadAggregatorWritePassthrough m_stream;

        private SemaphoreSlim m_writeLock  = new SemaphoreSlim(1);
        private bool m_disposed = false;


        // public properties
        public IReadableStreamAsync  ReadStream       { get { return m_stream; } }
        public IWriteableStreamAsync WriteStream      { get { return new WriteClosure(this.writeAsync);      } }
        public IWriteableStreamAsync WriteEventStream { get { return new WriteClosure(this.writeEventAsync); } }

        public IPEndPoint RemoteEndPoint { get; private set; } = null;


        // Constructor
        public AmTcpMuxer(SslEgg egg)
        {
            RemoteEndPoint = (IPEndPoint)egg.TcpSocket.Client.RemoteEndPoint;
            m_client = egg.SslStream;
            m_stream = new ReadAggregatorWritePassthrough(m_client);
        }

        
        public void Close()
        {
            if (!m_disposed)
            {
                m_client.Close();
                m_disposed = true;
            }
        }


        private byte[] buildHeader(SerializerTag tag, int length)
        {
            List<byte[]> buildHeader = new List<byte[]>() { 
                // version: 1
                BitConverter.GetBytes(1),
                // tag:
                BitConverter.GetBytes((int)tag),
                // length:
                BitConverter.GetBytes(length)
            };

            MemoryStream packHeader = new MemoryStream();
            foreach (byte[] bs in buildHeader)
                packHeader.Write(bs, 0, bs.Length);

            // (3*4=12 bytes for header)
            byte[] header = packHeader.ToArray();
            Debug.Assert(header.Length == 3 * 4);

            return header;
        }

        private async Task<bool> writeAsync(SerializerTag tag, byte[] buffer, int offset, int length)
        {
            // sanity check
            Debug.Assert(buffer.Length >= offset + length);

            // truncate write length if necessary
            if (length > WRITE_MAX)
            {
                Debug.Assert(false);
                length = WRITE_MAX;
            }

            // construct the header 
            var header = buildHeader(tag, length);

            using (var lck = new AsyncLockManager(m_writeLock))
            {
                await lck.LockAsync();

                // send the header, then write the buffer
                return await m_stream.WriteAsync(header, 0, header.Length) && 
                       await m_stream.WriteAsync(buffer, offset, length);
            }
        }

        private async Task<bool> writeAsync(byte[] buffer, int offset, int length)
        {
            // main channel
            return await writeAsync(SerializerTag.MAIN, buffer, offset, length);
        }
        private async Task<bool> writeEventAsync(byte[] buffer, int offset, int length)
        {
            // event channel
            return await writeAsync(SerializerTag.EVENT, buffer, offset, length);
        }


    }
}
