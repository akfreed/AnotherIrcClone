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
    /// This singleton class handles file creating/reading/writing for the client or server.
    /// </summary>
    public static class FtFileManager
    {
        public static string BasePath { get; set; } = "FileTransfer";


        private static string addBasePath(string filename)
        {
            return Path.Combine(BasePath, filename);
        }

        public static bool FileExists(string filename)
        {
            string filepath = addBasePath(filename);
            return File.Exists(filepath);
        }


        public static Stream OpenForRead(string filename, out string errMessage)
        {
            string path = addBasePath(filename);

            try
            {
                FileStream fileIn = new FileStream(path, FileMode.Open, FileAccess.Read);
                errMessage = "";
                return fileIn;
            }
            catch (Exception e)
            {
                errMessage = String.Format("Unable to open file '{0}': {1}", path, e.Message);
                return null;
            }
        }

        
        private static bool createEmpty(string filename, out string errMessage)
        {
            if (!String.IsNullOrWhiteSpace(BasePath) && !Directory.Exists(BasePath))
            {
                try
                {
                    Directory.CreateDirectory(BasePath);
                }
                catch (Exception e)
                {
                    errMessage = String.Format("Unable to create base directory '{0}': '{1}'", BasePath, e.Message);
                    return false;
                }
            }

            string path = addBasePath(filename);

            try
            {
                using (FileStream fileOut = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                }
                errMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errMessage = String.Format("Unable to create file '{0}': {1}", path, e.Message);
                return false;
            }
        }

        public static bool Delete(string filename, out string errMessage)
        {
            string path = addBasePath(filename);
            try
            {
                File.Delete(path);
                errMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errMessage = String.Format("Unable to delete file '{0}': {1}", path, e.Message);
                return false;
            }
        }


        // assumes the file does not exist
        public static Stream OpenForWrite(string filename, out string errMessage)
        {
            if (!createEmpty(filename, out errMessage))
                return null;

            string path = addBasePath(filename);

            try
            {
                // Open an existing file only. Erase the contents.
                FileStream fileOut = new FileStream(path, FileMode.Truncate, FileAccess.Write);
                errMessage = "";
                return fileOut;
            }
            catch (Exception e)
            {
                errMessage = String.Format("Unable to open file '{0}' for writing: {1}", path, e.Message);
                return null;
            }
        }


        // returns true if client is still connected
        public static async Task<bool> FileSendAsync(Stream clientOutStream, Stream fileIn)
        {
            const int BUFFER_SIZE = 1 << 20;  // ~1MB
            byte[] buffer = new byte[BUFFER_SIZE];

            // find the size of the file
            int fileLength;
            try
            {
                fileLength = (int)fileIn.Seek(0, SeekOrigin.End);
                fileIn.Seek(0, SeekOrigin.Begin);
            }
            catch
            {
                return true;
            }

            // first, write the file size
            if (!await Serializer.SerializeAsync(new ReadAggregatorWritePassthrough(clientOutStream), fileLength))
                return false;

            int totalRead = 0;

            while (totalRead < fileLength)
            {
                // read from file
                int amountRead;
                try
                {
                    amountRead = await fileIn.ReadAsync(buffer, 0, Math.Min(BUFFER_SIZE, fileLength - totalRead));
                }
                catch
                {
                    return true;
                }
                // send to client
                try
                { 
                    await clientOutStream.WriteAsync(buffer, 0, amountRead);
                }
                catch
                {
                    return false;
                }

                totalRead += amountRead;
            }

            return true;
        }


        public static async Task<Tuple<bool, Response>> FileReceiveAsync(Stream clientInStream, Stream fileOut)
        {
            // read file length
            int fileLength = 0;
            {
                Tuple<bool, int> res = await Serializer.DeserializeAsync(new ReadAggregatorWritePassthrough(clientInStream), 0);
                if (!res.Item1)
                    return new Tuple<bool, Response>(false, null);
                fileLength = res.Item2;

                if (fileLength <= 0)
                {
                    Logging.DebugWriteLine("FtFileManager received a negative fileLength.");
                    return new Tuple<bool, Response>(true, new Response(false, "File size must be greater than zero."));
                }
            }

            const int BUFFER_SIZE = 1 << 20;  // ~1MB
            byte[] buffer = new byte[BUFFER_SIZE];
            int totalRead = 0;

            while (totalRead < fileLength)
            {
                int amountRead;
                try
                {
                    amountRead = await clientInStream.ReadAsync(buffer, 0, Math.Min(BUFFER_SIZE, fileLength - totalRead));
                    if (amountRead == 0)  // client disconnected before finishing
                    {
                        Logging.DebugWriteLine("Client disconnected before file upload completed.");
                        return new Tuple<bool, Response>(false, null);
                    }
                }
                catch
                {
                    Logging.DebugWriteLine("Client disconnected before file upload completed.");
                    return new Tuple<bool, Response>(false, null);
                }

                try
                {
                    await fileOut.WriteAsync(buffer, 0, amountRead);
                }
                catch (Exception e)
                {
                    string message = String.Format("File write failed: {0}", e.Message);
                    Logging.DebugWriteLine(message);
                    return new Tuple<bool, Response>(true, new Response(false, message));
                }
                totalRead += amountRead;
            }

            await fileOut.FlushAsync();
            return new Tuple<bool, Response>(true, new Response(true, ""));
        }


    }
}
