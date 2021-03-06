Remote Actors for .NET
======================

**Remact.Net** facilitates the development of remote actors in .NET languages. 

It is a class library written in C-Sharp. 
It runs in Microsoft .NET and Linux-Mono environments. 

The [Actor model](http://en.wikipedia.org/wiki/Actor_model) is inspired by physics and by nature. 
It brings order to multi-thread, multi-core, multi-host systems. 
These days many systems have such requirements.
The [Reactive Manifesto](http://www.reactivemanifesto.org/) explains it in detail. 

Industrial control systems with dynamically connected and movable intelligent subsystems have been in focus of Remact.Net's design. 
Therefore, this library supports many instances of the same application running on one or on distributed hosts. 
It also supports dynamic discovery of actors and does not require configuration of host names and TCP ports. 

In spite of all these features, Remact.Net is remarkably slim and easy to adapt for special needs. 


**Project status** 

The next development steps are:
* Threadsafe Threadpool Actors using SemaphoreSlim.WaitAsync
* Integrate [AMQP 1.0](https://github.com/Azure/amqpnetlite) transport
* Xamarin.Android test application
* Additional integration tests for bidirectional communication
* Remove reference to Remact.Net.Bms1Serializer from test contracts
* Do not use Type.AssemblyQualifiedTypeName for dispatching in WAMP protocol
* Cleanup TODO's



### Feature list

The following goals have been reached:

* Small and clean API allows to dynamically create lightweight actors
* Local actors (message passing between threads)
* Remote actors (message passing between hosts or processes)
* WebSockets, Json and other open standards are used to link Remact actors
* Supported protocols: WAMP, JSON-RPC and BMS1.
* Supported serialization: Json (text), MsgPack (binary), BMS1 (binary).
* Peer to peer communication using distributed actor catalogs avoid single point of failure
* Fully support bidirectional models: client-server / server-client / publish-subscribe
* Strongly typed interfaces
* High throughput on Linux and Windows: More than 5000 request/response pairs per second between processes.
  Remact.Net is much faster on Linux and much more interoperable than its predecessor [AsyncWcfLib](http://sourceforge.net/projects/asyncwcflib/).


### Related work

Some programming languages have built-in support for remote actors:

* [Scala](http://www.scala-lang.org/).RemoteActors
* [Erlang](http://www.erlang.org/)

For other languages there are libraries to help programming remote actors:

* [Java and Scala: The Akka library](http://akka.io/)
* [C#: The Stact library](https://github.com/phatboyg/Stact)

Serialization is still an evolving theme. I'm looking for a simple solution for interoperable, binary
serialization that supports inheritance and extensibility (lax versioning).
Interesting developments are:

* [WampSharp](https://github.com/darkl/WampSharp), I have lent some ideas from this WAMP implementation
* [Protobuf-Net](https://code.google.com/p/protobuf-net/) and [Google protocol buffers](https://developers.google.com/protocol-buffers)
* [A good overview](http://spin.atomicobject.com/2011/11/23/binary-serialization-tour-guide/)


Dependencies and standards
--------------------------
Remact.Net is built on open standards and uses open source components.
I would like to thank all who built these components for their contribution to the open source community.

* [AMQP 1.0](http://www.amqp.org/), the ISO/IEC 19464 or [Oasis Standard](http://docs.oasis-open.org/amqp/core/v1.0/amqp-core-messaging-v1.0.html).
  I use the [AmqNetLite](https://github.com/Azure/amqpnetlite) implementation

* [WebSocket](http://tools.ietf.org/html/rfc6455), the IETF standard RFC6455

* [Alchemy-Websockets](https://github.com/Olivine-Labs/Alchemy-Websockets), a class library from Olivine-Labs.
  I had to considerably improve this library. Therefore, I created another [fork](https://github.com/steforster/Alchemy-Websockets.git) of this project.

* [WAMP](http://wamp.ws/), the WebSocket Application Messaging Protocol

* [JSON-RPC](http://www.jsonrpc.org/specification), instead of WAMP, Json-RPC can be used on top of the WebSocket protocol.

* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json), a class library for Json serialization

* [MessagePack](http://msgpack.org) for [CLI](https://github.com/msgpack/msgpack-cli), for [Json.Net](https://github.com/Code-Sharp/Newtonsoft.Msgpack)

* [Log4Net](http://logging.apache.org/log4net/), logging component from Apache

* [NUnit](http://www.nunit.org/), unit testing infrastructure

* [Nito.Async.ActionThread](http://nitoasync.codeplex.com/) is used for testing console applications




Documentation
-------------

###High level design###


On a high level a Remact system is split in three parts:

* **Contracts**: Shared interfaces and data structures are delivered in their own assembly.
  The data structures may have a dependency to Newtonsoft.Json because of serialization attributes.
  The interfaces currently do have a dependency to Remact.Net.RemactMessage.

* **Actors**: Actors use contracts to communicate. Actors do not have a direct dependency to other actors.
  An actor is programmed without having to know its location in the system.

* **System wiring**: This third part of the system places the actor into its location during runtime.
  It defines the process and the host to run on. It also connects client and service ports of the actors.
  The wiring may use dynamic features like actor location at runtime, m:n relationship and backup actors.



###Conceptual parts###


**Actors and ports**

Think of an actor as a room with several ports. 
Only one person - the worker - is walking (running) around in the room and handles all stored goods.
The worker also sends packets out through the ports or gets packets sent in through the ports.

In software, the actor is a group of objects that are accessed by one worker thread only.
Data inside an actor is as consistent as the incoming events allow it to be.
No other threads may access the objects of an actor, therefore the actor programmer needs not to care about critical sections (locks). 

The actor has ports. These are linked to other actors on the same or on a remote process. 
All incoming events are received as messages passed through one of the ports.
The actor has one message queue that queues all incoming messages (requests, responses, notifications or errors). 


**Threadpool Actors**

One of the next versions of Remact.Net will support thread-safety and asynchrony with 'Threadpool Actors'. 

Until now, thread-safety is guaranteed because only one thread is working for one actor (the same thread can work for many actors). 
This concept does not scale up to several 100 actors, because it is difficult to assign the threads to the many actors.
Also, by default, ConsoleApplications and WindowsServices use the threadpool for non-UI tasks.
But without a SynchronizationContext, many threads can access the actor. Therefore, the programmer has to be aware of race- and deadlock conditions.
 
The novel concept will ensure, that only **one threadpool thread at a time** is working for one actor. 
Then again, thread safety is guaranteed by Remact.Net. Thanks to the asynchronous design, throughput and reactivity is not compromised.
Atomic operations stretch from one SendReceiveAsync to the next SendReceiveAsync statement. This is sufficient because actors do not expose
interfaces except those accepting messages from other actors.


**Client ports**

A client port is built from a RemactPortProxy that is connected to a RemactPortService of another actor. 
Messages are sent by the actor to the RemactPortProxy. Sending is a non blocking operation. 
Optionally the remote actor may send a reply message. It is handled as an asynchronous callback event 
in a lambda expression or in a task continuation or in the default message handler of the origin actor. 
Contrary to the normal client/server pattern, the service port may send notification and request messages to a client.
These messages are handled in a method defined by the contract interface or in the default message handler method. 
Disconnecting the client is signaled to the service. A disconnect signal may also be issued by the service. 


**Service ports**

Many remote client ports may be connected to one service port. 
Once connected, messages may be sent from the client to the service but also from the service to the client. 
Address and version information for each connected client is available for the service. 
Additional session data may be kept for each connection on service and/or client side. 
Periodic messages should be exchanged by the actors to check the communication channels. 


**RemactMessage and Payload**

The RemactMessage class addresses source and destination ports. It is used to route the payload data through the system. 
The payload may be of any serializable object type. 
By default serialization is done by Newtonsoft.Json. Therefore, attributes like [JsonProperty] and [JsonIgnore] may be used to
control the serialization process. 
Other serializers like msgpack-cli or the very slim binary message stream serializer (BMS) are pluggable.
The folders **Remact.Net.Plugin** contains the currently implemented combinations of protocols and serializers.
The configured plugin is dynamically loaded at runtime. It will then load the third party assemblies it depends on.


**Methods**

Messages are sent to the method that handles the message payload type as a single input parameter. 
The method also defines the reply payload as the return type. 
A void method will normally not reply a message. 
But in case of error or exception, also void methods will reply an ErrorMessage. 


**Contract interface**

A contract interface defines the set of methods and the corresponding request and response payload types that are available
on a certain client or service port. 
On the receiving side the methods will have the specified, single message payload parameter and additional parameters
for message source identification and session data. 

The folder **src/Remact.Net/Contracts** and **test/SpeedTestApp/src/Contracts** contains interfaces for remotely callable methods and their
corresponding request- and response messages. These definitions and their XML comments form the basic interface definition of actors.

These contracts must be present on both sides of the communication channel.
For actors not written in a .NET programming language (e.g. Java Script), the interface contract must be translated.


**Remact.Catalog application**

Remact.Catalog is an application containing the catalog actor. The catalog is informed about all Remact service ports in a network. 
The catalog knows all coordinates like service name, version, host and TCP port number. 
The catalog actor synchronizes itself with partner catalog actors on other hosts. 

Normally each host runs a catalog actor. To add a new host into a Remact network, just reference a running catalog host.
Then, all actors including those on the new host are informed about the status of all Remact service ports. 
The catalog actor is for actors what a DNS server is for hosts.


**Anonymous client applications**

Applications that have only client functionality can run in a browser (using Java Script) or as a .NET application.
These applications are not registered in the catalog but they can use a catalog actor to get coordinates of
open service ports.



###Communication models


**Client / Service**

One actor may contain several clients and services. 
The output (client) sends a request to the input (service) of another actor and will get the reply from it. 


**Notifications**

The service port may send a callback notification to the client port. 
Also, the client port may send a notification to the service port. 
Notifications are defined as parameter of a void method of the receiving port contract interface. 
There is no reply message to notifications. 


**Service / Client**

In Remact, communication is symmetrical. Therefore services may also send requests to a method of the connected client port
and get a response from it. The difference between service and client lies in the 1 : many relationship 
and in the active part the client is playing during connection buildup. 


**Publish / Subscribe**

A service port can send messages to all its connected client ports. 
Connecting to such a service in fact means - subscribing to its publications. 
The publisher knows who received its publications because all communication is done through reliable 
TCP connections to known partners. 


**Communication stack**

The communication stack depends on the loaded Remact.Net.Plugin. For example, Plugin.Json.Msgpak.Alchemy uses the following layers when receiving a message from a remote actor:
* The .NET TCP layer raises an event on a threadpool thread
* The Alchemy.WebSocketClient or -Service gets the data frame and dispatches it to the protocol layer
* The Wamp- or Json.RPC protocol layer deserializes the payload to a [Newtonsoft.Json.Linq.JToken](http://weblog.west-wind.com/posts/2012/Aug/30/Using-JSONNET-for-dynamic-JSON-parsing)
* The RemactClient or -Service switches to the correct actor thread, builds a RemactMessage and and handles Remact internal messages
* User messages are handled by the RemactDispatcher. It finds the addressed method name in the list of supported contract interfaces,
  it converts the payload to the .NET type defined as first parameter of the addressed method and
  invokes the addressed method by passing the strong typed payload, the RemactMessage and the session data as parameters
* The called method may accept any serializable payload type, a Newtonsoft.Json.Linq.JToken or a [dynamic object](http://msdn.microsoft.com/en-us/library/dd264736%28v=vs.110%29.aspx)
* When the addressed method could not be found or the received stream could not be converted to the correct .NET type,
  a default message handler is called
* The protocol layer serializes the return type of the called method and sends it as a response



###Assemblies

* Remact.Net.dll: The remote actors library
* Remact.Net.CatalogApp.exe: A remact catalog application for desktop systems
* Remact.Net.DesktopAppHelper.dll: A helper library for desktop systems
* Remact.Net.Plugin.Json.Msgpack.Alchemy.dll: The plugin to exchange messages using Newtonsoft.Json.dll, Alchemy.dll and MsgPack.dll
* Remact.Net.Plugin.Bms.Tcp.dll: The plugin to exchange messages using the Remact.Net.Bms1Serializer.dll and Remact.Net.TcpStream.dll
* Newtonsoft.Json.Replacement.dll: A helper assembly in case you have Json-attributes in your code but no Newtonsoft.Json.dll



How to build and test Remact.Net
--------------------------------

Solutions are provided for Visual Studio 2015 and MonoDevelop.  
The solutions contain all plugins and all dependencies to third party library source code. 
To build the plugins, you must clone dependent repositories. All repos must reside in one parent folder and have the original name.
Use the following command lines for cloning: 

      $ git clone https://github.com/steforster/Remact.Net.git 

To build **Remact.Net.Plugin.Json.Msgpack.Alchemy.dll** you need:

      $ git clone https://github.com/steforster/Alchemy-Websockets.git 
      $ git clone https://github.com/JamesNK/Newtonsoft.Json.git 
      $ git clone https://github.com/Code-Sharp/Newtonsoft.Msgpack.git 
      $ git clone https://github.com/msgpack/msgpack-cli.git 

To build **Remact.Net.Plugin.Bms.Tcp.dll** you need:

        $ git clone https://github.com/steforster/Remact.Net.Bms1Serializer.git

To build **Remact.Net.Plugin.Amqp.Tcp.dll** you need:

        $ git clone https://github.com/Azure/amqpnetlite.git
        Note: Currently (Mai 2016) you need to upgrade to VS2015 or modify some source files to make it compileable under MonoDevelop.

In case you have not cloned some repos, you can unload not buildable projects from the solution.

To manually test under Windows you may start "test/SpeedTestApp/Net/_startTest2.cmd", 
in Linux you start "test/SpeedTestApp/Net/_startTest2.sh".
These scrips specify the plugin to use. Modify the arguments for your purpose.
Manual tests write log files to the 'logs' folder.

NUnitTests.ActorDemoTest is an introduction to Remact.Net. 
NUnitTests run in Visual Studio and MonoDevelop. There is a set of tests for each serializer plugin.



License
-------
Remact.Net is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php). 
Copyright (c) 2014-2016, Stefan Forster.



