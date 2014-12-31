
// Copyright (c) https://github.com/steforster/Remact.Net

using System;

namespace Remact.Net
{
    /// <summary>
    /// An unexpected RemactMessage is converted to and thrown as a RemactException on the receiving side.
    /// When the RemactMessage type is 'Error' and the payload is 'ErrorMessage', the payload is converted to a inner exception.
    /// In all other cases, the payload is a dynamic, serialized object e.g. Newtonsoft.Json.Linq.JToken. The user may inspect its content.
    /// </summary>
    public class RemactException : Exception
    {
        /// <summary>
        /// Initializes a RemactException.
        /// </summary>
        /// <param name="msg">The received RemactMessage.</param>
        /// <param name="errorcode">The Remact ErrorCode.</param>
        /// <param name="message">A textual exeption message.</param>
        /// <param name="innerEx">In case an ErrorMessage is received as payload, it can be converted and added as inner exception.</param>
        /// <param name="sourceStackTrace">Additional data from the ErrorMessage.</param>
        public RemactException (RemactMessage msg, ErrorCode errorcode, string message = null, Exception innerEx = null, string sourceStackTrace = null)
            : base (message, innerEx)
        {
            RemactMessage = msg;
            ErrorCode = errorcode;
            SourceStackTrace = sourceStackTrace;
        }

        /// <summary>
        /// The received RemactMessage.
        /// </summary>
        public RemactMessage RemactMessage { get; protected set; }

        /// <summary>
        /// Get the Error-Code
        /// </summary>
        public ErrorCode ErrorCode { get; protected set; }

        /// <summary>
        /// In case a InnerException represents an ErrorMessage, the SourceStackTrace may contain additional information from the sending side.
        /// </summary>
        public string SourceStackTrace { get; protected set; }
    }
}
