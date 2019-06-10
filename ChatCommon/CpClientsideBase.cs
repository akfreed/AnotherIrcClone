// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Collections.Generic;

using System.Net;
using System.Threading;


namespace ChatCommon
{ 
    /// <summary>
    /// This class runs handles the Chat Protocol between the client and server.
    /// It runs the read thread.
    /// Some callbacks must be implemented by the inheriting class.
    /// </summary>
    abstract public class CpClientsideBase
    {
        protected const char m_stringDelimiter = ':';

        private AmTcpDemuxer           m_server;
        private SerializationAdapter   m_serverSerializer;
        private DeserializationAdapter m_serverDeserializer;
        private DeserializationAdapter m_serverEventDeserializer;

        private Thread m_eventChannelReadThread;

        private bool m_disposed = false;


        public IPEndPoint RemoteEndPoint  { get { return m_server.RemoteEndPoint; } }
        public char       StringDelimiter { get { return m_stringDelimiter;       } }


        // constructor
        public CpClientsideBase(SslEgg egg)
        {
            m_server                  = new AmTcpDemuxer(egg);
            m_serverSerializer        = new SerializationAdapter(m_server.WriteStream);
            m_serverDeserializer      = new DeserializationAdapter(m_server.ReadStream);
            m_serverEventDeserializer = new DeserializationAdapter(m_server.ReadEventStream);

            m_eventChannelReadThread = new Thread(eventChannelReadLoop);
            m_eventChannelReadThread.Start();
        }


        public void Close()
        {
            if (!m_disposed)
            {
                m_server.Close();
                m_disposed = true;
                m_eventChannelReadThread.Join();
            }
        }



        private void eventChannelReadLoop()
        {
            
            while (true)
            {
                // the command string read from client
                string commandString = "";
                bool stillConnected = m_serverEventDeserializer.Deserialize(ref commandString);
                if (!stillConnected)
                {
                    Logging.DebugWriteLine("Server disconnected.");
                    goto loop_exit;
                }

                // an array of strings parsed from the sent command string by splitting by colon :
                // hard-coded max 10 substrings. Change if needed.
                string[] commandSplit = CpParseUtilities.SplitMessage(commandString, m_stringDelimiter, 10);


                switch (commandSplit[0].ToLower())
                {
                    case "event_disconnect":
                        {
                            Response response = receiveEvent_Disconnect_checked(commandSplit);
                            goto loop_exit;
                            // [break]
                        }
                    case "event_message_room":
                        {
                            Response resonse = receiveEvent_MessageRoom_checked(commandSplit);
                            break;
                        }
                    case "event_message_personal":
                        {
                            Response response = receiveEvent_MessagePersonal_checked(commandSplit);
                            break;
                        }
                    case "event_room_deleted":
                        {
                            Response response = receiveEvent_RoomDeleted_checked(commandSplit);
                            break;
                        }

                    default:
                        Logging.DebugWriteLine(String.Format("Server sent unknown event. '{0}'.", commandString));
                        break;
                }
            }

            loop_exit:
            m_server.Close();
        }


        private bool readAck(out Response response)
        {
            return ReadAck(m_serverDeserializer, m_stringDelimiter, out response);
        }
        public static bool ReadAck(DeserializationAdapter deserializer, char delimiter, out Response response)
        {
            response = new Response(false, "");

            string reply = "";
            bool stillConnected = deserializer.Deserialize(ref reply);
            if (!stillConnected)
            {
                response.Message = "Server disconnected.";
                return false;
            }

            response = ParseAck(reply, delimiter);
            return true;
        }
        public static Response ParseAck(string message, char delimiter)
        {
            Response response = new Response(false, "");

            // split and remove whitespace
            string[] responseSplit = CpParseUtilities.SplitMessage(message, delimiter, 2);

            if (responseSplit.Length > 1)
                response.Message = responseSplit[1];
            else
                response.Message = "";

            if (responseSplit[0].ToLower() == "ack")
            {
                response.Success = true;
            }
            else if (responseSplit[0].ToLower() == "nack")
            {
                if (String.IsNullOrWhiteSpace(response.Message))
                    response.Message = "Server sent no explanation.";
            }
            else
            {
                response.Message = String.Format("Server sent unexpected response '{0}'.", message);
            }

            return response;
        }

        public bool Send_Connect(string desiredName, out Response response)
        {
            // send the magic byte to indicate chat
            bool stillConnected = m_server.WriteStream.Write(new byte[1] { (byte)CpServersideBase.ConnectionMode.CHAT_PROTOCOL }, 0, 1);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            // send the registration commandstring
            string commandString = String.Format("connect:{0}", desiredName);
            stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }

        public bool Send_Disconnect(out Response response)
        {
            string commandString = "disconnect";
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }

        public bool Send_CreateRoom(string roomName, out Response response)
        {
            string commandString = String.Format("create_room:{0}", roomName);
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }

        public bool Send_DeleteRoom(string roomName, out Response response)
        {
            string commandString = String.Format("delete_room:{0}", roomName);
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }

        public bool Send_ListRooms(out Response response, out IEnumerable<string> roomList)
        {
            roomList = null;

            // send the command string
            string commandString = "list_rooms";
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;  // connection was closed
            }

            // the server response will contain the room list in the message string
            stillConnected = readAck(out response);
            if (!stillConnected)
                return false;  // connection was closed

            // If server acked successful, parse the message. (Actually, this operation is always successful, but it's good to check in case that ever changes.)
            if (response.Success)
                roomList = CpParseUtilities.SplitMessage(response.Message, m_stringDelimiter);

            return true;  // connection still active
        }

        public bool Send_SubscribeRoom(string roomName, out Response response)
        {
            string commandString = String.Format("subscribe_room:{0}", roomName);
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }

        public bool Send_UnsubscribeRoom(string roomName, out Response response)
        {
            string commandString = String.Format("unsubscribe_room:{0}", roomName);
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }

        public bool Send_ListRoomMembers(string roomName, out Response response, out IEnumerable<string> roomMembers)
        {
            roomMembers = null;

            // send the command string
            string commandString = String.Format("list_room_members:{0}", roomName);
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;  // connection was closed
            }

            // the server response will contain the room member list in the message string
            stillConnected = readAck(out response);
            if (!stillConnected)
                return false;  // connection was closed

            // If server acked successful, parse the message.
            if (response.Success)
                roomMembers = CpParseUtilities.SplitMessage(response.Message, m_stringDelimiter);

            return true;  // connection still active
        }

        public bool Send_MessageRoom(string roomName, string messageText, out Response response)
        {
            string commandString = String.Format("send_message_room:{0}:{1}", roomName, messageText);
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }

        public bool Send_MessagePersonal(string toUsername, string messageText, out Response response)
        {
            string commandString = String.Format("send_message_personal:{0}:{1}", toUsername, messageText);
            bool stillConnected = m_serverSerializer.Serialize(commandString);
            if (!stillConnected)
            {
                response = null;
                return false;
            }
            return readAck(out response);
        }


        protected virtual Response receiveEvent_Disconnect_checked(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 1, 1, ref errMessage))
                return new Response(false, errMessage);

            return handle_Disconnect();
        }
        abstract protected Response handle_Disconnect();

        protected virtual Response receiveEvent_MessageRoom_checked(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 4, ref errMessage))
                return new Response(false, errMessage);

            CpParseUtilities.RecombineSplitMessage(ref commandSplit, 3);

            string fromRoomName = commandSplit[1];
            string fromUsername = commandSplit[2];
            string messageText  = commandSplit[3];

            return handle_MessageRoom(fromRoomName, fromUsername, messageText);
        }
        abstract protected Response handle_MessageRoom(string fromRoomName, string fromUsername, string messageText);

        protected virtual Response receiveEvent_MessagePersonal_checked(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 3, ref errMessage))
                return new Response(false, errMessage);

            CpParseUtilities.RecombineSplitMessage(ref commandSplit, 2);

            string fromUsername = commandSplit[1];
            string messageText = commandSplit[2];

            return handle_MessagePersonal(fromUsername, messageText);
        }
        abstract protected Response handle_MessagePersonal(string fromUsername, string messageText);

        protected virtual Response receiveEvent_RoomDeleted_checked(string[] commandSplit)
        {
            string errMessage = "";
            if (!CpParseUtilities.CheckArgCount(commandSplit, 2, 2, ref errMessage))
                return new Response(false, errMessage);

            string roomName = commandSplit[1];

            return handle_RoomDeleted(roomName);
        }
        abstract protected Response handle_RoomDeleted(string roomName);

    }
}
