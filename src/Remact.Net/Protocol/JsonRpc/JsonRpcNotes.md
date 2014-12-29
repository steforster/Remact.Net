
Limits of the implementation of JsonRpc in Remact
-------------------------------------------------

Remact currently supports MsgPack binary serialization on the JsonRpc V2 protocol.
TODO: Use WebSocket.Frame.IsBinary property to select Json or MsgPack serialization.

Batch requests are not supported (see http://www.jsonrpc.org/specification).

The id field is a positive integer number.
