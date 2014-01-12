
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

**Uri's** do have the following format:

	<ActorPortName> / <MethodName> / <FullQualifiedPayloadType>

Uri's are case sensitive.
All segments of the uri are optional. The segment separator is '/'.
Segment separators after a segment are NOT optional.
Segment separators at the beginning of an uri are optional.

Example uri's:

	"Remact.Net.ActorInfo": Payload is of type "Remact.Net.ActorInfo". It will be handled in any appropriate method
                            provided by the destination side.
							The Uri "/Remact.Net.ActorInfo" or "//Remact.Net.ActorInfo" lead to the same result.

    "GetSomeData/":			The message will be handled by the method "GetSomeData". The payload is converted to
							the c# type of the first argument of this method.

							When a method "GetSomeData" without argument exists, and the payload is null, 
							this method will be called.

							When only a method "GetSomeData" with first argument type "object" exists, 
							this method will be called and the the payload is passed as argument of type "JToken".

    "DoSomeThing/MyAssemply.MyType": 
	                        The message will be handled by the method "DoSomeThing" when the first argument of this
	                        method has the type "MyAssemply.MyType".

							Otherwise, when a method "DoSomeThing" with first argument type "object" exists, 
							this method will be called after converting the payload to "MyAssemply.MyType".

							As last resort, when a method "DoSomeThing" with first argument type "JToken" exists, 
							this method will be called.

    "ActorName/DoIt/MyAssemply.MyType":
							On a ActorInput with TCP-portsharing, the actor "ActorName" is located.
							There, method DoIt with first argument of type "MyAssemply.MyType" is called.
							The same method matching rules as above are applicable.

    "ActorName//MyAssemply.MyType":
							On a ActorInput with TCP-portsharing, the actor "ActorName" is located.
							There, a method with first argument of type "MyAssemply.MyType" is called.
							The same method matching rules as above are applicable.

    "ActorName///":         On a ActorInput with TCP-portsharing, the actor "ActorName" is located.
							There, a method with first argument of type "JToken" is called.

	"///" or "":			On the receiving actor (without TCP-portsharing), 
	                        a method with first argument of type "JToken" is called.
