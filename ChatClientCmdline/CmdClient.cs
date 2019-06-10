// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Collections.Generic;
using System.Linq;

using System.Net.Sockets;
using System.Net.Security;
using System.IO;

using ChatCommon;


namespace ChatClientCmdline
{
    /// <summary>
    /// This is the implementation of the client.
    /// It provides a command line interface for the user.
    /// It handles connection to the server, UI, and implements the callbacks required from the base class.
    /// </summary>
    class CmdClient : CpClientsideBase
    {
        private const string m_targetCertHost = @"Freed Dev Cert";

        // program entry point
        public static void Main(string[] args)
        {
            Logging.Tag            = "[Client]";
            Logging.DebugTag       = "[Client DEBUG]";
            FtFileManager.BasePath = "";

            Run();

            Console.Write("\n\n\nEnd of program. Press any key to exit...");
            Console.ReadKey(true);
        }


        // private data
        private HashSet<string> m_rooms     = new HashSet<string>();
        private HashSet<string> m_show      = new HashSet<string>();
        private HashSet<string> m_cast      = new HashSet<string>();
        private bool            m_connected = true;
        private object          m_rscLock   = new object();


        // constructor
        public CmdClient(SslEgg egg)
            : base(egg)
        {
        }


        public static void Run()
        {
            string host;
            int port;
            string username;

            // get input from user
            if (!inputConnectionInfo(out host, out port, out username))
                return;

            // connect
            SslEgg sslEgg;
            try
            {
                TcpClient tcpSocket = new TcpClient(host, port);

                // establish ssl
                sslEgg = new SslEgg(tcpSocket);
                sslEgg.AuthenticateAsClient(m_targetCertHost);
            }
            catch (Exception e)
            {
                Logging.WriteLine(String.Format("Unable to establish connection to {0}:{1} : {2}", host, port, e.Message));
                return;
            }
            
            // create client instance
            CmdClient cmdClient = new CmdClient(sslEgg);

            // run main user I/O loop
            cmdClient.mainInputLoop(username);
        }


        // read host, port, and username from user
        private static bool inputConnectionInfo(out string host, out int port, out string username)
        {
            // get host and port from user
            Console.Write("Host: ");
            host = Console.ReadLine().Trim();
            if (String.IsNullOrWhiteSpace(host))
                host = "127.0.0.1";

            Console.Write("Port: ");
            string portString = Console.ReadLine().Trim();
            if (String.IsNullOrWhiteSpace(portString))
                portString = "12589";
            port = 12589;
            try
            {
                port = Convert.ToInt32(portString);
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid port number.");
                username = null;
                return false;
            }

            // get username
            Console.Write("Username: ");
            username = Console.ReadLine().Trim();

            return true;
        }


        // main user I/O loop
        private void mainInputLoop(string username)
        {
            // register with server
            Response response;
            if (!Send_Connect(username, out response))
            {
                Logging.WriteLine("Unable to establish connection. Server disconnected without replying.");
                Close();
                return;
            }
            else if (!response.Success)
            {
                Logging.WriteLine("Unable to establish connection. " + response.Message);
                Close();
                return;
            }

            Logging.WriteLine(String.Format("Connection esablished to {0}", RemoteEndPoint.ToString()  ));

            bool stillConnected = true;

            // main loop
            while (stillConnected)
            {
                string message = Console.ReadLine().Trim();

                // split and strip whitespace
                string[] messageSplit = CpParseUtilities.SplitMessage(message, ' ');
                string command = messageSplit[0].ToLower();
                string roomName = messageSplit.Length > 1 ? messageSplit[1] : null;

                // note that this doesn't catch all spurious disconnects.
                // The ones that occur in the middle of an operation shall result in the operation returning false
                lock (m_rscLock)
                {
                    if (!m_connected)
                        break;
                }

                // handle commands
                if (command == "/exit" || command == "/quit")
                {
                    if (messageSplit.Length != 1)
                        Logging.WriteLine("Disconnect from server. Usage: /quit");
                    else if (!Send_Disconnect(out response))
                    {
                        Logging.WriteLine("While attempting to disconnect, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                    {
                        Logging.WriteLine("Successfully disconnected.");
                        stillConnected = false;
                    }
                }
                else if (command == "/create")
                {
                    if (messageSplit.Length != 2)
                        Logging.WriteLine("Create a room. Usage: /create roomname");
                    else if (!Send_CreateRoom(roomName, out response))
                    {
                        Logging.WriteLine("After attempting to create room, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                        Logging.WriteLine(String.Format("Created room '{0}'.", roomName)); 
                }
                else if (command == "/delete")
                {
                    if (messageSplit.Length != 2)
                        Logging.WriteLine("Delete a room. Usage: /delete roomname");
                    else if (!Send_DeleteRoom(roomName, out response))
                    {
                        Logging.WriteLine("After attempting to delete room, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                        Logging.WriteLine(String.Format("Deleted room '{0}'.", roomName));
                }
                else if (command == "/rooms" || command == "/r")
                {
                    IEnumerable<string> roomList;
                    if (messageSplit.Length != 1)
                        Logging.WriteLine("List all rooms. Usage: /rooms");
                    else if (!Send_ListRooms(out response, out roomList))
                    {
                        Logging.WriteLine("After attempting to get room list, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                    {
                        string roomsString = "\nRoom List:\n    - " + String.Join("\n    - ", roomList);
                        Logging.WriteLine(roomsString);
                    }
                }
                else if (command == "/members" || command == "/m")
                {
                    IEnumerable<string> memberList;
                    if (messageSplit.Length != 2)
                        Logging.WriteLine("List the members of the specified room. Usage: /members roomname");
                    else if (!Send_ListRoomMembers(roomName, out response, out memberList))
                    {
                        Logging.WriteLine("After attempting to get room member list, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                    {
                        string memberListString = String.Format("\nRoom '{0}' Members:\n    - {1}",
                            roomName,
                            String.Join("\n    - ", memberList));
                        Logging.WriteLine(memberListString);
                    }
                }
                else if (command == "/subscribe" || command == "/sub")
                {
                    if (messageSplit.Length != 2)
                        Logging.WriteLine("Begin receiving room broadcasts (use /select or /show to see them). Usage: /subscribe roomname");
                    else if (!Send_SubscribeRoom(roomName, out response))
                    {
                        Logging.WriteLine("After attempting to subscribe to room, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                    {
                        Logging.WriteLine(String.Format("Subscribed to room '{0}'.", roomName));
                        lock (m_rscLock)
                            m_rooms.Add(roomName);
                    }
                }
                else if (command == "/unsubscribe" || command == "/unsub")
                {
                    if (messageSplit.Length != 2)
                        Logging.WriteLine("Stop receiving room broadcasts. Usage: /unsubscribe roomname");
                    else if (!Send_UnsubscribeRoom(roomName, out response))
                    {
                        Logging.WriteLine("After attempting to unsubscribe from room, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                    {
                        Logging.WriteLine(String.Format("Unsubscribed from room '{0}'.", roomName));
                        lock (m_rscLock)
                        { 
                            m_rooms.Remove(roomName);
                            m_show.Remove(roomName);
                            m_cast.Remove(roomName);
                        }

                    }
                }
                else if (command == "/select" || command == "/sel" || command == "/s" || command == "/join" || command == "/j")
                {
                    if (messageSplit.Length != 2)
                        Logging.WriteLine("Show and cast-to the specified room, subscribing to the room if needed. Usage: /select roomname");
                    else
                    {
                        // subscribe if needed
                        bool subbed = false;
                        lock (m_rscLock)
                            subbed = m_rooms.Contains(roomName);
                        if (!subbed)
                        {
                            if (!Send_SubscribeRoom(roomName, out response))
                            {
                                Logging.WriteLine("After attempting to subscribe to room, server disconnected without replying.");
                                stillConnected = false;
                            }
                            else if (!response.Success)
                            {
                                Logging.WriteLine(response.Message);
                            }
                            else
                            {
                                Logging.WriteLine(String.Format("Subscribed to room '{0}'.", roomName));
                                lock (m_rscLock)
                                    m_rooms.Add(roomName);
                                subbed = true;
                            }
                        }

                        if (subbed)
                        {
                            Logging.WriteLine(String.Format("Now showing and casting room '{0}' exclusively.", roomName));
                            lock (m_rscLock)
                            {
                                m_show.Clear();
                                m_show.Add(roomName);
                                m_cast.Clear();
                                m_cast.Add(roomName);
                            }
                        }
                    }
                }
                else if (command == "/show")
                {
                    lock (m_rscLock)
                    {
                        if (messageSplit.Length != 2)
                            Logging.WriteLine("Begin displaying messages from the specified room. Usage: /show roomname");  // DEV: add multiroom
                        else if (!m_rooms.Contains(roomName))
                            Logging.WriteLine(String.Format("You are not subscribed to room '{0}'. Use /subscribe.", roomName));
                        else if (m_show.Contains(roomName))
                            Logging.WriteLine(String.Format("Already showing messages from room '{0}'.", roomName));
                        else
                        {
                            Logging.WriteLine(String.Format("Now showing room '{0}'.", roomName));
                            m_show.Add(roomName);
                        }
                    }
                }
                else if (command == "/hide")
                {
                    lock (m_rscLock)
                    {
                        if (messageSplit.Length != 2)
                            Logging.WriteLine("Stop displaying messages from the specified room. Usage: /hide roomname");  // DEV: add multiroom
                        else if (!m_rooms.Contains(roomName))
                            Logging.WriteLine(String.Format("You are not subscribed to room '{0}'. Use /subscribe.", roomName));
                        else if (!m_show.Contains(roomName))
                            Logging.WriteLine(String.Format("Already hiding messages from room '{0}'.", roomName));
                        else
                        {
                            Logging.WriteLine(String.Format("Now hiding room '{0}'.", roomName));
                            m_show.Remove(roomName);
                            m_cast.Remove(roomName);  // should only cast to a room you are showing
                        }
                    }
                }
                else if (command == "/cast" || command == "/c")
                {
                    if (messageSplit.Length < 2)
                        Logging.WriteLine("Specify which room(s) will be sent your messages. Usage: /cast roomname [roomname2] [roomname3...]");
                    else
                    {
                        lock (m_rscLock)
                        {
                            bool success = true;
                            // the user must be subscribed-to and showing every room
                            var rooms = messageSplit.Skip(1);
                            foreach (var room in rooms)
                            {
                                if (!m_rooms.Contains(room))
                                {
                                    Logging.WriteLine(String.Format("You are not subscribed to room '{0}'. Use /subscribe.", room));
                                    success = false;
                                }
                                else if (!m_show.Contains(room))
                                {
                                    Logging.WriteLine(String.Format("You must be showing room '{0}' to cast to it. Use /show.", room));
                                    success = false;
                                }
                            }
                            if (success)
                            {
                                // reset the cast list
                                m_cast.Clear();
                                foreach (var room in rooms)
                                    m_cast.Add(room);

                                string castRooms = String.Join(", ", rooms);
                                Logging.WriteLine("Now casting exclusively to: " + castRooms);
                            }
                        }
                    }
                }  // end if /cast
                else if (command == "/private" || command == "/p")
                {
                    if (messageSplit.Length < 2)
                        Logging.WriteLine("Send a private message. Usage: /private username blah blah blah");
                    else if (!Send_MessagePersonal(messageSplit[1],                         // username
                                                   String.Join(" ", messageSplit.Skip(2)),  // message
                                                   out response))                           // result
                    {
                        Logging.WriteLine("After attempting to send the private message, server disconnected without replying.");
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                    {
                    }
                }
                else if (command == "/upload")
                {
                    if (messageSplit.Length < 2)
                        Logging.WriteLine("Upload a file. Usage: /upload localpath");
                    else if (!Send_FileUp(messageSplit[1], out response))
                    {
                        Logging.WriteLine("Server disconnected in the middle of upload. " + response.Message);
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                        Logging.WriteLine("Upload successful.");
                }
                else if (command == "/download")
                {
                    if (messageSplit.Length < 2)
                        Logging.WriteLine("Download a file. Usage: /download remotepath");
                    else if (!Send_FileDown(messageSplit[1], out response))
                    {
                        Logging.WriteLine("Server disconnected in the middle of download. " + response.Message);
                        stillConnected = false;
                    }
                    else if (!response.Success)
                        Logging.WriteLine(response.Message);
                    else
                        Logging.WriteLine("Download successful.");
                }
                else if (command == "/help" || command == "/h")
                {
                    string helpString = getHelpString();
                    Logging.WriteLine(helpString);
                }
                else if (command.Length > 0 && command[0] == '/')
                {
                    Logging.WriteLine("Unknown command: " + command);
                }
                else  // else send message to the casted rooms
                {
                    if (m_cast.Count == 0)
                        Logging.WriteLine("No rooms selected for casting. Use /select or /cast");

                    foreach (var room in m_cast)
                    {
                        if (!Send_MessageRoom(room, message, out response))
                        {
                            Logging.WriteLine("After attempting to send message, server disconnected without replying.");
                            stillConnected = false;
                            break;
                        }
                        else if (!response.Success)
                            Logging.WriteLine(response.Message);
                        else
                        {
                        }
                    }
                }

            }

            
            Close();
        }  // end main loop



        protected override Response handle_MessageRoom(string fromRoomName, string fromUsername, string messageText)
        {
            string timestamp = Logging.GetTimestamp();
            lock (m_rscLock)
            { 
                if (m_show.Contains(fromRoomName))
                    Console.WriteLine(String.Format("{0} ({1}) {2}: {3}", timestamp, fromRoomName, fromUsername, messageText));
            }
            return new Response(true, "");
        }

        protected override Response handle_MessagePersonal(string fromUsername, string messageText)
        {
            string timestamp = Logging.GetTimestamp();
            Console.WriteLine(String.Format("{0} (pm) {1}: {2}", timestamp, fromUsername, messageText));
            return new Response(true, "");
        }

        protected override Response handle_Disconnect()
        {
            string timestamp = Logging.GetTimestamp();
            Console.WriteLine(timestamp + " Server disconnected gracefully.");
            lock (m_rscLock)
            {
                m_rooms.Clear();
                m_show.Clear();
                m_cast.Clear();
                m_connected = false;
            }
            return new Response(true, "");
        }

        protected override Response handle_RoomDeleted(string roomName)
        {
            string timestamp = Logging.GetTimestamp();
            Console.WriteLine(String.Format("{0} Room '{1}' was deleted.", timestamp, roomName));
            lock (m_rscLock)
            {
                m_rooms.Remove(roomName);
                m_show.Remove(roomName);
                m_cast.Remove(roomName);
            }
            return new Response(true, "");
        }


        // Open a new connection to the server (Unfortunately, CP wasn't designed with FT in mind)
        private SslStream connectFt()
        {
            // connect using existing remote endpoint info
            if (RemoteEndPoint == null)
                return null;

            try
            {
                TcpClient tcpServer = new TcpClient(RemoteEndPoint.Address.ToString(), RemoteEndPoint.Port);
                SslEgg egg = new SslEgg(tcpServer);
                egg.AuthenticateAsClient(m_targetCertHost);
                // write the magic byte (to indicate ftp)
                egg.SslStream.Write(new byte[1] { (byte)CpServersideBase.ConnectionMode.FILE_TRANSFER }, 0, 1);
                return egg.SslStream;
            }
            catch
            {
                return null;
            }
        }


        public bool Send_FileUp(string localPath, out Response response)
        {
            // open the file for reading
            string errMessage = "";
            using (Stream fileIn = FtFileManager.OpenForRead(localPath, out errMessage))
            {
                if (fileIn == null)
                {
                    response = new Response(false, errMessage);
                    return true;
                }

                // Open a new connection to the server
                using (SslStream sslServer = connectFt())
                {
                    if (sslServer == null)
                    {
                        response = new Response(false, "Could not connect to server for file transfer.");
                        return false;
                    }

                    FtClient ftClient = new FtClient(sslServer);

                    string filename = Path.GetFileName(localPath);

                    // write the command string
                    if (!ftClient.Serializer.Serialize(String.Format("file_up:{0}", filename)))
                    {  // server disconnected unexpectedly
                        response = null;
                        return false;
                    }

                    // expect ack
                    if (!ReadAck(ftClient.Deserializer, StringDelimiter, out response))
                        return false;  // server disconnected unexpectedly
                    // server can't receive file
                    if (!response.Success)
                        return true;

                    // send the file
                    var task = FtFileManager.FileSendAsync(sslServer, fileIn);
                    task.Wait();
                    if (!task.Result)
                        return false;

                    // expect ack
                    // currently doesn't handle failed transfers DEV: fix
                    if (!ReadAck(ftClient.Deserializer, StringDelimiter, out response))
                        return false;

                    return true;
                }
            }

        }


        public bool Send_FileDown(string remotePath, out Response response)
        {
            // open a file for writing
            const string downloadDir = "Downloads";
            if (!Directory.Exists(downloadDir))
            {
                try
                {
                    Directory.CreateDirectory(downloadDir);
                }
                catch (Exception e)
                {
                    response = new Response(false, String.Format("Unable to create base directory '{0}': '{1}'", downloadDir, e.Message));
                    return true;
                }
            }
            string filename = Path.GetFileName(remotePath);
            string localPath = Path.Combine(downloadDir, filename);
            string errMessage = "";
            using (Stream fileOut = FtFileManager.OpenForWrite(localPath, out errMessage))
            {
                if (fileOut == null)
                {
                    response = new Response(false, errMessage);
                    return true;
                }

                // Open a new connection to the server
                using (SslStream sslServer = connectFt())
                {
                    if (sslServer == null)
                    {
                        response = new Response(false, "Could not connect to server for file transfer.");
                        return false;
                    }

                    FtClient ftClient = new FtClient(sslServer);

                    // write the command string
                    bool stillConnected = ftClient.Serializer.Serialize(String.Format("file_down:{0}", remotePath));
                    if (!stillConnected)
                    {
                        response = null;
                        return false;
                    }

                    // expect ack
                    if (!ReadAck(ftClient.Deserializer, StringDelimiter, out response))
                        return false;  // server disconnected unexpectedly
                    // server can't send file
                    if (!response.Success)
                        return true;

                    // receive the file
                    var task = FtFileManager.FileReceiveAsync(ftClient.Client, fileOut);
                    task.Wait();
                    if (!task.Result.Item1)
                        return false;

                    // expect ack
                    if (!ReadAck(ftClient.Deserializer, StringDelimiter, out response))
                        return false;

                    return true;
                }
            }
        }


        private string getHelpString()
        {
            return @"Commands:
/create              roomname                  - create room
/delete              roomname                  - delete room
/rooms       /r                                - list rooms
/members     /m      roomname                  - list room members
/subscribe   /sub    roomname                  - begin receiving room messages
/unsubscribe /unsub  roomname                  - stop receiving room messages
/select      /s      roomname                  - /sub, /show, and /cast room
/join        /j      roomname                  - alias for select   
/show                roomname                  - show messages from room
/hide                roomname                  - don't show messages from rooms
/cast        /c      roomname [roomname2...]   - write to selected rooms
/private     /p      username message          - private message
/upload              filepath                  - upload a file
/download            filename                  - download a file
/exit        /quit                             - log off";
        }


    }
}
