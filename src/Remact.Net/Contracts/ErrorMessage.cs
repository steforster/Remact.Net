
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Text; // StringBuilder
using Newtonsoft.Json;

namespace Remact.Net
{
    /// <summary>
    /// Most Error-Codes uniquely definies the code position where the error occured.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// Code not set or unknown.
        /// </summary>
        Undef,

        /// <summary>
        /// No error.
        /// </summary>
        Ok,

        // -------------------- Errorcodes for free use, not used by Remact ------------------
        /// <summary>
        /// The actor does not know how to handle this message.
        /// Set by library user.
        /// </summary>
        ActorReceivedMessageForUnknownDestinationMethod,

        /// <summary>
        /// The message could not be handled by the actor as some subsystems are not ready.
        /// Set by library user.
        /// </summary>
        ActorNotReady,

        /// <summary>
        /// The request could not be handled by the actor as the data is not available.
        /// Set by library user.
        /// </summary>
        ActorHasDataNotAvailable,

        /// <summary>
        /// This and enum values up to NotImplementedOnService are internally mapped to 'Undef' (used to check version compatibility).
        /// </summary>
        LastAppCode,

        // -------------------- Errorcodes used by Remact ------------------
        /// <summary>
        /// The service throws a NotImplementedException or NotSupportedException. This is the case, when an unknown request has been received.
        /// A software update on the server may be needed.
        /// </summary>
        NotImplementedOnService = 1000,

        /// <summary>
        /// The service throws an ArgumentException or ArgumentNullException. This is the case when wrong request arguments are sent.
        /// </summary>
        ArgumentExceptionOnService,

        /// <summary>
        /// Another exception occured while executing the request in the service-application.
        /// </summary>
        UnhandledExceptionOnService,

        /// <summary>
        /// Cannot send as the client is not (yet) connected.
        /// </summary>
        NotConnected,

        /// <summary>
        /// Error on service, when trying to disconnect.
        /// </summary>
        CouldNotDisconnect,

        /// <summary>
        /// Exception received when waiting for response.
        /// </summary>
        CouldNotSend,

        /// <summary>
        /// Error while dispaching a message to another thread inside the application.
        /// </summary>
        CouldNotDispatch,

        /// <summary>
        /// Exception while deserializing on service side.
        /// </summary>
        ReqestNotDeserializableOnService,

        /// <summary>
        /// Exception while deserializing on client side.
        /// </summary>
        ResponseNotDeserializableOnClient,

        /// <summary>
        /// The Ask method was expecting a different payload type.
        /// </summary>
        UnexpectedResponsePayloadType,

        /// <summary>
        /// RemactMessage with unknown client id.
        /// </summary>
        ClientIdNotFoundOnService,

        /// <summary>
        /// ErrorResponse to ServiceOpened.
        /// </summary>
        ServiceIsBackup,

        /// <summary>
        /// Error response to LookupService.
        /// </summary>
        ServiceNameNotRegisteredInCatalog,

        /// <summary>
        /// This and higher enum values are internally mapped to 'Undef' (used to check version compatibility).
        /// </summary>
        Last
    }

  /// <summary>
  /// <para>An error payload is generated when an exeption or timeout occurs on client or service side.</para>
  /// <para>The message contains a code indicating where the error occured and a text representation of the exception.</para>
  /// </summary>
  public class ErrorMessage
  {
    /// <summary>
    /// For version tolerance, error is streamed as int.
    /// Application internally, the enum 'Error' is used.
    /// </summary>
    [JsonProperty]
    private int error;
    
    /// <summary>
    /// Get the exception information.
    /// </summary>
    public string Message;

    /// <summary>
    /// Get the exception information (may be null).
    /// </summary>
    public string InnerMessage;

    /// <summary>
    /// Get the exception information (may be null).
    /// </summary>
    public string StackTrace;

    /// <summary>
    /// Get or set the Error-Code
    /// </summary>
    [JsonIgnore]
    public ErrorCode Error 
    {
        get
        {
            if (error <= 0 || error >= (int)ErrorCode.Last) 
                 {return ErrorCode.Undef;}
            else if (error > (int)ErrorCode.LastAppCode && error < (int)ErrorCode.NotImplementedOnService)   
                 {return ErrorCode.Undef;}
            else {return(ErrorCode) error;}
        }

        set {error = (int)value;}
    }
    
    
    /// <summary>
    /// Create an empty error message
    /// </summary>
    public ErrorMessage () {Message = String.Empty;}
    
    /// <summary>
    /// Create an error message.
    /// </summary>
    /// <param name="err">general reason.</param>
    /// <param name="text">unique information where te error occured.</param>
    public ErrorMessage (ErrorCode err, string text = null)
    {
      Error = err;
      Message = text;
      if (Message == null)
      {
          Message = String.Empty;
      }
    }// CTOR 1
    
    /// <summary>
    /// Create an error message.
    /// </summary>
    /// <param name="err">general reason.</param>
    /// <param name="ex">detailed information about the error.</param>
    public ErrorMessage (ErrorCode err, Exception ex)
    {
      Error = err;
      if (ex == null)
      {
        Message = String.Empty;
      }
      else
      {
        Message = string.Concat (ex.GetType().Name, ": ", ex.Message);
        string mainText = ex.Message;
        ex = ex.InnerException;
        while (ex != null)
        {
          if (InnerMessage == null) InnerMessage = string.Empty;
          if (mainText == ex.Message) {
            InnerMessage += " ...";
          }
          else {
            InnerMessage = string.Concat (InnerMessage, " Inner ", ex.GetType().Name, ": ", ex.Message);
          }
          ex = ex.InnerException;
        }
      }
    }// CTOR 2

    /// <summary>
    /// Create an error message.
    /// </summary>
    /// <param name="ex">detailed information about the error.</param>
    public ErrorMessage(RemactException ex)
        : this (ex.Error, ex)
    {
    }// CTOR 3

    /// <summary>
    /// Log the error message
    /// </summary>
    /// <returns>string containing all information about the error</returns>
    public override string ToString ()
    {
      StringBuilder err = new StringBuilder (1000);
      err.Append("RemactError ");
      if (Error == ErrorCode.Undef) {err.Append("error="); err.Append(error);}
                          else {err.Append(Error.ToString());}
      err.Append (". ");
      err.Append (Message);
      if (InnerMessage != null && InnerMessage.Length > 0)
      {
        err.Append (Environment.NewLine);
        err.Append ("  ");
        err.Append (InnerMessage);
      }                  

      return err.ToString();
    }
  }
}
