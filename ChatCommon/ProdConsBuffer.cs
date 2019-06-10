// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Threading;
using System.Diagnostics;

namespace ChatCommon
{
    /// <summary>
    /// This class is a threadsafe queue of bytes.
    /// </summary>
    class ProdConsBuffer : IReadableStreamSync, IWriteableStreamSync
    {
        public const int BUFFER_SIZE = 8192;
        private byte[] m_buffer     = new byte[BUFFER_SIZE];
        private int    m_first      = 0;
        private int    m_last       = 0;
        private int    m_freeSpace  = BUFFER_SIZE;
        private object m_bufferLock = new object();
        private bool   m_closed     = false;


        public void CloseWrite()
        {
            lock (m_bufferLock)
            {
                if (!m_closed)
                {
                    m_closed = true;
                    Monitor.PulseAll(m_bufferLock);
                }
            }
        }


        // length must be greater than 0
        public bool Write(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0, "Length must be > 0.");
            Debug.Assert(length <= BUFFER_SIZE, "Length must be <= buffer size.");

            lock (m_bufferLock)
            {
                // wait for enough free space to write
                while (m_freeSpace < length && !m_closed)
                    Monitor.Wait(m_bufferLock);

                if (m_closed)
                    return false;
                
                // copy
                write_unchecked(buffer, offset, length);

                // signal all (due to design of C# synchronization constructs. Works because single consumer/single producer)
                Monitor.PulseAll(m_bufferLock);
            }

            return true;
        }

        // assumes the lock is already held
        // assumes m_freeSpace >= length
        // assumes length > 0
        // assumes buffer is large enough to read @length bytes from @offset 
        private void write_unchecked(byte[] in_buffer, int offset, int length)
        {
            int toEnd = BUFFER_SIZE - m_last;
            if (length < toEnd)
            {
                Buffer.BlockCopy(in_buffer, offset, m_buffer, m_last, length);
                m_last += length;
            }
            else
            {
                Buffer.BlockCopy(in_buffer, offset,         m_buffer, m_last, toEnd);
                Buffer.BlockCopy(in_buffer, offset + toEnd, m_buffer, 0,      length - toEnd);
                m_last = length - toEnd;
            }
            m_freeSpace -= length;
        }


        public bool ReadAll(byte[] buffer, int offset, int length)
        {
            Debug.Assert(length > 0, "Length must be > 0.");
            Debug.Assert(length <= BUFFER_SIZE, "Length must be <= BUFFER_SIZE.");

            lock (m_bufferLock)
            {
                // wait for available data
                // available space is buffer capacity - free space
                while (length + m_freeSpace > BUFFER_SIZE && !m_closed)
                    Monitor.Wait(m_bufferLock);
                // postcondition: length + m_freeSpace <= BUFFER_SIZE || m_closed
                // Even if the write-side is closed, allow reading the last bit of data in the buffer.
                // However, no new data will be coming in. If more data is requested than is available, return (without reading).
                if (length + m_freeSpace > BUFFER_SIZE)
                    return false;

                // copy
                read_unchecked(buffer, offset, length);

                // signal all (due to design of C# synchronization constructs. Works because single consumer/single producer)
                Monitor.PulseAll(m_bufferLock);
            }

            // inherits return value from the interface. A "true" value means the socket is still connected. Since this isn't based on a socket, but a memory buffer, it is always connected.
            return true;
        }

        // assumes the lock is already held
        // assumes m_freeSpace + length <= BUFFER_SIZE
        // assumes length > 0
        // assumes buffer is large enough to write @length bytes from @offset
        private void read_unchecked(byte[] out_buffer, int offset, int length)
        {
            int toEnd = BUFFER_SIZE - m_first;
            if (length < toEnd)
            {
                Buffer.BlockCopy(m_buffer, m_first, out_buffer, offset, length);
                m_first += length;
            }
            else
            {
                Buffer.BlockCopy(m_buffer, m_first, out_buffer, offset,         toEnd);
                Buffer.BlockCopy(m_buffer, 0,       out_buffer, offset + toEnd, length - toEnd);
                m_first = length - toEnd;
            }
            m_freeSpace += length;
        }
    }
}
