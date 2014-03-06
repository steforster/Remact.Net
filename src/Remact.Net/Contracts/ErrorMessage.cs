
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Text;                 // StringBuilder
using Newtonsoft.Json;

namespace Remact.Net
{
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
    public Code Error 
    {
        get
        {
            if (error <= 0 || error >= (int)Code.Last) 
                 {return Code.Undef;}
            else if (error > (int)Code.LastAppCode && error < (int)Code.NotImplementedOnService)   
                 {return Code.Undef;}
            else {return(Code) error;}
        }

        set {error = (int)value;}
    }
    
    /// <summary>
    /// Most Error-Codes uniquely definies the code position where the error occured.
    /// </summary>
    public enum Code
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
      /// The service-application does not accept this request.
      /// Set by library user.
      /// </summary>
      AppRequestNotAcceptedByService,
      
      /// <summary>
      /// The request could not be handled by the service-application as some subsystems are not ready.
      /// Set by library user.
      /// </summary>
      AppServiceNotReady,

      /// <summary>
      /// The request could not be handled by the service-application as the data is not available.
      /// Set by library user.
      /// </summary>
      AppDataNotAvailableInService,

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
      /// RemactMessage with unknown client id.
      /// </summary>
      ClientIdNotFoundOnService,

      /// <summary>
      /// This and higher enum values are internally mapped to 'Undef' (used to check version compatibility).
      /// </summary>
      Last
    }
    
    /// <summary>
    /// Create an empty error message
    /// </summary>
    public ErrorMessage () {Message = String.Empty;}
    
    /// <summary>
    /// Create a error message.
    /// </summary>
    /// <param name="err">general reason.</param>
    /// <param name="text">unique information where te error occured.</param>
    public ErrorMessage (Code err, string text = null)
    {
      Error = err;
      Message = text;
      if (Message == null)
      {
          Message = String.Empty;
      }
    }// CTOR 1
    
    /// <summary>
    /// Create a error message.
    /// </summary>
    /// <param name="err">general reason.</param>
    /// <param name="ex">detailed information about the error.</param>
    public ErrorMessage (Code err, Exception ex)
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
    /// Log the error message
    /// </summary>
    /// <returns>string containing all information about the error</returns>
    public override string ToString ()
    {
      StringBuilder err = new StringBuilder (1000);
      err.Append("RemactError ");
      if (Error == Code.Undef) {err.Append("error="); err.Append(error);}
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
