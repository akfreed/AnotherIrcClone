// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;

using System.Net;
using System.Net.Security;
using System.Diagnostics;
using System.Threading;


namespace ChatCommon
{
    /// <summary>
    /// Handles the demultiplexing side of Asymmetric Multiplexing over TCP (AM/TCP).
    /// This end can read from one of two virtual lines. A small header is used to indicate which line on which a message belongs to.
    /// This end only has one line to write to. (Thus asymmetric.)
    /// This design allows the server to use one socket per user. One virtual line is used for synchronous communication and the other 
    /// virtual line is used to end random event messages.
    /// 
    /// This classes uses a dedicated thread to read messages from the server and put them in the appropriate queue.
    /// 
    /// Thread-per-client is expensive for the server, but cheap for the client. I wanted to avoid even having 2 sockets per client.
    /// With this model, the clients shoulder the load for the server.
    /// </summary>
    public class AmTcpDemuxer
    {
        public const int BUFFER_SIZE = ProdConsBuffer.BUFFER_SIZE;

        private SslStream m_client;
        private ReadAggregatorWritePassthrough m_stream;
        private ProdConsBuffer m_mainBuffer  = new ProdConsBuffer();
        private ProdConsBuffer m_eventBuffer = new ProdConsBuffer();
        private Thread m_serviceThread;
        private bool m_disposed = false;
        private object m_disposeLock = new object();

        public IReadableStreamSync  ReadStream      { get { return m_mainBuffer;  } }
        public IReadableStreamSync  ReadEventStream { get { return m_eventBuffer; } }
        public IWriteableStreamSync WriteStream     { get { return m_stream;      } }

        public IPEndPoint RemoteEndPoint { get; private set; } = null;


        // Constructor
        public AmTcpDemuxer(SslEgg egg)
        {
            RemoteEndPoint = (IPEndPoint)egg.TcpSocket.Client.RemoteEndPoint;
            m_client = egg.SslStream;
            m_stream = new ReadAggregatorWritePassthrough(m_client);

            m_serviceThread = new Thread(serviceLoop);
            m_serviceThread.Start();
        }

        // Threadsafe, but note that m_client and m_stream are not protected by lock outside of this function.
        public void Close()
        {
            lock (m_disposeLock)
            {
                if (!m_disposed)
                {
                    m_client.Close();
                    // don't need to close the ProdConsBuffers because they will be closed by the other thread.
                    m_serviceThread.Join();
                    m_disposed = true;
                }
            }
        }

        

        private void serviceLoop()
        {
            while (multiplexRead())
            { }

            m_mainBuffer.CloseWrite();
            m_eventBuffer.CloseWrite();
        }
        
        private bool multiplexRead()
        {
            byte[] buffer = new byte[BUFFER_SIZE];

            // read the header (3*4=12 bytes)
            bool stillConnected = m_stream.ReadAll(buffer, 0, 3 * 4);
            if (!stillConnected)
                return false;

            int version = BitConverter.ToInt32(buffer, 0);
            int iTag    = BitConverter.ToInt32(buffer, 4);
            int length  = BitConverter.ToInt32(buffer, 8);

            AmTcpMuxer.SerializerTag tag;
            try
            {
                tag = (AmTcpMuxer.SerializerTag)iTag;
            }
            catch
            {
                // if the tag was invalid, dump the buffer
                stillConnected = m_stream.DumpReadBuffer(length);
                return stillConnected;
            }

            // detect length too long
            int overflow = 0;
            if (length > BUFFER_SIZE)
            {
                overflow = length - BUFFER_SIZE;
                length = BUFFER_SIZE;
            }

            stillConnected = m_stream.ReadAll(buffer, 0, length);
            if (!stillConnected)
                return false;

            if (tag == AmTcpMuxer.SerializerTag.MAIN)
                m_mainBuffer.Write(buffer, 0, length);
            else if (tag == AmTcpMuxer.SerializerTag.EVENT)
                m_eventBuffer.Write(buffer, 0, length);
            else
                Debug.Assert(false);  // shouldn't happen

            // truncate length if necessary
            if (overflow > 0)
            {
                stillConnected = m_stream.DumpReadBuffer(overflow);
                if (!stillConnected)
                    return false;
            }

            return true;
        }


    }
}
