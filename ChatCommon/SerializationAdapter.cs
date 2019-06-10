// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
//
// Contains the class definitions for SerializationAdapter and SerializationAdapterAsync
// ==================================================================

using System;
using System.Threading.Tasks;


namespace ChatCommon
{
    /// <summary>
    /// This class is a convenience wrapper over the Serialzer class.
    /// </summary>
    public class SerializationAdapter
    {
        private IWriteableStreamSync m_outStream;

        public SerializationAdapter(IWriteableStreamSync stream)
        {
            m_outStream = stream;
        }

        public bool Serialize(Int32 i)
        {
            return Serializer.Serialize(m_outStream, i);
        }
        public bool Serialize(string s)
        {
            return Serializer.Serialize(m_outStream, s);
        }
    }

    /// <summary>
    /// This class is a convenience wrapper over the Serialzer class.
    /// </summary>
    public class SerializationAdapterAsync
    {
        private IWriteableStreamAsync m_outStream;

        public SerializationAdapterAsync(IWriteableStreamAsync stream)
        {
            m_outStream = stream;
        }

        public async Task<bool> SerializeAsync(Int32 i)
        {
            return await Serializer.SerializeAsync(m_outStream, i);
        }
        public async Task<bool> SerializeAsync(string s)
        {
            return await Serializer.SerializeAsync(m_outStream, s);
        }
    }
}
