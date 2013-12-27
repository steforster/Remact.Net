Remote Actors for .NET
======================

**Remact.Net** is a class library written in C-Sharp.
It runs in Microsoft .NET and Linux-Mono environements.

It facilitates the development of [actors](http://en.wikipedia.org/wiki/Actor_model) in .NET languages.


### Related work

Some programmming languages have built in support for remote actors:

* [Scala: RemoteActors](http://www.scala-lang.org/)
* [Erlang](http://www.erlang.org/)

For other languages there are librarys to help programming remote actors:

* [Java and Scala: The Akka library](http://akka.io/)
* [C#: The Stact library](https://github.com/phatboyg/Stact)
* [C#: AsyncWcfLib (a library developed by me)](http://sourceforge.net/projects/asyncwcflib/)


### Feature list

Remact is work in progress. The following goals are intended to reach:

[ ] Small and clean API like AsyncWcfLib
[ ] Local actors (process internal message passing between threads)
[ ] Remote actors (message passing between hosts)
[ ] Unlike AsyncWcfLib, Remact will be based on open web standards (WebSockets).
    The motivation is better support for bidirectional communication and higher performance
    on Linux/Mono based environments.
[ ] WebSockets and Json are used to link actors written in Java or JavaScript (browser based actors).


Third party components and standards
------------------------------------
Remact.Net is built on open standards and uses open components, namely

* [WebSocket, the IETF standard RFC6455](http://tools.ietf.org/html/rfc6455)
* [Alchemy-Websockets, a class library from Olivine-Labs](https://github.com/Olivine-Labs/Alchemy-Websockets)
* [WAMP, the WebSocket Application Messaging Protocol](http://wamp.ws/)
* [Json class library from Newtonsoft](https://github.com/JamesNK/Newtonsoft.Json)
* [Log4Net, logging component from Apache](http://logging.apache.org/log4net/)
* [NUnit, unit testing infrastructure](http://www.nunit.org/)
* [Nito.Async.ActionThread is used for testing console applications](http://nitoasync.codeplex.com/)


License
-------
Remact.Net is licensed under [MIT](http://www.opensource.org/licenses/mit-license.php).
Copyright (c) 2014, Stefan Forster.


