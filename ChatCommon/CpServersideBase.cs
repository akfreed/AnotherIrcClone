// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Net.Security;


namespace ChatCommon
{
    /// <summary>
    /// This class runs handles the Chat Protocol between the server and client.
    /// Some callbacks must be implemented by the inheriting class.
    /// </summary>
    abstract public class CpServersideBase
    {
        protected const char   m_stringDelimiter = ':';

        public enum ConnectionMode : byte
        {
            CHAT_PROTOCOL = 0,
            FILE_TRANSFER = 1
        }


        protected async Task clientServiceLoop(SslEgg egg)
        {
            SslStream sslClient = egg.SslStream;

            // Determine if client is for chat or file transfer.
            // Read one byte right from the stream
            int amountRead = 0;
            ConnectionMode mode;
            try
            {
                byte[] buffer = new byte[1];
                amountRead = await sslClient.ReadAsync(buffer, 0, 1);
                mode = (ConnectionMode)buffer[0];
            }
            catch
            {
                goto exit_fail;
            }

            if (amountRead != 1)  // client disconnected
                goto exit_fail;

            switch (mode)
            {
                case ConnectionMode.CHAT_PROTOCOL:
                    await cpClientServiceLoopAsync(egg);
                    return;
                case ConnectionMode.FILE_TRANSFER:
                    await ftClientServiceLoopAsync(egg);
                    return;

                default:
                    Debug.Assert(false);  // should never happen
                    goto exit_fail;
            }

            exit_fail:
            sslClient.Close();
            return;
        }

        private async Task cpClientServiceLoopAsync(SslEgg egg)
        {
            AmTcpClient client = new AmTcpClient(egg);
            
            // shortcut lambda
            Func<Response, Task<bool>> lSendResponseAsync = async (response) =>
            {
                return await SendResponseAsync(client.Serializer, response.Success, response.Message);
            };
            
            // establish connection / register user
            string username;
            {
                // read from the new client (expecting a "connect" commandstring).
                var tup = await client.Deserializer.DeserializeAsync("");
                bool   stillConnected = tup.Item1;
                string commandString  = tup.Item2;
                
                if (!stillConnected)
                {
                    Logging.DebugWriteLine("Client connected but disconnected before trying to register.");
                    client.Close();
                    return;
                }
                // parse the command string and register the user
                Response res = callChecked_Connect(client, commandString, out username);
                await lSendResponseAsync(res);  // don't need to check result as a disconnected client will be detected upon first read
                if (!res.Success)
                {
                    Logging.DebugWriteLine(String.Format("Unable to register new client '{0}'. {1}", username, res.Message));
                    client.Close();
                    return;
                }

                Logging.DebugWriteLine(String.Format("Client '{0}' connected. {1}", username, client.RemoteEndPoint.ToString() ));
            }

            while (true)
            {
                // the command string read from client
                var tup = await client.Deserializer.DeserializeAsync("");
                bool stillConnected = tup.Item1;
                string commandString = tup.Item2;

                if (!stillConnected)
                {
                    // remove the user from various data structures
                    Response result = handle_Disconnect(username);
                    Logging.DebugWriteLine(String.Format("Client {0} disconnected.", username));
                    if (!result.Success)
                        Logging.DebugWriteLine(result.Message);
                    goto exit_while;
                }

                // an array of strings parsed from the sent command string by splitting by colon :
                // hard-coded max 10 substrings. Change if needed.
                string[] commandSplit = CpParseUtilities.SplitMessage(commandString, m_stringDelimiter, 10);


                switch (commandSplit[0].ToLower())
                {
                    case "disconnect":
                        {
                            Response result = callChecked_Disconnect(commandSplit, username);
                            await lSendResponseAsync(result);
                            if (result.Success)
                            {
                                Logging.DebugWriteLine(String.Format("Client {0} disconnected gracefully.", username));
                                goto exit_while;
                            }
                            Logging.DebugWriteLine(String.Format("From server: Client {0} requested disconnect but was denied. {1}", username, result.Message));
                            break;
                        }
                    case "create_room":
                        {
                            Response result = callChecked_CreateRoom(commandSplit);
                            await lSendResponseAsync(result);
                            if (result.Success)
                                Logging.DebugWriteLine(String.Format("Client {0} created room '{1}'.", username, commandSplit[1]));
                            break;
                        }
                    case "delete_room":
                        {
                            Response result = await callChecked_DeleteRoom(commandSplit);
                            await lSendResponseAsync(result);
                            if (result.Success)
                                Logging.DebugWriteLine(String.Format("Client {0} deleted room '{1}'.", username, commandSplit[1]));
                            break;
                        }
                    case "list_rooms":
                        {
                            Response result = callChecked_ListRooms(commandSplit);
                            await lSendResponseAsync(result);
                            break;
                        }
                    case "subscribe_room":
                        {
                            Response result = callChecked_SubscribeRoom(commandSplit, username);
                            await lSendResponseAsync(result);
                            break;
                        }
                    case "unsubscribe_room":
                        {
                            Response result = callChecked_UnsubscribeRoom(commandSplit, username);
                            await lSendResponseAsync(result);
                            break;
                        }
                    case "list_room_members":
                        {
                            Response result = callChecked_ListRoomMembers(commandSplit);
                            await lSendResponseAsync(result);
                            break;
                        }
                    case "send_message_room":
                        {
                            Response result = await callChecked_MessageRoomAsync(commandSplit, username);
                            await lSendResponseAsync(result);
                            break;
                        }
                    case "send_message_personal":
                        {
                            Response result = await callChecked_MessagePersonalAsync(commandSplit, username);
                            await lSendResponseAsync(result);
                            break;
                        }

                    default:
                        await lSendResponseAsync(new Response(false, "Unknown command."));
                        Logging.DebugWriteLine(String.Format("Client sent unknown command. '{0}'", commandString));
                        break;
                }
            }

            exit_while:
            // cleanup socket resources
            client.Close();
        }

        // by default this feature is not implemented
        virtual protected Task ftClientServiceLoopAsync(SslEgg egg)
        {
            egg.SslStream.Close();
            return Task.CompletedTask;
        }




        public async Task<bool> SendEvent_Disconnect(SerializationAdapterAsync clientEventSerializer)
        {
            string commandString = "event_disconnect";
            return await clientEventSerializer.SerializeAsync(commandString);
            // There will be no response from client.
        }

        protected async Task<bool> sendEvent_MessageRoom(SerializationAdapterAsync clientEventSerializer, string roomName, string fromUsername, string messageText)
        {
            string commandString = String.Format("event_message_room:{0}:{1}:{2}", roomName, fromUsername, messageText);
            return await clientEventSerializer.SerializeAsync(commandString);
        }

        protected async Task<bool> sendEvent_MessagePersonal(SerializationAdapterAsync clientEventSerializer, string fromUsername, string messageText)
        {
            string commandString = String.Format("event_message_personal:{0}:{1}", fromUsername, messageText);
            return await clientEventSerializer.SerializeAsync(commandString);
        }

        protected async Task<bool> sendEvent_RoomDeleted(SerializationAdapterAsync clientEventSerializer, string roomName)
        {
            string commandString = String.Format("event_room_deleted:{0}", roomName);
            return await clientEventSerializer.SerializeAsync(commandString);
        }


        public static async Task<bool> SendResponseAsync(SerializationAdapterAsync clientMainSerializer, bool success, string message)
        {
            string response = String.Format("{0}{1}", 
                success ? "ack" : "nack", 
                String.IsNullOrWhiteSpace(message) ? "" : ":" + message);  // add : delimiter only if there is an actual message
            return await clientMainSerializer.SerializeAsync(response);
        }


        protected virtual Response callChecked_Connect(AmTcpClient newConnection, string commandString, out string requestedUsername)
        {
            requestedUsername = "";

            // This function is special and has to do the extra work of splitting and checking the commandstring.
            string[] commandSplit = CpParseUtilities.SplitMessage(commandString, m_stringDelimiter, 2);
            // check it is a "connect" request
            if (commandSplit[0].ToLower() != "connect")
                return new Response(false, "Expected connection request.");

            // check arguments (should be 2)
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Response(false, errMessage);

            // check if name has a colon in it (not allowed)
            requestedUsername = commandSplit[1];
            if (requestedUsername.Contains(m_stringDelimiter))
                return new Response(false, String.Format("Name shall not contain the delimiter character '{0}'.", m_stringDelimiter));

            // attempt to register the user
            return handle_Connect(requestedUsername, newConnection);
        }
        abstract protected Response handle_Connect(string username, AmTcpClient newConnection);


        protected virtual Response callChecked_Disconnect(string[] commandSplit, string username)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 1, 1, ref errMessage))
                return new Response(false, errMessage);

            return handle_Disconnect(username);
        }
        abstract protected Response handle_Disconnect(string username);


        protected virtual Response callChecked_CreateRoom(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Response(false, errMessage);

            string roomName = commandSplit[1];

            Debug.Assert(m_stringDelimiter == ':');
            if (roomName.Contains(m_stringDelimiter))
                return new Response(false, "Room name shall not contain a colon.");

            return handle_CreateRoom(roomName);
        }
        abstract protected Response handle_CreateRoom(string roomName);


        protected virtual async Task<Response> callChecked_DeleteRoom(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Response(false, errMessage);

            string roomName = commandSplit[1];

            return await handle_DeleteRoom(roomName);
        }
        abstract protected Task<Response> handle_DeleteRoom(string roomName);


        protected virtual Response callChecked_ListRooms(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 1, 1, ref errMessage))
                return new Response(false, errMessage);

            // get the room list from upper layer
            IEnumerable<string> roomList;
            Response res = handle_ListRooms(out roomList);
            // the handler actually can't fail, but leave this check in anyway
            if (!res.Success)
                return res;

            // pack the room list into the response message
            string roomsString = String.Join(":", roomList);
            return new Response(true, roomsString);
        }
        abstract protected Response handle_ListRooms(out IEnumerable<string> roomList);


        protected virtual Response callChecked_SubscribeRoom(string[] commandSplit, string username)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Response(false, errMessage);

            string roomName = commandSplit[1];

            return handle_SubscribeRoom(roomName, username);
        }
        abstract protected Response handle_SubscribeRoom(string roomName, string username);


        protected virtual Response callChecked_UnsubscribeRoom(string[] commandSplit, string username)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Response(false, errMessage);

            string roomName = commandSplit[1];

            return handle_UnsubscribeRoom(roomName, username);
        }
        abstract protected Response handle_UnsubscribeRoom(string roomName, string username);


        protected virtual Response callChecked_ListRoomMembers(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Response(false, errMessage);
            
            string roomName = commandSplit[1];

            // get the member list from the upper layer
            IEnumerable<string> memberList;
            Response res = handle_ListRoomMembers(roomName, out memberList);
            // check result
            if (!res.Success)
                return res;            
            
            // pack the member list into the response
            string membersString = String.Join(":", memberList);
            return new Response(true, membersString);
        }
        abstract protected Response handle_ListRoomMembers(string roomName, out IEnumerable<string> memberList);


        protected virtual async Task<Response> callChecked_MessageRoomAsync(string[] commandSplit, string fromUsername)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 3, ref errMessage))
                return new Response(false, errMessage);

            CpParseUtilities.RecombineSplitMessage(ref commandSplit, 2);

            string roomName    = commandSplit[1];
            string messageText = commandSplit[2];

            return await handle_MessageRoomAsync(roomName, fromUsername, messageText);
        }
        abstract protected Task<Response> handle_MessageRoomAsync(string roomName, string fromUsername, string messageText);


        protected virtual async Task<Response> callChecked_MessagePersonalAsync(string[] commandSplit, string fromUsername)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 3, ref errMessage))
                return new Response(false, errMessage);

            CpParseUtilities.RecombineSplitMessage(ref commandSplit, 2);

            string toUsername  = commandSplit[1];
            string messageText = commandSplit[2];

            return await handle_MessagePersonalAsync(toUsername, fromUsername, messageText);
        }
        abstract protected Task<Response> handle_MessagePersonalAsync(string toUsername, string fromUsername, string messageText);




        protected virtual async Task<Tuple<bool, Response>> callChecked_FileUpAsync(FtClient ftClient, string[] commandSplit)
        {
            return new Tuple<bool, Response>(true, new Response(false, "File transfer not supported by this server."));
        }
        protected virtual async Task<Tuple<bool, Response>> callChecked_FileDownAsync(FtClient ftClient, string[] commandSplit)
        {
            return new Tuple<bool, Response>(true, new Response(false, "File transfer not supported by this server."));
        }

        

    }
}
