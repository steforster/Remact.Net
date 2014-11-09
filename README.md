Remote Actors for .NET
======================

**Remact.Net** facilitates the development of remote actors in .NET languages.  

It is a class library written in C-Sharp.  
It runs in Microsoft .NET and Linux-Mono environments.  

The [Actor model](http://en.wikipedia.org/wiki/Actor_model) is inspired by physics and by nature.  
It brings order to multithread, multicore, multihost systems.  
These days many systems have such requirements.
The [Reactive Manifesto](http://www.reactivemanifesto.org/) explains it in detail.  

Industrial control systems with dynamically connected and movable intelligent subsystems have been in focus of Remact.Net's design.  
Therefore, this library supports many instances of the same application running on one or on distributed hosts.  
It also supports dynamic discovery of actors and does not require configuration of host names and TCP ports.  

In spite of all these features, Remact.Net is remarkably slim and easy to adapt for special needs.  


**Project status**  

Remact.Net is in good shape now. You are invited to try it out.  

The next development steps will be:
* Additional integration tests for bidirectional communication
* Do not use Type.AssemblyQualifiedTypeName for dispatching
* Port unit tests from AsyncWcfLib
* Integrate Newtonsoft logging
* Add missing XML comments
* Cleanup TODO's
* Support Json-RPC



### Feature list

The following goals have been reached:

* Small and clean API allows to dynamically create lightweight actors
* Local actors (message passing between threads)
* Remote actors (message passing between hosts or processes)
* WebSockets, Json and other open standards are used to link Remact actors
* Peer to peer communication using distributed actor catalogs avoid single point of failure
* Fully support bidirectional models: client-server / server-client / publish-subscribe
* Strongly typed interfaces
* High throughput on Linux and Windows: More than 5000 request/respose pairs per second between processes.
  Remact.Net is much faster on Linux and much more interoperable than its predecessor [AsyncWcfLib](http://sourceforge.net/projects/asyncwcflib/). 


### Related work

The [Robot Operating System ROS](http://www.ros.org) is a popular framework that uses the actor model.
There, a new messaging subsystem [DDS](http://design.ros2.org/articles/ros_on_dds.html) is considered. 

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
* [MessagePack](http://msgpack.org) for [CLI](https://github.com/msgpack/msgpack-cli), for [Json.Net](https://github.com/Code-Sharp/Newtonsoft.Msgpack)
* [Protobuf-Net](https://code.google.com/p/protobuf-net/) and [Google protocol buffers](https://developers.google.com/protocol-buffers)
* [A good overview](http://spin.atomicobject.com/2011/11/23/binary-serialization-tour-guide/)


Dependencies and standards
--------------------------
Remact.Net is built on open standards and uses open source components.
I would like to thank all who built these components for their contribution to the open source community.

* [WebSocket](http://tools.ietf.org/html/rfc6455), the IETF standard RFC6455

* [Alchemy-Websockets](https://github.com/Olivine-Labs/Alchemy-Websockets), a class library from Olivine-Labs.
  I had to considerably improve this library. Therefore, I created another [fork](https://github.com/steforster/Alchemy-Websockets.git) of this project.

* [WAMP](http://wamp.ws/), the WebSocket Application Messaging Protocol

* [JSON-RPC](http://www.jsonrpc.org/specification), instad of WAMP, Json-RPC can be used on top of the WebSocket protocol.

* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json), a class library for Json serialization

* [Log4Net](http://logging.apache.org/log4net/), logging component from Apache

* [NUnit](http://www.nunit.org/), unit testing infrastructure

* [Nito.Async.ActionThread](http://nitoasync.codeplex.com/) is used for testing console applications




Documentation
-------------

###High level design###


On a high level a Remact system is split in three parts:

* **Contracts**: Shared interfaces and data structures are delivered in their own assembly.
  The datastructures may have a dependency to Nowtonsoft.Json because of serialization attributes.
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
Data inside an actor is as consistant as the incoming events allow it to be.
No other threads may access the objects of an actor, therefore the actor itself has no critical sections (locks).  

The actor has ports. These are linked to other actors on the same or on a remote process.  
All incoming events are received as messages passed through one of the ports. 
The actor has one message queue that queues all incoming messages (requests, responses, notifications or errors).  


**Client ports**

A client port is built of a RemactPortProxy that is connected to a RemactPortService of another actor.  
Messages are sent by the actor to the proxy of the connected service port. Sending is a non blocking operation.  
Optionally the remote actor may send a reply message. It is handled as an asynchronous callback event  
in a lambda expression or in a task continuation of the origin actor.  
Contrary to the normal client/server pattern, the service port may send notification and request messages to a client. 
These messages are handled in a method defined by the contract interface.  
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
Other serializers like msgpack-cli or a dynamic switching serializer are pluggable.
The folder **Remact.Net.Protocol** contains the currently implemented combinations of protocols and serializers.


**Methods**

Messages are sent to the method that handles the message payload type as a single input parameter.  
The method also defines the reply payload as the return type.  
A void method will normally not reply a message.  
But in case of error or exception, also void methods will reply an ErrorMessage.  


**Contract interface**

A contract interface defines the set of methods and the corresponding request and response message types that are available
on a certain client or service port.  
On the receiving side the methods will have the specified, single message payload parameter and additional parameters
for message source identification and session data.  

The folder **src/Remact.Net/Contracts** and **test/SpeedTestApp/src/Contracts** contains interfaces for remotely callable methods and their
corresponding request- and response messages. These definitions and their XML comments form the basic interface definition of actors.

These contracts must be present on both sides of the communication channel.
For actors not written in a .NET programming language (e.g. Java Script), the interface contract must be translated. 


**Remact.Catalog application**

Remact.Catalog is an application containing the catalog actor. The catalog is informed about all Remact service ports in a network.  
The catalog knows all coordinates like service name, version, host and TCP portnumber.  
The catalog actor synchronizes itself with partner catalog actors on other hosts.  

Normally each host runs a catalog actor. To add a new host into a Remact network, just reference a running catalog host.
Then, all actors including those on the new host are informed about the status of all Remact service ports.  
The catalog actor is for actors what a DNS server is for hosts.


**Anonymous client applications**

Applications that have only client functionality can run in a browser (using Java Script) or as a .NET application.
These applications are not registered in the catalog but they can use a catalog actor to get coordinates of 
open service ports. 



###Communication models###


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

Remact uses the following layers when receiving a message from a remote actor:
* The .NET TCP layer raises an event on a threadpool thread
* The Alchemy.WebSocketClient or -Service interpretes the data frame and dispatches a text string to the protocol layer
* The Wamp- or Json.RPC protocol layer deserializes the payload to a [Newtonsoft.Json.Linq.JToken](http://weblog.west-wind.com/posts/2012/Aug/30/Using-JSONNET-for-dynamic-JSON-parsing)
* The RemactClient or -Service switches to the correct actor thread, builds a RemactMessage and and handles Remact internal messages 
* Other messages are handled by the RemactDispatcher. It finds the addressed method name in the list of supported contract interfaces,
  converts the payload to the .NET type defined as first parameter of the addressed method and
  invokes the addressed method by passing the strong typed payload, the RemactMessage and the session data as parameters
* The called method may accept any serializable payload type, a Newtonsoft.Json.Linq.JToken or a [dynamic object](http://msdn.microsoft.com/en-us/library/dd264736%28v=vs.110%29.aspx)
* When the addressed method could not be found or the received stream could not be converted to the correct .NET type,
  a default message handler is called
* The protocol layer serializes the return type of the called method and send it as a response



How to build and test Remact.Net
--------------------------------
You have to clone these (small) repos to be able to build:  

      $ git clone https://github.com/steforster/Remact.Net.git  
      $ git clone https://github.com/steforster/Alchemy-Websockets.git  
      $ git clone https://github.com/JamesNK/Newtonsoft.Json.git  
      $ git clone https://github.com/Code-Sharp/Newtonsoft.Msgpack.git  
      $ git clone https://github.com/msgpack/msgpack-cli.git  

Afterwards your git project folder should look like this:

      $ ls  
      Alchemy-Websockets  msgpack-cli  Newtonsoft.Json  Newtonsoft.MsgPack  Remact.Net  ...  


The Remact.VS2012 solution is used for VisualStudio 2012 under Windows.  
Use Remact.Mono.sln for MonoDevelop 4.0.12 under Ubuntu 14.04.

Both solutions currently reference the same projects, target the Mono/.NET 4.0 frameworks 
and generate their output into a 'Mono' folder.  

To test under Windows you may start "test\SpeedTestApp\Mono\_startTest2.cmd",  
under Ubuntu you may start "test\SpeedTestApp\Mono\_startTest2.sh".



License
-------
Remact.Net is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php).
Copyright (c) 2014, Stefan Forster.


