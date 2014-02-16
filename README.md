Remote Actors for .NET
======================

**Remact.Net** facilitates the development of [actors](http://en.wikipedia.org/wiki/Actor_model) in .NET languages.

It is a class library written in C-Sharp.  
It runs in Microsoft .NET and Linux-Mono environments.  

**Project status**  
Remact.Net is work in progress. Some integration test are running.  
[AsyncWcfLib](http://sourceforge.net/projects/asyncwcflib/) is the predecessor of Remact.Net.

The motivation for this new project is improved support for bidirectional communication,  
higher performance on Linux/Mono based environments and wider interoperability  
so that actors written in Java or JavaScript (browser based actors) can participate.



### Feature list

The following goals have been reached:

- [*] Small and clean API allows to dynamically create lightweight actors
- [*] Local actors (message passing between threads)
- [*] Remote actors (message passing between hosts or processes)
- [*] WebSockets, Json and other open standards are used to link Remact actors.
- [*] High throughput on Linux and Windows: More than 5000 request/respose pairs per second.



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
I would like to thank all participants for their contribution to the open source community.

* [WebSocket](http://tools.ietf.org/html/rfc6455), the IETF standard RFC6455

* [Alchemy-Websockets](https://github.com/Olivine-Labs/Alchemy-Websockets), a class library from Olivine-Labs.
  I had to considerably improve this library. Therefore, I created another [fork](https://github.com/steforster/Alchemy-Websockets.git) of this project.

* [WAMP](http://wamp.ws/), the WebSocket Application Messaging Protocol

* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json), a class library for Json serialization

* [Log4Net](http://logging.apache.org/log4net/), logging component from Apache

* [NUnit](http://www.nunit.org/), unit testing infrastructure

* [Nito.Async.ActionThread](http://nitoasync.codeplex.com/) is used for testing console applications

* [WampSharp](https://github.com/darkl/WampSharp) is not used as a component, but I have lent some ideas from this WAMP implementation



Documentation
-------------
Currently, [the main conceptual ideas](http://sourceforge.net/p/asyncwcflib/wiki/Actors/) should be read on the AsyncWcfLib pages.

The folder **src/Remact.Net/Contracts** contains remotely callable methods and their corresponding request- and response messages.
These definitions and their XML comments form the basic interface definition of actors.

Application actors will add more contracts in other assemblies. 
These definitions must be present on both sides of the communication channel.
For actors not written in a .NET programming language (e.g. Java Script), the interface definition must be translated. 

Receiving of WAMP messages is done in five steps:
* Deserialization to a [Newtonsoft.Json.Linq.JToken](http://weblog.west-wind.com/posts/2012/Aug/30/Using-JSONNET-for-dynamic-JSON-parsing)
* Dispatching to the addressed actor 
* Switching to the thread bound to the actor
* Optional converting to a strongly typed object or to a [dynamic object](http://msdn.microsoft.com/en-us/library/dd264736%28v=vs.110%29.aspx)
* Optional dispatching to a method having the matching parameter type



How to build
------------
The Remact.Mono solution is used for VisualStudio 2012 and for MonoDevelop 2.8.6.3.  
Source projects of other git repositories are referenced from Remact.Mono.sln.  
You have to clone all these (small) repos to be able to build Remact:

      $ git clone https://github.com/steforster/Remact.Net.git  
      $ git clone https://github.com/steforster/Alchemy-Websockets.git  
      $ git clone https://github.com/JamesNK/Newtonsoft.Json.git  

Afterwards your project folder should look like this:

      $ ls  
      Alchemy-Websockets  Newtonsoft.Json  Remact.Net  ...  



License
-------
Remact.Net is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php).
Copyright (c) 2014, Stefan Forster.


