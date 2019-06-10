// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Threading.Tasks;

using System.IO;


namespace ChatCommon
{
    /// <summary>
    /// This class is a framework over a network stream that implements the ReadAll function by looping until all data is read.
    /// The write function does not offer this.
    /// </summary>
    class ReadAggregatorWritePassthrough : IReadableStream, IWriteableStream
    {
        private Stream m_stream;

        // constructor
        public ReadAggregatorWritePassthrough(Stream stream)
        {
            m_stream = stream;
        }

        public bool Write(byte[] buffer, int offset, int length)
        {
            try
            {
                m_stream.Write(buffer, offset, length);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
        public async Task<bool> WriteAsync(byte[] buffer, int offset, int length)
        {
            try
            {
                await m_stream.WriteAsync(buffer, offset, length);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

        }

        /// <summary>
        /// Read from the underlying stream until length bytes have been read.
        /// </summary>
        /// <param name="buffer">The buffer into which to place bytes read from the stream.</param>
        /// <param name="offset">The index at which to place the first byte.</param>
        /// <param name="length">number of bytes to read. Will block until all are read.</param>
        /// <returns>false if the stream was closed before the read completed. otherwise true</returns>
        public bool ReadAll(byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            while (totalRead < length)
            {
                int amountRead;
                try
                {
                    amountRead = m_stream.Read(buffer, offset + totalRead, length - totalRead);
                }
                catch (IOException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                if (amountRead == 0)  // socket closed
                    return false;
                totalRead += amountRead;
            }
            return true;
        }

        /// <summary>
        /// Read from the underlying stream until length bytes have been read.
        /// </summary>
        /// <param name="buffer">The buffer into which to place bytes read from the stream.</param>
        /// <param name="offset">The index at which to place the first byte.</param>
        /// <param name="length">number of bytes to read. Will not return until all are read.</param>
        /// <returns>false if the stream was closed before the read completed. otherwise true</returns>
        public async Task<bool> ReadAllAsync(byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            while (totalRead < length)
            {
                int amountRead;
                try
                {
                    amountRead = await m_stream.ReadAsync(buffer, offset + totalRead, length - totalRead);
                }
                catch (IOException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }

                if (amountRead == 0)  // socket closed
                    return false;

                totalRead += amountRead;
            }
            return true;
        }


        /// <summary>
        /// Read from the underlying stream until length bytes have been read.
        /// Discards the bytes.
        /// </summary>
        /// <param name="length">The number of bytes to discard.</param>
        /// <returns>false if the stream was closed before the read completed. otherwise true</returns>
        public bool DumpReadBuffer(int length)
        {
            if (length <= 0)
                return true;

            const int BUFF_LEN = 4096;
            byte[] buffer = new byte[BUFF_LEN];

            while (length > 0)
            {
                int amountRead;
                try
                {
                    amountRead = m_stream.Read(buffer, 0, Math.Min(BUFF_LEN, length));
                }
                catch (IOException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                if (amountRead == 0)  // socket closed
                    return false;
                length -= amountRead;
            }
            return true;
        }
    }
}
