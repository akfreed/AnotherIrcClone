// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

using ChatCommon;


namespace ChatServer
{
    /// <summary>
    /// The server application-level imlementation.
    /// Handles connections and rooms and implements the callbacks required by the base class.
    /// </summary>
    class Server : FtServerside
    {
        private const string m_certificatePath = @"cert.pfx";


        // main
        public static void Main(string[] args)
        {
            Logging.Tag      = "[Server]";
            Logging.DebugTag = "[Server DEBUG]";

            int port = getPortFromUser();
            if (port > 0)
            {
                // start server
                Server server = new Server(port);
                server.Start();
                Logging.WriteLine(String.Format("Server running on port {0}. Press ESC to quit.", port));

                // stop server if user presses escape
                while (Console.ReadKey(true).Key != ConsoleKey.Escape)
                    ;

                server.Stop();
                Logging.WriteLine("Server stopped.");
            }

            Console.Write("Press any key to exit...");
            Console.ReadKey(true);
        }

        private static int getPortFromUser()
        {
            Console.Write("Enter port: ");
            string portString = Console.ReadLine();

            if (String.IsNullOrEmpty(portString))
                return 12589;

            try
            {
                int port = Convert.ToInt16(portString);
                if (port > 0)
                    return port;
            }
            catch
            {
            }

            Logging.WriteLine(String.Format("Invalid port number: {0}", portString));
            return -1;
        }



        // private data
        private int m_port;
        private TcpListener m_listener;
        private volatile bool m_active = false;
        private ConcurrentDictionary<string, AmTcpClient> m_clients = new ConcurrentDictionary<string, AmTcpClient>();
        private Random rng = new Random();

        private RoomManager m_roomManager = new RoomManager();
        

        // constructor
        public Server(int port)
        {
            m_port = port;
        }


        public void Start()
        {
            m_listener = new TcpListener(IPAddress.Any, m_port);
            m_listener.Start();
            m_active = true;
            // set the callback if a client connects
            m_listener.BeginAcceptTcpClient(acceptPendingClientCallback, null);
        }

        public void Stop()
        {
            m_active = false;
            m_listener.Stop();
            Thread.Sleep(100);  // DEV: crap code
            disconnectAll();
            // check again
            if (!m_clients.IsEmpty)
                disconnectAll();
        }

        /// <summary>
        /// Callback for when a client tries to connect.
        /// </summary>
        /// <param name="ar"></param>
        private void acceptPendingClientCallback(IAsyncResult ar)
        {
            TcpClient connection = null;
            try
            {
                // accept the client
                connection = m_listener.EndAcceptTcpClient(ar);
            }
            catch
            {
                // if m_active==false, this is an expected exception due to shutdown of the listener
                if (!m_active)
                    return;
                // otherwise, this is an unexpected exception and needs to be re-raised
                throw;
            }

            // sanity check
            Debug.Assert(connection != null);

            startClientServiceLoopAsync(connection);  // DEV: save tasks to join later? could help get rid of the crap code

            // set this callback again
            m_listener.BeginAcceptTcpClient(acceptPendingClientCallback, null);
        }


        private async void startClientServiceLoopAsync(TcpClient tcpClient)
        {
            // establish ssl
            SslEgg egg = new SslEgg(tcpClient);
            try
            {
                await egg.AuthenticateAsServerAsync(m_certificatePath);
            }
            catch (Exception e)
            {
                Logging.DebugWriteLine(String.Format("Unable to establish secure connection with {0} : {1}", tcpClient.Client.RemoteEndPoint.ToString(), e.Message));
                tcpClient.Close();
                return;
            }

            // begin service loop
            await clientServiceLoop(egg);
        }


        public bool AddClient(string username, AmTcpClient connection)
        {
            return m_clients.TryAdd(username, connection);
        }
        public bool FindClient(string username, out AmTcpClient connection)
        {
            return m_clients.TryGetValue(username, out connection);
        }
        public bool RemoveClient(string username)
        {
            // passthrough
            AmTcpClient unused;
            return RemoveClient(username, out unused);
        }
        public bool RemoveClient(string username, out AmTcpClient clientConnection)
        {
            m_roomManager.UnsubscribeAllRooms(username);
            return m_clients.TryRemove(username, out clientConnection);
        }

        public async Task<Response> KickAsync(string username)
        {
            AmTcpClient client;
            if (!m_clients.TryGetValue(username, out client))
                return new Response(false, String.Format("User '{0}' not found (KickAsync).", username));
            // send disconnect message
            await SendEvent_Disconnect(client.EventSerializer);  // don't need to catch result, as we are closing anyway
            // just close the connection. The read service loop will take care of calling Remove
            client.Close();
            return new Response(true, "");
        }

        // The server should be stopped before calling.
        // Due to race conditions, it should be stopped for "a while" before calling this.
        // Check that m_clients is indeed empty after calling
        private void disconnectAll()
        {
            List<string> toRemove = new List<string>();
            foreach (var clientKV in m_clients)
                toRemove.Add(clientKV.Key);

            // kick all users
            List<Task<Response>> tasks = new List<Task<Response>>();
            foreach (var username in toRemove)
            {
                var t = KickAsync(username);
                tasks.Add(t);                
            }
            Task.WaitAll(tasks.ToArray());
            // logging
            foreach (var t in tasks)
            {
                if (!t.Result.Success)
                    Logging.DebugWriteLine(t.Result.Message);
            }

            // crap code DEV: remove
            for (int i = 0; i < 50 && !m_clients.IsEmpty; ++i)
                Thread.Sleep(50);
        }


        protected override Response handle_Connect(string username, AmTcpClient newConnection)
        {
            if (AddClient(username, newConnection))
                return new Response(true, "");
            else
                return new Response(false, String.Format("Username '{0}' is taken.", username));
        }

        protected override Response handle_Disconnect(string username)
        {
            if (RemoveClient(username))
                return new Response(true, "");
            else
                return new Response(false, String.Format("Unable to disconnect user '{0}'. Please try again.", username));
        }


        protected override Response handle_CreateRoom(string roomName)
        {
            return m_roomManager.AddRoom(roomName);
        }

        protected override async Task<Response> handle_DeleteRoom(string roomName)
        {
            // delete the room and get the list of users who used to be in it
            IEnumerable<string> members;
            Response res = m_roomManager.DeleteRoom(roomName, out members);
            // if the room was successfully deleted, notify the old members
            if (res.Success)
            {
                List<Task> tasks = new List<Task>();
                foreach (var name in members)
                {
                    AmTcpClient client;
                    if (!m_clients.TryGetValue(name, out client))
                        Logging.DebugWriteLine(String.Format("While deleting room '{0}', server was unable to notify member '{1}'.", roomName, name));
                    else
                    {
                        Task t = sendEvent_RoomDeleted(client.EventSerializer, roomName);  // don't need to catch result because client read loop will handle disconnects
                        tasks.Add(t);
                    }
                }
                await Task.WhenAll(tasks.ToArray());
            }
            return res;
        }

        protected override Response handle_ListRooms(out IEnumerable<string> roomList)
        {
            roomList = m_roomManager.GetRoomList();
            // we are a no-fail function
            return new Response(true, "");
        }

        protected override Response handle_SubscribeRoom(string roomName, string username)
        {
            return m_roomManager.SubscribeRoom(roomName, username);
        }

        protected override Response handle_UnsubscribeRoom(string roomName, string username)
        {
            return m_roomManager.UnsubscribeRoom(roomName, username);
        }

        protected override Response handle_ListRoomMembers(string roomName, out IEnumerable<string> memberList)
        {
            return m_roomManager.GetRoomMemberList(roomName, out memberList);
        }

        protected override async Task<Response> handle_MessageRoomAsync(string roomName, string fromUsername, string messageText)
        {
            // check if the sending user is a member of the room
            IEnumerable<string> memberList;
            Response res = m_roomManager.GetRoomMemberList(roomName, out memberList);
            if (!res.Success)
                return res;
            if (!memberList.Contains(fromUsername))
                return new Response(false, String.Format("User '{0}' is not a member of room '{1}'.", fromUsername, roomName));

            // send message to all room members
            List<Task> tasks = new List<Task>();
            foreach (var member in memberList)
            {
                AmTcpClient client;
                if (m_clients.TryGetValue(member, out client))
                {
                    // send message to invididual room member
                    Task t = sendEvent_MessageRoom(client.EventSerializer, roomName, fromUsername, messageText);  // don't need to catch result because the clientreadloop will handle disconnects
                    tasks.Add(t);
                }
                else
                    Logging.DebugWriteLine(String.Format("(MessageRoom '{0}'): couldn't find user '{1}'.", roomName, fromUsername));
            }
            // wait for all async tasks to finish
            await Task.WhenAll(tasks.ToArray());
            return new Response(true, "");
        }

        protected override async Task<Response> handle_MessagePersonalAsync(string toUsername, string fromUsername, string messageText)
        {
            AmTcpClient other;

            if (!FindClient(toUsername, out other))
                return new Response(false, String.Format("Username '{0}' not found.", toUsername));

            if (!await sendEvent_MessagePersonal(other.EventSerializer, fromUsername, messageText))
                return new Response(false, String.Format("User '{0}' has disconnected.", toUsername));

            return new Response(true, "");
        }
    }
}
