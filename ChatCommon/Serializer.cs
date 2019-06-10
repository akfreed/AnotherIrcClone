// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Net;


namespace ChatCommon
{
    /// <summary>
    /// This static class has common serialization and deserialization functions of both the synchronous (traditional blocking) and asynchronous (C# Task/threadpool) varieties.
    /// It currently only has Int32 and String serialization implement.
    /// </summary>
    public static class Serializer
    {
        public const int MAX_STRING_LEN = ProdConsBuffer.BUFFER_SIZE;

        private static ASCIIEncoding m_asciiEncoder = new ASCIIEncoding();


        public static bool Serialize(IWriteableStreamSync stream, Int32 i)
        {
            var iNetworkOrder = IPAddress.HostToNetworkOrder(i);
            var bytes = BitConverter.GetBytes(iNetworkOrder);
            return stream.Write(bytes, 0, bytes.Length);
        }
        public static async Task<bool> SerializeAsync(IWriteableStreamAsync stream, Int32 i)
        {
            Int32 iNetworkOrder = IPAddress.HostToNetworkOrder(i);
            var bytes = BitConverter.GetBytes(iNetworkOrder);
            return await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        public static bool Serialize(IWriteableStreamSync stream, string s)
        {   
            // convert string to bytes
            var bytes = m_asciiEncoder.GetBytes(s);

            // max length check
            Debug.Assert(bytes.Length <= MAX_STRING_LEN);
            int length = Math.Min(bytes.Length, MAX_STRING_LEN);

            // first send length of string, then send string
            return Serialize(stream, length) && stream.Write(bytes, 0, length);
        }
        public static async Task<bool> SerializeAsync(IWriteableStreamAsync stream, string s)
        {
            // convert string to bytes
            var bytes = m_asciiEncoder.GetBytes(s);

            // max length check
            Debug.Assert(bytes.Length <= MAX_STRING_LEN);
            int length = Math.Min(bytes.Length, MAX_STRING_LEN);

            // first send length of string, then send string
            return await SerializeAsync(stream, length) && await stream.WriteAsync(bytes, 0, length);
        }

        public static bool Deserialize(IReadableStreamSync stream, ref Int32 i)
        {
            var bytes = new byte[sizeof(Int32)];
            bool stillConnected = stream.ReadAll(bytes, 0, bytes.Length);
            if (!stillConnected)
                return false;

            Int32 iNetworkOrder = BitConverter.ToInt32(bytes, 0);
            i = IPAddress.NetworkToHostOrder(iNetworkOrder);
            return true;
        }
        public static async Task<Tuple<bool, Int32>> DeserializeAsync(IReadableStreamAsync stream, Int32 defaultVal)
        {
            var bytes = new byte[sizeof(Int32)];
            bool stillConnected = await stream.ReadAllAsync(bytes, 0, bytes.Length);
            if (!stillConnected)
                return new Tuple<bool, Int32>(false, defaultVal);

            Int32 iNetworkOrder = BitConverter.ToInt32(bytes, 0);
            Int32 i = IPAddress.NetworkToHostOrder(iNetworkOrder);

            return new Tuple<bool, Int32>(true, i);
        }

        public static bool Deserialize(IReadableStreamSync stream, ref string s)
        {
            // first read length of string
            Int32 length = 0;
            bool stillConnected = Deserialize(stream, ref length);
            if (!stillConnected)
                return false;

            // length check
            if (length < 0 || length > MAX_STRING_LEN)
            {
                // if other end sends bad length, we can't currently handle it  DEV: improve
                return false;
            }

            var bytes = new byte[length];
            stillConnected = stream.ReadAll(bytes, 0, bytes.Length);
            if (!stillConnected)
                return false;
            try
            {
                s = m_asciiEncoder.GetString(bytes);
                return true;
            }
            catch
            {
                // if other end sends bad info, we can't currently handle it  DEV: improve
                return false;
            }            
        }
        public static async Task<Tuple<bool, string>> DeserializeAsync(IReadableStreamAsync stream, string defaultVal)
        {
            // first read length of string
            var res = await DeserializeAsync(stream, 0);
            bool stillConnected = res.Item1;
            int length = res.Item2;

            if (!stillConnected)
                return new Tuple<bool, string>(false, defaultVal);

            // length check
            if (length < 0 || length > MAX_STRING_LEN)
            {
                // if other end sends bad length, we can't currently handle it  DEV: improve
                return new Tuple<bool, string>(false, defaultVal);
            }

            var bytes = new byte[length];
            stillConnected = await stream.ReadAllAsync(bytes, 0, bytes.Length);
            if (!stillConnected)
                return new Tuple<bool, string>(false, defaultVal);
            try
            {
                string s = m_asciiEncoder.GetString(bytes);
                return new Tuple<bool, string>(true, s);
            }
            catch
            {
                // if other end sends bad info, we can't currently handle it  DEV: improve
                return new Tuple<bool, string>(false, defaultVal);
            }
        }
    }
}
