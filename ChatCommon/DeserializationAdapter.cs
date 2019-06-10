// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
//
// Contains the class definitions for DeserializationAdapter and DeserializationAdapterAsync
// ==================================================================

using System;
using System.Threading.Tasks;


namespace ChatCommon
{
    /// <summary>
    /// This class is a convenience wrapper over the Serialzer class.
    /// </summary>
    public class DeserializationAdapter
    {
        private IReadableStreamSync m_inStream;

        public DeserializationAdapter(IReadableStreamSync stream)
        {
            m_inStream = stream;
        }

        public bool Deserialize(ref Int32 i)
        {
            return Serializer.Deserialize(m_inStream, ref i);
        }
        public bool Deserialize(ref string s)
        {
            return Serializer.Deserialize(m_inStream, ref s);
        }
    }

    /// <summary>
    /// This class is a convenience wrapper over the Serialzer class.
    /// </summary>
    public class DeserializationAdapterAsync
    {
        private IReadableStreamAsync m_inStream;

        public DeserializationAdapterAsync(IReadableStreamAsync stream)
        {
            m_inStream = stream;
        }
        
        public async Task<Tuple<bool, Int32>> DeserializeAsync(Int32 defaultVal)
        {
            return await Serializer.DeserializeAsync(m_inStream, defaultVal);
        }
        public async Task<Tuple<bool, string>> DeserializeAsync(string defaultVal)
        {
            return await Serializer.DeserializeAsync(m_inStream, defaultVal);
        }
    }
}
