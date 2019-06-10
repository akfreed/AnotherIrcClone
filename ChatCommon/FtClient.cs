// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System.Net.Security;


namespace ChatCommon
{
    /// <summary>
    /// This class allows File Transfer to get around the ReadAll method (by saving the original stream), but still provide some 
    /// serialization/deserialation functions that use ReadAll.
    /// ReadAll is inappropriate for file transfer, since the files be too large to hold in memory.
    /// 
    /// Tips: 
    /// - This class will not stop you from shooting yourself in the foot.
    ///     - It simply provides a holding place for the different serializers/deserializers all based on the given TcpClient
    /// - Provides no mechanism to handle concurrent reads. Provides no mechanism to handle concurrent writes.
    ///     - Concurrent read/write is ok, since this is allowed by the TcpClient.
    /// - Probably shouldn't mix Async with non-Async
    /// </summary>
    public class FtClient
    {
        public  SslStream                      Client            { get; private set; }
        private ReadAggregatorWritePassthrough m_readAllHelper;
        public  SerializationAdapter           Serializer        { get; private set; }
        public  SerializationAdapterAsync      SerializerAsync   { get; private set; }
        public  DeserializationAdapter         Deserializer      { get; private set; }
        public  DeserializationAdapterAsync    DeserializerAsync { get; private set; }
        private bool m_disposed = false;

        public FtClient(SslStream sslClient)
        {
            Client            = sslClient;
            m_readAllHelper   = new ReadAggregatorWritePassthrough(sslClient);
            Serializer        = new SerializationAdapter(m_readAllHelper);
            SerializerAsync   = new SerializationAdapterAsync(m_readAllHelper);
            Deserializer      = new DeserializationAdapter(m_readAllHelper);
            DeserializerAsync = new DeserializationAdapterAsync(m_readAllHelper);
        }

        public void Close()
        {
            if (!m_disposed)
            {
                m_readAllHelper   = null;
                Serializer        = null;
                SerializerAsync   = null;
                Deserializer      = null;
                DeserializerAsync = null;

                Client.Close();
                Client = null;

                m_disposed = true;
            }
        }
    }
}
