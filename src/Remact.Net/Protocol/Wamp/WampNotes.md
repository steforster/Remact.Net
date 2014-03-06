
Limits of the implementation of Wamp in Remact
----------------------------------------------

Remact uses only the following [Wamp](http://wamp.ws/) message types:  
v1Call, v1CallResult, v1CallError, v1Event.  

The request/response pattern is supported by **v1Call** and **v1CallResult**:  
For a v1Call, Remact supports only ONE argument.  
This means, there are a maximum of 4 JTokens in the JArray of a v1Call-message.  

Event notifications do not have to be subscribed in Remact. A server can send notifications to each connected client.  
'Publish to all' means, an RemactPortService will notify a **v1Event** message to all its connected client actors.  

**v1CallError** is sent from server to client when the request cannot be processed on the server side.  
Additionally in Remact, a v1CallError can be sent from client to server when the response or the notification cannot   
be processed on the client side.  

**Curie's** are not used in Remact.

**Uri's** are case sensitive, they do have one of the following formats in Remact:

	a) <MethodName>  
    b) Remact.ActorInfo.<Use-enum>  
	c) <Type.AssemblyQualifiedTypeName>  

Variant (a) is used for v1Call. The method name is registered in the InputDispatcher of the receiving RemactPortService.  
Variant (b) is used for messages to be processed by the RemactService or the RemactCatalog (v1Call, v1CallResult).  
Variant (c) is used in v1CallError messages to transfer the .NET type of error detail.  
Variant (c) is used in v1Event messages to transfer the .NET type of the payload.  

When the AssemblyQualifiedTypeName of variant (c) is empty, the errorDetail or notification payload is passed as 
Json.Net.JToken to the application.

An example for an AssemblyQualifiedTypeName of variant (c) is:

    "Remact.Net.ErrorMessage, Remact.Net, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null"

The string contains: Type.FullName, Assembly.Name, Assembly.Version, Assembly.Culture, Assembly.PublicKeyToken.
 
