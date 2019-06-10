# Table of Contents
```
1. Chat Protocol
        1.1. Initial Connection
        1.2. Network Stack (Chat Protocol)
        1.3. Request and Reply Messages
                1.3.1. Connect
                1.3.2. Disconnect
                1.3.3. Create Room
                1.3.4. Delete Room
                1.3.5. Subscribe to Room
                1.3.6. Unsubscribe from Room
                1.3.7. List Room Members
                1.3.8. Send Message to Room
                1.3.9. Send Private Message
        1.4. Event Messages
                1.4.1. Receive Message from Room
                1.4.2. Receive Private Message
                1.4.3. Server Disconnected
                1.4.4. Room Deleted
2. Asymmetric Multiplexing over TCP (AM/TCP)
        2.1. Headers
                2.1.1. Requests
                2.1.2. Replies / Events
3. Serialization
        3.1. Integers
        3.2. Strings
4. Secure Socket Layer (SSL)
5. File Transfer
        5.1. Network Stack (File Transfer)
        5.2. Requests / Replies
                5.1.1. Upload
                5.1.2. Download
6. Retrospective
```



# 1) Chat Protocol

## 1.1) Initial Connection 

Clients connect to the server via TCP and immediately establish an SSL session with TLS 1.2 (see the section on SSL). 

The very next thing the client must do is send one byte to indicate if the connection is for Chat Protocol or File Transfer. To indicate Chat Protocol, the client shall send a 0. To indicate File Transfer, the client shall send a 1. No other values are allowed.

After sending a 0, the client and server enter Chat Protocol. The first thing a client shall do is send a Connect Request. After successfully connecting, the client is free to send any other Requests.

For Chat Protocol, the TCP connection is persistent.

## 1.2) Network Stack (Chat Protocol)

```
+--------------------+                               +--------------------+
| Client Application |                               | Server Application |
+--------------------+                               +--------------------+
          |                                                    |
          | function calls                                     | function calls
          |                                                    |
+--------------------+                               +--------------------+
|   Chat Protocol    |  <-Requests/Replies/Events->  |   Chat Protocol    |
+--------------------+                               +--------------------+
          |                                                    |
          | strings                                            | strings
          |                                                    |
+--------------------+                               +--------------------+
|   Serialization    |  <---------objects--------->  |   Serialization    |
+--------------------+                               +--------------------+
          |                                                    |
          | bytes                                              | bytes
          |                                                    |
+--------------------+                               +--------------------+
|   AM/TCP Demuxer   |   <--socket multiplexing--->  |    AM/TCP Muxer    |
+--------------------+                               +--------------------+
          |                                                    |
          | (header bytes + bytes)                             | (header bytes + bytes)
          |                                                    |
+--------------------+                               +--------------------+
|        SSL         |  <-----encrypted bytes----->  |        SSL         |
+--------------------+                               +--------------------+
          |                                                    |
+--------------------+                               +--------------------+
|        TCP         |   <----------bytes--------->  |        TCP         |
+--------------------+                               +--------------------+
          |                                                    |
         ...                                                  ...
         
Fig 1.1: Chat Protocol Network Stack
```

## 1.3) Request and Reply Messages

Requests are sent from the client to the server's Request Line (see AM/TCP). A Request consists of a single string that contains a command plus zero or more arguments, all delimited by colons. 

For example, `list_room_members:lobby` requests a list of users in the "lobby" room. The first part, `list_room_members`, is the command. The command is case-insensitive. The second part is a room name. Room names and usernames are case-sensitive. Additionally, they shall not contain a colon. 

**A Request is always answered with a Reply.** Replies are sent by the server to the client's Reply Line (see AM/TCP section). **A Reply consists of an `ack` or `nack` followed by zero or more messages, all delimited by colons.** Unless otherwise specified, the server shall respond to a given Request with this standard *Ack* or *Nack* Reply. An example Reply is `ack`. Another example Reply is `nack:That room does not exist`. Typically an *Ack* will require no message, but a *Nack* should include some kind of error explanation, such as "That username is already taken". 

**Figure 1.2** shows a time-sequence diagram of a client Request and the server's Reply.

```
+--------+          +--------+
| Client |          | Server |
+--------+          +--------+
     |     Request      |
     |----------------->|
     |                  |
     |      Reply       |
     |<-----------------|
     |                  |
     |                  |
    ...                ...
    
Fig 1.2: Request/Reply time-sequence diagram
```

### 1.3.1) Connect
`connect:[username]`
* `[username]` - The desired username.
*  example: `connect:Calvin`

Register your username with the server. The username is for this session only and does not persist after disconnect.

This Request should be sent exactly once, immediately after entering Chat Protocol.

### 1.3.2) Disconnect
`disconnect`
* no arguments

This command is used to gracefully disconnect from the server. Server-side, the user is removed from all rooms and the client's username is freed.

Special Notes
* The server may reply with a `Nack`, but the client may ignore this.

### 1.3.3) Create Room
`create_room:[roomname]`
* `[roomname]` - The name of the new room.
* example: `create_room:lobby`

Create a room on the server.

### 1.3.3) Delete Room
`delete_room:[roomname]`
* `[roomname]` - The name of the room
* example: `delete_room:lobby`

Delete a room on the server.

### 1.3.4) List Rooms
`list_rooms`
* no arguments

Request a list of the rooms.

Special Reply
* If the operation is successful, the server shall reply with `ack` followed by zero or more room names, delimited by colons. Example: `ack:lobby:roomA:roomB`

### 1.3.5) Subscribe to Room
`subscribe_room:[roomname]`
* `[roomname]` - The name of the room.

Once subscribed to a room, the server will begin forwarding Events related to this room to the client. Room Events are room message broadcasts and room-deleted Events.

### 1.3.6) Unsubscribe from Room
`unsubscribe_room:[roomname]`
* `[roomname]` - The name of the room.

The server will stop forwarding Events for this room to the client.

### 1.3.7) List Room Members
`list_room_members:[roomname]`
* `[roomname]` - The name of the room.

Request a list of the members of a given room.

Special Reply
* The server shall reply with `ack` or `nack`, followed by zero or more usernames, delimited by colons. Example: `ack:Bob:Alice:Tom`

### 1.3.8) Send Message to Room
`send_message_room:[roomname]:[messagetext]`

* `[roomname]` - The name of the room. The client must be subscribed to this room.
* `[messagetext]` - The text body to send. It may contain colons.
* example: `send_message_room:lobby:Hello, World!`

Broadcast a message to the given room.

### 1.3.9) Send Private Message
`send_message_personal:[to_username]:[messagetext]`
* `[to_username]` - The username of the recipient.
* `[messagetext]` - The text body to send. It may contain colons.
* example: `send_message_personal:Bobby2:Hello, Bobby2!`

Send a private message to the given user.

## 1.4) Event Messages
Events are messages triggered randomly by the activity of other users on the server. These are sent to the client's Event Line (see AM/TCP section).

Events are sent from the server to the client. The client does not acknowledge the event back to the server.

```
+--------+          +--------+
| Client |          | Server |
+--------+          +--------+
     |                  |
     |      Event       |
     |<-----------------|
     |                  |
     |                  |
    ...                ...
    
Fig 1.3: Event time-sequence diagram
```

Events typically occur due to some other user on the server. For example, a user broadcasting a message to a room. This is not always true--for example, the server sends an event when it is shutting down. 

**Figure 1.4** contains an example time-sequence for a room broadcast. In this diagram, assume *Client A* and *Client B* are subscribed to the *lobby* room. *Client A* sends the message "hi!" to the *lobby* room. The server receives this Request and sends Events to the subscribers of *lobby*. *Client B* receives the Event. *Client A* also receives the/ event, because they are subscribed to *lobby* as well. When the server is done, it sends an *Ack* Reply to *Client A* to indicate success. 

If the operation had failed (for example, if the room did not exist), the server would send a *Nack* reply along with an error message to *Client A*.

```
+----------+               +--------+               +----------+ 
| Client A |               | Server |               | Client B | 
+----------+               +--------+               +----------+ 
     |   message,lobby,"hi!"   |                         |
     |--------[Request]------->|                         |
     |                         |                         |
     |   lobby,Client A,"hi!"  |                         |
     |<--------[Event]---------|   lobby,Client A,"hi!"  |
     |                         |---------[Event]-------->|
     |           Ack           |                         |
     |<--------[Reply]---------|                         |
     |                         |                         |
     |                         |                         |
    ...                       ...                       ...
    
Fig 1.4: Room broadcast time-sequence diagram
```

### 1.4.1) Receive Message from Room
`event_message_room:[roomname]:[from_username]:[messagetext]`

* `[roomname]` - The name of the room. The client must be subscribed to this room.
* `[from_username]` - The username of the message's author.
* `[messagetext]` - The text body of the message. It may contain colons.
* example: `event_message_room:lobby:Calvin:Hello, World!`

An incoming room broadcast.

### 1.4.2) Receive Private Message
`event_message_personal:[from_username]:[messagetext]`
* `[from_username]` - The username of the sender.
* `[messagetext]` - The text body of the message. It may contain colons.
* example: `event_message_room:lobby:Calvin:Hello, Bobby2!`

An incoming private message.

### 1.4.3) Server Disconnected
`event_disconnect`
* no arguments

The server has disconnected from the client.

### 1.4.4) Room Deleted
`event_room_deleted:[roomname]`
* `[roomname]` - The room that was deleted. The client must be subscribed to the room.

A room that the client was subscribed to has been deleted.



# 2) Asymmetric Multiplexing over TCP (AM/TCP)

Some Requests cannot be completed by the server. E.g. the user cannot join a room that doesn't exist. So the server must respond to all Requests with an `ack` or `nack:message` Reply to indicate the success or failure of a Request. At the same time, there are random Events, such as room broadcasts, that can come in at any time. (Recall that Events are sent from the server to the client at any time. The client does not reply to them.) The client must be able to handle such incoming Event occurring while the client is expecting an *Ack* or *Nack*.

There are a few ways the client can deal with this. It could keep a queue of unacknowledged actions so events and acknowledgements can be interleaved. However, this requires splitting the client Requests into top-halves and bottom-halves. The top-half initiates the Request. The bottom-half runs when the corresponding Reply is received, completing the client-side state transitions if the Reply was an *Ack* or canceling them if the Reply was a *Nack*. This introduces a lot of complexity.

Another way the client can handle stochastic Events is to have two socket connections to the server, one for Request/Reply communication and one for receiving Events. This necessitates some added complexity in the connection code for the server, which must now check consistency. The server must ensure that the client connects on both sockets, and must verify that it is the same client for security purposes. This two-socket method also cuts the number of clients a server can accommodate in half, leaving a maximum of 32,768 clients.

Asymmetric Multiplexing over TCP (AM/TCP) allows the server to use one socket for both synchronous Request/Reply and stochastic Event communication by adding header information to indicate the type of communication.

AM/TCP is "asymmetric" because the multiplexing side has two virtual lines for writing and one for reading, while the demultiplexing side has two virtual lines for reading and one for writing.
```
+------------+                        +------------+
|   Client   |  ---[Request Line]-->  |   Server   |
|   (demux)  |  <--[ Reply Line ]---  |    (mux)   |
|            |  <--[ Event Line ]---  |            |
+------------+                        +------------+

Fig 2.1: Application Layer
```

```
+----------------------------------+     +----------------------------------+
|           AM/TCP Demuxer         |     |           AM/TCP Muxer           | 
|  [Request bytes (no header)]---->| --> |---->[Request bytes (no header)]  |
|                                  |     |                                  |
|  [Reply header + msg bytes]<---+-| <-- |<-+---[Reply header + msg bytes]  |
|  [Event header + msg bytes]<--/  |     |   \--[Event header + msg bytes]  |
|                                  |     |                                  | 
+----------------------------------+     +----------------------------------+

Fig 2.2: AM/TCP Layer
```

The client now receives more information about the purpose of a given message. The client can implement a software AM/TCP demultiplexing layer, or use the top-half/bottom-half method used before, or otherwise implement this functionality any way they desire.

The AM/TCP Layer sits above SSL. On the multiplexing side, headers are added to Reply and Event messages after serialization but before encryption. On the demultiplexing side, headers are stripped after encryption but before deserialization.

**Figure 2.3** is an expansion of **Figure 1.4** from the AM/TCP layer's view. In **Figure 2.3,** you can see how a server really sends a message Event. *Client A* sends a request to the server to send the message "hi!" to the room named *lobby*. *Client A* and *Client B* are subscribed to *lobby*. The server sends the room broadcast Event to each member's respective Event line. When the operation is done, the server sends an *Ack* Reply to *Client A* on its Reply line.

Since the Event is sent to *Client A's* Event line, it does not interfere with *Client A's* Reply line, which is expecting an *Ack* or *Nack* Reply. Note that *Client B's* Reply line is not shown here.

```
+------------+  +------------+          +--------------+            +------------+  
|  Client A  |  |  Client A  |          |    Server    |            |  Client B  |  
|(Event line)|  |(Reply line)|          |(Request line)|            |(Event line)|  
+------------+  +------------+          +--------------+            +------------+  
      |               |                         |                         |        
      |               |   message,lobby,"hi!"   |                         |        
      |               |--------[Request]------->|                         |        
      |               |                         |                         |        
      |                   lobby,Client A,"hi!"  |                         |        
      |<------------------------[Event]---------|   lobby,Client A,"hi!"  |        
      |                                         |---------[Event]-------->|        
      |               |          Ack            |                         |        
      |               |<--------[Reply]---------|                         |        
      |               |                         |                         |        
      |               |                         |                         |        
     ...             ...                       ...                       ...       
     
Fig 2.3: AM/TCP layer's view of room broadcast Event
```

## 2.1) Headers

### 2.1.1) Requests
Requests have no header. Request message bytes pass through the AM/TCP layer unchanged.

### 2.1.2) Replies / Events
Reply and Event messages require a header. The header is 12 bytes. It has 3 items.

```
byte 0            4            8            12
     +------------+------------+------------+
     |   version  |     tag    |   length   |
     +------------+------------+------------+
     
Fig 2.4: AM/TCP Reply/Event Header
```

**Version** is a 32-bit integer. It shall be set to 1.

**Tag** is a 32-bit integer. It indicates which line the message is for. A value of 0 indicates a Request. A value of 1 indicates an Event. No value other is allowed.

**Length** is a 32-bit integer. It indicates the number of bytes following the header that are part of the message. Implementations are free to put a limit on the maximum length.

A single Reply or Event message may be broken up into several parts by the multiplexer, with each part requiring a header. All that is required is that the message payload bytes be reassembled by the demultiplexer in the correct order for consumption by the layers above.

# 3) Serialization

Chat Protocol only has requirements for serializing 32-bit integers and strings.

## 3.1) Integers

32-bit integers shall be sent most-significant byte first (big-endian). The bit ordering within a byte is not specified.

## 3.2) Strings

Strings must be in ASCII format.

Before a string is sent, its length must be sent first. The length of the string is the number of bytes that will be sent. The length shall be encoded as a 32-bit integer and serialized using the corresponding rules. The server is free to set a limit on the length of a string.

After sending the length, the string is sent byte for byte. Do not include a null-terminator to indicate the end of a string.

# 4) Secure Socket Layer (SSL)

The Secure Socket Layer (SSL) is just above the TCP connection in the network stack.

Immediately after establishing a TCP connection, the client and server shall seek to establish a secure session. The protocol used is TLS 1.2. The server shall authenticate with an X.509 certificate. The client is not required to authenticate via certificate.

# 5) File Transfer

File Transfer (FT) is "in beta" and is subject to change. FT is optional, and servers are not required to implement the FT Requests.

FT currently only supports uploading and downloading files in one large chunk. Because of this, a scheme for connecting via a second socket (non-persistent connection) is specified. 

There is currently no failure checking. There is no mechanism for authorizing file access.

As in Chat Protocol, clients connect to the server via TCP and immediately establish an SSL session with TLS 1.2 (see the section on SSL). 

The very next thing the client must do is send one byte to indicate if the connection is for Chat Protocol or File Transfer. To indicate Chat Protocol, the client shall send a 0. To indicate File Transfer, the client shall send a 1. No other values are allowed.

After sending a 1, the client and server enter File Transfer.

For File Transfer, the TCP connection is non-persistent. The connection is closed after a Request is finished.

## 5.1) Network Stack (File Transfer)

```
+--------------------+                               +--------------------+
| Client Application |                               | Server Application |
+--------------------+                               +--------------------+
          |                                                    |
          | function calls                                     | function calls
          |                                                    |
+--------------------+                               +--------------------+
|    File Transfer   |  <-Requests/Replies/bytes-->  |    File Transfer   |
+--------------------+                               +--------------------+
          |                                                    |
          | strings/bytes                                      | strings/bytes
          |                                                    |
+--------------------+                               +--------------------+
|  (Serialization*)  |  <---------objects--------->  |  (Serialization*)  |
+--------------------+                               +--------------------+
          |                                                    |
          | bytes                                              | bytes
          |                                                    |
+--------------------+                               +--------------------+
|        SSL         |  <-----encrypted bytes----->  |        SSL         |
+--------------------+                               +--------------------+
          |                                                    |
+--------------------+                               +--------------------+
|        TCP         |   <----------bytes--------->  |        TCP         |
+--------------------+                               +--------------------+
          |                                                    |
         ...                                                  ...
         
Fig 5.1: File Transfer Network Stack
```

*Note that serialization is not used when transferring file bytes.

## 5.2) Requests / Replies

File Transfer has Requests and Replies similar to Chat Protocol. The client sends upload or download Requests to the server. The server may send multiple *Ack* or *Nack* Replies in response to one Request.

**There is no equivalent to Chat Protocol Events in File Transfer.** All communication is done synchronously. Thus, File Transfer does not use AM/TCP.

### 5.2.1) Upload
1. The client establishes TCP and SSL with the server and sends the byte indicating File Transfer
2. The client sends an Upload Request string
    * `file_up:[filename]`
      * `[filename]` - What the server should name the file about to be uploaded.
3. The server will reply with an *Ack* to indicate it is ready for the client to begin sending. (or *Nack* to indicate error). 
4. The client serializes a 32-bit integer to indicate the number of bytes it is about to send (i.e. the file size).
5. The client streams the bytes to the server.
6. The server replies with an *Ack* to indicate it has received the bytes.
7. Both sides close the connection

```
+--------+            +--------+
| Client |            | Server |
+--------+            +--------+
     |                    |
     | establish TCP/SSL  |
     |------------------->|
     |                    |
     |  file_up:filename  |
     |------------------->|
     |                    |
     |        Ack         |
     |<-------------------|
     |                    |
     |    file's bytes    |
     |------------------->|
     |                    |
     |                    |
     |                    |
     |        Ack         |
     |<-------------------|
     |                    |
     |                    |
     x  both sides close  x
     
Fig 5.2: Upload Request time-sequence diagram
```

### 5.2.2) Download
1. The client establishes TCP and SSL with the server and sends the byte indicating File Transfer
2. The client sends a Download Request
    * `file_down:[filename]`
      * `[filename]` - The name of the file (on the server) to download.
3. The server will reply with an *Ack* to indicate it is has the file. (or *Nack* to indicate a problem). 
4. The server serializes a 32-bit integer to indicate the number of bytes it is about to send (i.e. the file size).
5. The server streams the bytes to the client.
6. The server sends an *Ack* to indicate it sent the file with no issues.
7. Both sides close the connection

```
+--------+            +--------+
| Client |            | Server |
+--------+            +--------+
     |                    |
     | establish TCP/SSL  |
     |------------------->|
     |                    |
     | file_down:filename |
     |------------------->|
     |                    |
     |        Ack         |
     |<-------------------|
     |                    |
     |    file's bytes    |
     |<-------------------|
     |                    |
     |                    |
     |                    |
     |        Ack         |
     |<-------------------|
     |                    |
     |                    |
     x  both sides close  x
     
Fig 5.3: Download Request time-sequence diagram
```

# 6) Retrospective

Chat Protocol sends server messages after a failure. I realized a little too late that it would be way better to send an error codes instead of a string. This would standardize the server responses and allow the chat client to know how to handle the type of error. For example, with the current protocol, if the client tries to Connect with a username that's taken, the Connect fails. Without being able to parse the server message, the client can't know that the operation failed because the username was taken. If the server instead returned an error code, the client could automatically negotiate to find a free username.

I didn't have time to add timeouts or soft state considerations.

I added File Transfer as an afterthought. Chat Protocol wasn't designed with byte streaming in mind. It was designed for sending short strings. So File Transfer is basically a second protocol that you have to choose at the beginning of a connection. That's a little kludgey. Additionally, because only 32-bit signed integers can be serialized, the size of file that can be transfered is limited to to 2^31 = 2GB.

It would be better to use Unicode strings instead of ASCII.



-----
Copyright © 2019 Alexander Freed
Language: Markdown. CommonMark 0.28 compatible.
