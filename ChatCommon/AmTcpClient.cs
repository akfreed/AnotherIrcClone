// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================


namespace ChatCommon
{
    /// <summary>
    /// This class conveniently ties the AmTcpMuxer with serialization and deserialization adapters.
    /// </summary>
    public class AmTcpClient
    {
        private AmTcpMuxer m_client;

        public SerializationAdapterAsync   Serializer      { get; }
        public SerializationAdapterAsync   EventSerializer { get; }
        public DeserializationAdapterAsync Deserializer    { get; }

        public System.Net.IPEndPoint RemoteEndPoint { get { return m_client.RemoteEndPoint; } }


        public AmTcpClient(SslEgg egg)
        {
            m_client        = new AmTcpMuxer(egg);
            Serializer      = new SerializationAdapterAsync(m_client.WriteStream);
            EventSerializer = new SerializationAdapterAsync(m_client.WriteEventStream);
            Deserializer    = new DeserializationAdapterAsync(m_client.ReadStream);
        }

        public void Close()
        {
            m_client.Close();
        }
    }
}
