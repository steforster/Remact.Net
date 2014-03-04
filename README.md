Remote Actors for .NET
======================

**Remact.Net** facilitates the development of remote actors in .NET languages.

It is a class library written in C-Sharp.  
It runs in Microsoft .NET and Linux-Mono environments.  

The [Actor model](http://en.wikipedia.org/wiki/Actor_model) is inspired by physics and by nature.  
It brings order to multithread, multicore, multihost systems.  
These days many systems have such requirements. But industrial control systems with dynamically connected  
and movable intelligent subsystems have been in focus of the design of Remact.Net.
Therefore, Remact.Net supports many instances of the same application running on one or on distributed hosts.
It also supports dynamic discovery of actors and does not require configuration of host names and TCP ports.

In spite of all these features, Remact.Net is remarkably slim and easy to adapt for special needs.


**Project status**  
Remact.Net is work in progress. Some integration test are running.  
[AsyncWcfLib](http://sourceforge.net/projects/asyncwcflib/) is the predecessor of Remact.Net.

The motivation for this new project is to improve support for bidirectional communication,  
higher performance on Linux/Mono based environments and interoperability  
with actors written in Java or JavaScript (browser based actors).



### Feature list

The following goals have been reached:

- [*] Small and clean API allows to dynamically create lightweight actors
- [*] Local actors (message passing between threads)
- [*] Remote actors (message passing between hosts or processes)
- [*] WebSockets, Json and other open standards are used to link Remact actors.
- [*] High throughput on Linux and Windows: More than 5000 request/respose pairs per second between processes.



### Related work

Some programming languages have built in support for remote actors:

* [Scala](http://www.scala-lang.org/).RemoteActors
* [Erlang](http://www.erlang.org/)

For other languages there are libraries to help programming remote actors:

* [Java and Scala: The Akka library](http://akka.io/)
* [C#: The Stact library](https://github.com/phatboyg/Stact)
* [C#: AsyncWcfLib](http://sourceforge.net/projects/asyncwcflib/)



Dependencies and standards
--------------------------
Remact.Net is built on open standards and uses open source components.
I would like to thank all who built these bits for their contribution to the open source community.

* [WebSocket](http://tools.ietf.org/html/rfc6455), the IETF standard RFC6455

* [Alchemy-Websockets](https://github.com/Olivine-Labs/Alchemy-Websockets), a class library from Olivine-Labs.
  I had to considerably improve this library. Therefore, I created another [fork](https://github.com/steforster/Alchemy-Websockets.git) of this project.

* [WAMP](http://wamp.ws/), the WebSocket Application Messaging Protocol

* [JSON-RPC](http://www.jsonrpc.org/specification), instad of WAMP, Json-RPC can be used on top of the WebSocket protocol.

* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json), a class library for Json serialization

* [Log4Net](http://logging.apache.org/log4net/), logging component from Apache

* [NUnit](http://www.nunit.org/), unit testing infrastructure

* [Nito.Async.ActionThread](http://nitoasync.codeplex.com/) is used for testing console applications

* [WampSharp](https://github.com/darkl/WampSharp) is not used as a component, but I have lent some ideas from this WAMP implementation



Documentation
-------------
Currently, [the main conceptual ideas](http://sourceforge.net/p/asyncwcflib/wiki/Actors/) should be read on the AsyncWcfLib pages.

The folder **src/Remact.Net/Contracts** and **test/SpeedTestApp/src/Contracts** contains interfaces for remotely callable methods and their
corresponding request- and response messages. These definitions and their XML comments form the basic interface definition of actors.

These contracts must be present on both sides of the communication channel.
For actors not written in a .NET programming language (e.g. Java Script), the interface contract must be translated. 

Receiving of WAMP messages is done in five steps:
* Deserialization to a [Newtonsoft.Json.Linq.JToken](http://weblog.west-wind.com/posts/2012/Aug/30/Using-JSONNET-for-dynamic-JSON-parsing)
* Dispatching to the addressed actor 
* Switching to the thread bound to the actor
* Optional converting to a strongly typed object or to a [dynamic object](http://msdn.microsoft.com/en-us/library/dd264736%28v=vs.110%29.aspx)
* Optional dispatching to a method having the matching parameter type


Conceptual parts
----------------

**Actors**

An actor is a group of objects that are accessed by one thread. Data inside an actor is consistant.  
An actor may feature several input and output ports. These ports are connected to other actors on the same or on a remote process.  
The actor contains one message queue. All incoming messages pass this queue (incoming requests, incoming responses).  


**Outputs**

An output port is connected to an input port of another actor.  
Messages are sent by the output to the connected input. Sending is a non blocking operation.  
Optionally the remote actor may send a reply message. It is handled as an asynchronous callback event  
in a lambda expression or in an async method.  
The remote actor may send notification messages to an output. It is handled in a method defined by the contract interface.  
Disconnecting the output is signaled to the input. A disconnect signal may also be issued by the input.


**Inputs**

Many remote outputs may be connected to one input port.  
Once connected, messages may be sent from the output to the input but also from the input to the output.  
Address and version information for each connected output is available from the input server.  
Additional session data may be kept for each connected actor output.  
Periodic messages should be exchanged by the actors to check the communication channels.


**ActorMessage and Payload**

The ActorMessage class addresses source and destination port. It is used to route the payload data through the system.
The payload may be of any serializable object type.  
Serialization is done by Newtonsoft.Json. Therefore, attributes like [JsonProperty] and [JsonIgnore] may be used to   
control the serialization process. By default all public properties and fields are serialized.


**Methods**

Messages are sent to the method that handles the message payload type as a single input parameter.  
The method also defines the reply payload as the return type.  
A void method will normally not reply a message.  
In case of error or exception, methods will reply an ErrorMessage.  


**Contract interface**

A contract interface defines the set of methods and the corresponding request and response message types that are available
on a certain input or output port.  
On the receiving side the methods will have the specified, single message payload parameter and additional parameters  
for message source identification and session data.



Communication models
--------------------

**Client / Service**

One actor may contain several clients and services.
The output (client) sends a request to the input (service) of another actor and will get the reply from it.


**Notifications**

The input port may send a callback notification to the output port.
The output may send a notification to the input port.
Notifications are defined as parameter of a void method of the receiving port contract interface.


**Service / Client**

Communication is symmetrical. Therefore inputs may also send requests to a method of the connected output  
and get a response from it. The difference between input and output lies in the 1 : many relationship  
and in the active part of the output during connection buildup.  
Some day we hopefully will find a better name for the two connected communication port types.


**Publish / Subscribe**

An input port can send messages to all its connected output ports.
Connecting to such an input in fact means - subscribing to its publications.
The publisher knows who received its publications because all communication is done through reliable  
TCP connections to known partners.



How to build and test Remact.Net
--------------------------------
The Remact.Mono solution is used for VisualStudio 2012 and for MonoDevelop 2.8.6.3.  
Source projects of other git repositories are referenced from Remact.Mono.sln.  
You have to clone all these (small) repos to be able to build Remact:

      $ git clone https://github.com/steforster/Remact.Net.git  
      $ git clone https://github.com/steforster/Alchemy-Websockets.git  
      $ git clone https://github.com/JamesNK/Newtonsoft.Json.git  

Afterwards your project folder should look like this:

      $ ls  
      Alchemy-Websockets  Newtonsoft.Json  Remact.Net  ...  

Newtonsoft.Json needs some small adaptions to run under Mono. I copied "Newtonsoft.Json.Net40.csproj" to 
"Newtonsoft.Json.Mono.csproj" and switched the target framework to 4.0 (not client profile).

Then you should be able to compile in VS2010 or VS2012 the "Remact.Mono.sln" (Release) and start "test\SpeedTestApp\Mono\_startTest2.cmd".


License
-------
Remact.Net is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php).
Copyright (c) 2014, Stefan Forster.


