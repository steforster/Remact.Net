
Remact uses only the following [Wamp](http://wamp.ws/) message types:
v1Call, v1CallResult, v1CallError, v1Event.

The normal client server pattern is supported by **v1Call** and **v1CallResult**:
ActorOutputs send requests to an ActorInput and get the response to this request.
For a v1Call, Remact supports only ONE argument.
This means, there are a maximum of 4 JTokens in the JArray of a v1Call-message.

Event notifications do not have to be subscribed. A server can send notifications to each connected client.
This means, that an ActorInput will send a **v1Event** message to its connected ActorOutput.

**v1CallError** is sent from server to client when the request cannot be processed on the server side.
Additionally v1CallError can be sent from client to server when the response or the notification cannot 
be processed on the client side.

**Curie's** are not used by Remact so far.

**Uri's** are case sensitive, they do have the following format:

	a) <MethodName> 
    b) Remact.ActorInfo.<Use-enum>
	c) <Type.FullName of the error detail>

Variant (a) is used for v1Call, v1Event.
Variant (b) is used for Remact internal messages (v1Call, v1CallResult).
Variant (c) is used for v1CallError messages
 
