// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Collections.Generic;
using System.Linq;

using ChatCommon;


namespace ChatServer
{
    /// <summary>
    /// This class keeps track of the state of rooms and room membership for the server.
    /// </summary>
    public class RoomManager
    {
        // A two-way lookup for room membership
        private Dictionary<string, ICollection<string>> m_room_members = new Dictionary<string, ICollection<string>>();  // key: room_name  val: user_name
        private Dictionary<string, ICollection<string>> m_member_rooms = new Dictionary<string, ICollection<string>>();  // key: user_name  val: room_name
        private object m_roomLock = new object();

        
        private string badRoomNameString(string badRoomName)
        {
            return String.Format("No room with the name '{0}' exists.", badRoomName);
        }


        public Response AddRoom(string roomName)
        {
            lock (m_roomLock)
            {
                if (m_room_members.ContainsKey(roomName))
                    return new Response(false, String.Format("A room with the name '{0}' already exists.", roomName));

                m_room_members.Add(roomName, new List<string>());
                return new Response(true, "");
            }
        }

        public Response DeleteRoom(string roomName, out IEnumerable<string> outMembers)
        {
            lock (m_roomLock)
            {
                // get the list of members of the room
                ICollection<string> members;
                if (!m_room_members.TryGetValue(roomName, out members))
                {
                    outMembers = null;
                    return new Response(false, badRoomNameString(roomName));
                }
                // first remove the room from each individual user's list
                outMembers = members;
                foreach (var member in members)
                {
                    ICollection<string> usersRoomList;
                    if (!m_member_rooms.TryGetValue(member, out usersRoomList) || !usersRoomList.Remove(roomName))
                        Logging.DebugWriteLine("RoomManager inconsistent state. (DeleteRoom).");
                }
                // now remove the room
                m_room_members.Remove(roomName);
                return new Response(true, "");
            }
        }

        public IEnumerable<string> GetRoomList()
        {
            lock (m_roomLock)
                return m_room_members.Keys.OrderBy(name => name);
        }

        
        public Response SubscribeRoom(string roomName, string username)
        {
            lock (m_roomLock)
            {
                // add user to room's list
                ICollection<string> members;
                if (!m_room_members.TryGetValue(roomName, out members))
                    return new Response(false, badRoomNameString(roomName));
                if (members.Contains(username))
                    return new Response(false, String.Format("User '{0}' is already a member of room '{1}'.", username, roomName));
                members.Add(username);

                // add room to user's list
                ICollection<string> rooms;
                if (!m_member_rooms.TryGetValue(username, out rooms))
                    m_member_rooms.Add(username, new List<string>() { roomName });
                else if (rooms.Contains(roomName))
                    return new Response(false, String.Format("User '{0}' is already a member of room '{1}'.", username, roomName));
                else
                    rooms.Add(roomName);
                return new Response(true, "");
            }
        }

        public Response UnsubscribeRoom(string roomName, string username)
        {
            lock (m_roomLock)
            {
                // room must exist
                ICollection<string> members;
                if (!m_room_members.TryGetValue(roomName, out members))
                    return new Response(false, badRoomNameString(roomName));

                // remove user from room's list
                bool wasRoomMember = members.Remove(username);

                // remove room from user's list
                ICollection<string> rooms;
                bool wasMemberRoom = m_member_rooms.TryGetValue(username, out rooms) && rooms.Remove(roomName);

                if (!wasRoomMember && !wasMemberRoom)
                    return new Response(false, String.Format("User '{0}' is not a member of room '{1}'.", username, roomName));

                if (wasRoomMember ^ wasMemberRoom)
                    Logging.DebugWriteLine("RoomManager inconsistent state. (UnsubscribeRoom).");

                return new Response(true, "");
            }
        }

        // remove a user from every room (e.g. before disconnecting)
        public void UnsubscribeAllRooms(string username)
        {
            lock (m_roomLock)
            {
                // get the user's room list
                ICollection<string> rooms;
                if (m_member_rooms.TryGetValue(username, out rooms))
                {
                    rooms = rooms.ToArray();  // make a copy
                    // call UnsubscribeRoom for each room
                    foreach (var room in rooms)
                        UnsubscribeRoom(room, username);  // Don't need to save individual operation responses. Why? Because the end result is the same--the user is in no rooms.

                    // rooms collection should now be empty
                    if (m_member_rooms[username].Count() > 0)
                        Logging.DebugWriteLine("RoomManager inconsistent state. This should seriously never happen. (UnsubscribeAllRooms).");

                    m_member_rooms.Remove(username);
                }
            }
        }

        public Response GetRoomMemberList(string roomName, out IEnumerable<string> memberList)
        {
            lock (m_roomLock)
            {
                ICollection<string> members;
                if (m_room_members.TryGetValue(roomName, out members))
                {
                    memberList = members.OrderBy(name => name);
                    return new Response(true, "");
                }
                else
                {
                    memberList = null;
                    return new Response(false, badRoomNameString(roomName));
                }
            }
        }

        public bool IsMember(string roomName, string username)
        {
            lock (m_roomLock)
                return m_member_rooms.ContainsKey(username) && m_member_rooms[username].Contains(roomName);
        }

    }
}
