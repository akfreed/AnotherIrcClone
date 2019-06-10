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
    /// This optional class handles the File Transfer between the server and client.
    /// </summary>
    abstract public class FtServerside : CpServersideBase
    {

        protected override async Task ftClientServiceLoopAsync(SslEgg egg)
        {
            FtClient client = new FtClient(egg.SslStream);

            // shortcut lambda
            Func<Response, Task<bool>> lSendResponseAsync = async (response) =>
            {
                return await CpServersideBase.SendResponseAsync(client.SerializerAsync, response.Success, response.Message);
            };

            // the command string read from client
            var tup = await client.DeserializerAsync.DeserializeAsync("");
            bool stillConnected = tup.Item1;
            string commandString = tup.Item2;

            if (!stillConnected)
                goto exit;

            // an array of strings parsed from the sent command string by splitting by colon :
            // hard-coded max 10 substrings. Change if needed.
            string[] commandSplit = CpParseUtilities.SplitMessage(commandString, m_stringDelimiter, 10);

            switch (commandSplit[0].ToLower())
            {
                case "file_up":
                    {
                        Tuple<bool, Response> res = await callChecked_FileUpAsync(client, commandSplit);
                        if (res.Item1)  // client still connected
                            await lSendResponseAsync(res.Item2);
                        break;
                    }
                case "file_down":
                    {
                        Tuple<bool, Response> res = await callChecked_FileDownAsync(client, commandSplit);
                        if (res.Item1)  // client still connected
                            await lSendResponseAsync(res.Item2);
                        break;
                    }

                default:
                    break;
            }

            exit:
            client.Close();
        }


        protected override async Task<Tuple<bool, Response>> callChecked_FileUpAsync(FtClient ftClient, string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Tuple<bool, Response>(true, new Response(false, errMessage));

            // get filename
            string filename = commandSplit[1];

            // create a file
            using (Stream fileOut = FtFileManager.OpenForWrite(filename, out errMessage))
            {
                if (fileOut == null)
                    return new Tuple<bool, Response>(true, new Response(false, errMessage));

                // send ok (ready to receive file)
                bool stillConnected = await SendResponseAsync(ftClient.SerializerAsync, true, "");
                if (!stillConnected)
                    return new Tuple<bool, Response>(false, null);

                // receive file
                var fileReceiveResult = await FtFileManager.FileReceiveAsync(ftClient.Client, fileOut);
                if (!fileReceiveResult.Item1)
                    return new Tuple<bool, Response>(fileReceiveResult.Item1, null);

                // send ok (file was received ok)
                return new Tuple<bool, Response>(true, fileReceiveResult.Item2);
            }

        }


        protected override async Task<Tuple<bool, Response>> callChecked_FileDownAsync(FtClient ftClient, string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Tuple<bool, Response>(true, new Response(false, errMessage));

            // get filename
            string filename = commandSplit[1];

            // open the file
            using (Stream fileIn = FtFileManager.OpenForRead(filename, out errMessage))
            {
                if (fileIn == null)
                    return new Tuple<bool, Response>(true, new Response(false, errMessage));
                // send ok (ready to send file)
                bool stillConnected = await SendResponseAsync(ftClient.SerializerAsync, true, "");
                if (!stillConnected)
                    return new Tuple<bool, Response>(false, null);

                // send file
                stillConnected = await FtFileManager.FileSendAsync(ftClient.Client, fileIn);
                if (!stillConnected)
                    return new Tuple<bool, Response>(false, null);
            }

            return new Tuple<bool, Response>(true, new Response(true, ""));
        }
        
        
    }
}
