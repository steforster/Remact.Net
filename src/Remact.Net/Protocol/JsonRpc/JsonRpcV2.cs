
// Copyright (c) 2014, github.com/steforster/Remact.Net

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Json rpc v2 message for request, reply, error and notification.
    /// </summary>
    public class JsonRpcV2Message
    {
        /// <summary>
        /// Member of all message types.
        /// A String specifying the version of the JSON-RPC protocol. MUST be exactly "2.0".
        /// </summary>
        public string jsonrpc;
        
        /// <summary>
        /// Member of request and notification message.
        /// A String containing the name of the method to be invoked. 
        /// Method names that begin with the word rpc followed by a period character (U+002E or ASCII 46) 
        /// are reserved for rpc-internal methods and extensions and MUST NOT be used for anything else.
        /// </summary>
        public string method;
        
        /// <summary>
        /// Member of request and notification message.
        /// An object that holds the first parameter to be used during the invocation of the method.
        /// Params may be a structure, an array, a single value or null.
        /// </summary>
        public object params1;
        
        /// <summary>
        /// This member is REQUIRED on success in reply messages.
        /// This member MUST NOT exist if there was an error invoking the method.
        /// The value of this member is determined by the method invoked on the Server.
        /// </summary>
        public object result;
        
        /// <summary>
        /// This member is REQUIRED on error in reply message.
        /// This member MUST NOT exist if there was no error triggered during invocation.
        /// The value for this member MUST be an Object as defined in section 5.1.
        /// </summary>
        public JsonRpcV2Error error;
        
        /// <summary>
        /// Member of request, reply and error message.
        /// An identifier established by the Client that MUST contain a String, Number, or null. 
        /// If id is null, it is assumed to be a notification that needs no reply. 
        /// </summary>
        public string id;
    }
    
/*
    /// <summary>
    /// Json rpc v2 request or notification message.
    /// </summary>
    public class JsonRpcV2Request : JsonRpcV2Message
    {
        /// <summary>
        /// A String containing the name of the method to be invoked. 
        /// Method names that begin with the word rpc followed by a period character (U+002E or ASCII 46) 
        /// are reserved for rpc-internal methods and extensions and MUST NOT be used for anything else.
        /// </summary>
        public string method;
        
        /// <summary>
        /// An object that holds the first parameter to be used during the invocation of the method.
        /// Params may be a structure, an array, a single value or null.
        /// </summary>
        public object params1;
    }
    

    /// <summary>
    /// Json rpc v2 response or error message.
    /// </summary>
    public class JsonRpcV2Response : JsonRpcV2Message
    {
        /// <summary>
        /// This member is REQUIRED on success.
        /// This member MUST NOT exist if there was an error invoking the method.
        /// The value of this member is determined by the method invoked on the Server.
        /// </summary>
        public object result;
        
        /// <summary>
        /// This member is REQUIRED on error.
        /// This member MUST NOT exist if there was no error triggered during invocation.
        /// The value for this member MUST be an Object as defined in section 5.1.
        /// </summary>
        public JsonRpcV2Error error;
    }
*/    
    /// <summary>
    /// Json rpc v2 error object.
    /// </summary>
    public class JsonRpcV2Error
    {
        /// <summary>
        /// A Number that indicates the error type that occurred.
        /// </summary>
        public int code;
        
        /// <summary>
        /// A String providing a short description of the error.
        /// The message SHOULD be limited to a concise single sentence.
        /// </summary>
        public string message;
        
        /// <summary>
        /// A Primitive or Structured value that contains additional information about the error.
        /// This may be omitted.
        /// The value of this member is defined by the Server (e.g. detailed error information, nested errors etc.).
        /// </summary>
        public object data;
    }
    
    
    /// <summary>
    /// Predefined errorcodes.
    /// </summary>
    public enum JsonRpcV2ErrorCode
    {
    }
}