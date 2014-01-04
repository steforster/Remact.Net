Remote Actors for .NET
======================

**Remact.Net** facilitates the development of [actors](http://en.wikipedia.org/wiki/Actor_model) in .NET languages.
It is a class library written in C-Sharp.
It runs in Microsoft .NET and Linux-Mono environements.


### Related work

Some programmming languages have built in support for remote actors:

* [Scala](http://www.scala-lang.org/).RemoteActors
* [Erlang](http://www.erlang.org/)

For other languages there are librarys to help programming remote actors:

* [Java and Scala: The Akka library](http://akka.io/)
* [C#: The Stact library](https://github.com/phatboyg/Stact)
* [C#: AsyncWcfLib (a library developed by me)](http://sourceforge.net/projects/asyncwcflib/)


### Feature list

Remact is work in progress. The following goals are intended to reach:

[ ] Small and clean API like AsyncWcfLib
[ ] Local actors (message passing between threads)
[ ] Remote actors (message passing between hosts or processes)
[ ] Unlike AsyncWcfLib, Remact will be based on open web standards (WebSockets).
    The motivation is better support for bidirectional communication and higher performance
    on Linux/Mono based environments.
[ ] WebSockets and Json are used to link actors written in Java or JavaScript (browser based actors)


Documentation
-------------
Currently, [the main conceptual ideas](http://sourceforge.net/p/asyncwcflib/wiki/Actors/) should be read on the AsyncWcfLib pages.

The folder **src/Remact.Net/Contracts** contains remotly callable methods and their corresponding request- and response messages.
These definitions and their XML comments form the basic interface definition of actors.

Application actors will add more contracts in other assemblies. 
These definitions must be present on both sides of the communication channel.
For actors not written in a .NET programming language (e.g. Java Script), the interface definition must be translated. 

Receiving of WAMP messages is done in five steps:
* Deserialization to a [Newtonsoft.Json.Linq.JObject](http://weblog.west-wind.com/posts/2012/Aug/30/Using-JSONNET-for-dynamic-JSON-parsing)
* Dispatching to the addressed actor 
* Switching to the thread bound to the actor
* Optional converting to a strongly typed object or to a [dynamic object](http://msdn.microsoft.com/en-us/library/dd264736%28v=vs.110%29.aspx)
* Optional dispatching to a method having the matching parameter type


Third party components and standards
------------------------------------
Remact.Net is built on open standards and uses open components, namely

* [WebSocket](http://tools.ietf.org/html/rfc6455), the IETF standard RFC6455
* [Alchemy-Websockets](https://github.com/Olivine-Labs/Alchemy-Websockets), a class library from Olivine-Labs
  I use [segor's fork](https://github.com/segor/Alchemy-Websockets), he has fixed some client side errors
* [WAMP](http://wamp.ws/), the WebSocket Application Messaging Protocol
* [Json class library from Newtonsoft](https://github.com/JamesNK/Newtonsoft.Json) 
* [Log4Net](http://logging.apache.org/log4net/), logging component from Apache
* [NUnit](http://www.nunit.org/), unit testing infrastructure
* [Nito.Async.ActionThread](http://nitoasync.codeplex.com/) is used for testing console applications
* [WampSharp](https://github.com/darkl/WampSharp) is not used as a component, but I have lended many ideas from this WAMP implementation


License
-------
Remact.Net is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php).
Copyright (c) 2014, Stefan Forster.


