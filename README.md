# Another IRC Clone
This is a chat server. It implements a protocol called "Chat Protocol".

This project contains the source code and project files for server and client implementations of Chat Protocol.

# Project

The source code is written in C# 6.0 (.NET Framework 4.6.1). The project files are for Visual Studio 2017. The solution is located in the base folder. The solution is broken into 3 projects, *ChatClientCmdline*, *ChatServer*, and *ChatCommon*.

**Source code** is located in the folders [*ChatClientCmdline*](https://github.com/akfreed/AnotherIrcClone/tree/master/ChatClientCmdline), [*ChatServer*](https://github.com/akfreed/AnotherIrcClone/tree/master/ChatServer), and [*ChatCommon*](https://github.com/akfreed/AnotherIrcClone/tree/master/ChatCommon). (With the bulk of it being in *ChatCommon*.)

After compiling, executables will be located in *ChatClientCmdline/bin/* and *ChatServer/bin/* under the *debug* or *release* folder (depending on which one you build).

You need a certificate to start an SSL connection as the server. A development certificate is included in the [*Certificate*](https://github.com/akfreed/AnotherIrcClone/tree/master/Certificate) directory. This certificate should be placed with the server executable.

# Await / Async

The reason I wrote this in C# was because I wanted to try out this `await` / `async` pattern, which is a feature of the language (not the library). Normal input functions block until data is available. This can be handled by thread-per-client (which is not scalable), polling (which wastes CPU cycles), `pselect`, or (you guessed it!) `async` functions.

These functions are not asynchronous in the traditional sense (which would be polling). The design pattern they implement is called a *promise*. In general, `async` functions are automatically ran on C#'s thread pool. When you call `await` on an `async` function, it waits for the task to complete. For `async` I/O functions, the magic actually goes all the way into the OS interrupt handler. When an `async` I/O function waits for I/O, it isn't blocking. In fact, there isn't even a thread (there is no task to run on the thread pool). When data comes comes in, the interrupt handler runs and yada, yada, yada, the C# runtime schedules the `async` function on a thread pool thread to continue where it left off.

Theoretically this should be highly scalable. The language automatically works with the OS to distribute work to the thread pool, which uses exactly as many resources as the system has without going over. Unfortunately, I didn't have a chance to do tests at scale with Chat Protocol. (I did some tests with File Transfer, and it was terribly slow.) Hopefully in the future I'll have time to experiment with this more.

# Class Descriptions

**Refer to [*RFC.md*](https://github.com/akfreed/AnotherIrcClone/blob/master/RFC.md) for an explanation of Chat Protocol.**

Many of the classes represent different portions of the network stack. This goal helped when designing the application. In the end, the convenience/shorthand classes can make it hard to figure out what stream is actually being written to / read from. 

This is a little bit due to laziness. If a class owns the stream, it is responsible for closing it gracefully, even in the presence of exceptions and race conditions. With these deep classes, it would require implementing `Close` functions all the way down.

By the way, I don't guarantee that the application is free of race conditions. There wasn't enough time to check everything, and being a simple chat application does not demand a lot of safety.

## Lower Level Network Stack

At the lowest level, the client and server communicate using the `SslStream` class that is part of the standard C# library. The TCP connection is started with an instance of the `TcpClient` class (which can be spawned by the `TcpListener` class in the server's case). This `TcpClient` instance can be passed to the `SslStream` constructor.

* `SslEgg` is a convenience class that ties the `SslStream` instance with the `TcpClient` instance that it was created from. `SslEgg` provides an easy API for creating the SSL session.
* `ReadAggregatorWritePassthrough` encapsulates the `SslStream` stream (or any `Stream`). It provides a `ReadAll` function that aggregates a specified number of bytes from the stream, blocking until all are available. It also provides exception handling for the socket. `ReadAggregatorWritePassthrough` implements the `IReadableStream` and `IWriteableStream` interfaces (created in this program).
* `ProdConsBuffer` is another class that implements the `IReadableStream` and `IWriteableStream` interfaces. `ProdConsBuffer` is a custom memory stream. It is essentially a thread-safe byte queue. More on `ProdConsBuffer` when we get to `AmTcpDemuxer`.
* `AmTcpMuxer` is used by the server to provide two virtual output byte streams over one socket. It has a `ReadAggregatorWritePassthrough` instance and exposes it for public reading. The `ReadAggregatorWritePassthrough` instance can be written to through the muxer's methods, which add headers to the given bytes.
* `AmTcpDemuxer` is used by the client. The server has two virtual byte streams over the one socket connection (a `ReadAggregatorWritePassthrough` instance). The demuxer has a dedicate thread to read this instance. It strips the headers and puts the bytes in the appropriate queue--one of two `ProdConsBuffers`. These `ProdConsBuffers` are accessible for public reading and represent the client's *Reply* and *Event* lines. The `ReadAggregatorWritePassthrough` is accessible for public writing.
* `AmTcpClient` is a convenience class used by the server to tie `AmTcpMuxer`s with corresponding `SerializationAdapter`s and `DeserializationAdapter`s.
* `SerializationAdapter` and `DeserializationAdapter` are convenience classes that tie a specific `IReadableStream` or `IWriteableStream` with the `Serializer` class
* `Serializer` is a static class that holds serialization and deserialization functions.

## Upper Level Network Stack
### Server
* `CpServersideBase` is the base class for a server. It handles Chat Protocol functions and specifies callbacks for the derived class to implement. `CpServersideBase` is stateless. `CpServersideBase` has the server's per-client read loop. It parses client Requests and calls the appropriate derived-class callbacks with the given arguments.
* `Server` is derived from `CpServersideBase`. It holds and connected client information in the form of `RoomManage`r and `AmTcpClient` collections. It also handles the `TcpListener` and accepting incoming TCP connections.
* `FtServerside` is between `Server` and `CpServersideBase`. It is an optional class  that holds the implementation for File Transfer functions. `FtServerside` is stateless.

### Client
* `CpClientsideBase` is the base class for a client. It handles Chat Protocol functions and specifies callbacks for the derived class to implement. `CpClientsideBase` runs the client's Event read loop. When an event comes in, it parses the arguments and passes them to the appropriate derived-class callback.
* `CmdClient` is derived from `CpClientsideBase`. It handles connecting to the server and reading and displaying user I/O. 

### File Transfer
* `FtFileManager` is a static class that manages files and file transfers for both server and client.
* `FtClient` is a convenience class that ties an serializers and deserializers together with its `SslStream`. This is necessary for File Transfer because string objects are serialized for communication, but the upper layers also need access to the byte stream for transferring files.

# Client Commands

```
command:     alt:    arguments:                explanation:

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
/exit        /quit                             - log off
```

-----
Copyright © 2019 Alexander Freed
Language: Markdown. CommonMark 0.28 compatible.