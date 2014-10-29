
Limits of the implementation of JsonRpc in Remact
-------------------------------------------------

Remact currently supports MsgPack binary serialization on the JsonRpc V2 protocol.

Batch requests are not supported (see http://www.jsonrpc.org/specification).

The id field is a positive integer number.
