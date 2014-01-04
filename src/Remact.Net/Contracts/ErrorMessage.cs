
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;  // List
using System.Reflection;           // Assembly
using System.Text;                 // StringBuilder
using Remact.Net.Internal;
using System.Threading;
using System.Net;                  // IPAddress

namespace Remact.Net
{
  /// <summary>
  /// <para>An error payload is generated when an exeption or timeout occurs on client or service side.</para>
  /// <para>The message contains a code indicating where the error occured and a text representation of the exception.</para>
  /// </summary>
  public class ErrorMessage
  {
    /// <summary>
    /// Get the exception information.
    /// </summary>
    public  string    Message;

    /// <summary>
    /// Get the exception information.
    /// </summary>
    public  string    InnerMessage;

    /// <summary>
    /// Get the exception information.
    /// </summary>
    public  string    StackTrace;

    /// <summary>
    /// Get or set the Error-Code, TODO: do not serialize
    /// </summary>
    public Code Error 
    {
     get{if (z_error < 0 || z_error >= (int)Code.Last) return Code.Undef;
         else if (z_error >= (int)Code.LastAppCode
               && z_error <  (int)Code.NotConnected)   return Code.Undef;
                                                  else return(Code) z_error;
        }
     set{z_error = (int) value;}
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
      
      // -------------------- Errorcodes for free use, not used by AsyncWcfLib ------------------
      /// <summary>
      /// An exception occured while executing the request in the service-application.
      /// Set by library user.
      /// </summary>
      AppUnhandledExceptionOnService,
      
      /// <summary>
      /// The service-application does not know or does not accept this request.
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
      /// This and enum values up to NotConnected are internally mapped to 'Undef' (used to check version compatibility).
      /// </summary>
      LastAppCode,

      // -------------------- Errorcodes used by AsyncWcfLib ------------------
      /// <summary>
      /// Cannot send as the client is not (yet) connected.
      /// </summary>
      NotConnected = 1000,
      
      /// <summary>
      /// Cannot open the client (configuration error).
      /// </summary>
      CouldNotOpen,

      /// <summary>
      /// Cannot open the service connection (refused by target).
      /// </summary>
      ServiceNotRunning,

      /// <summary>
      /// Cannot open the router connection (refused by target).
      /// </summary>
      RouterNotRunning,

      /// <summary>
      /// Exception while sending (serializing) the first connect message.
      /// </summary>
      CouldNotStartConnect,

      /// <summary>
      /// No response from service, when trying to connect.
      /// </summary>
      CouldNotConnect,

      /// <summary>
      /// Wrong response from WCF router, when trying to connect.
      /// </summary>
      CouldNotConnectRouter,
      
      /// <summary>
      /// Exception while sending (serializing) a message.
      /// </summary>
      CouldNotStartSend,
      
      /// <summary>
      /// Exception received when waiting for response.
      /// </summary>
      CouldNotSend,
      
      /// <summary>
      /// Error while dispaching a message to another thread inside the application.
      /// </summary>
      CouldNotDispatch,
      
      /// <summary>
      /// The service did not respond in time. Detected by client.
      /// </summary>
      TimeoutOnClient,
      
      /// <summary>
      /// The service did not respond in time. Detected by service itself.
      /// </summary>
      TimeoutOnService,
      
      /// <summary>
      /// Exception while deserializing or serializing on service side.
      /// </summary>
      ReqOrRspNotSerializableOnService,
      
      /// <summary>
      /// null message received.
      /// </summary>
      RspNotDeserializableOnClient,
      
      /// <summary>
      /// The request-message-type is not registered as a known type on this service.
      /// </summary>
      RequestTypeUnknownOnService,
      
      /// <summary>
      /// ActorMessage with unknown client id.
      /// </summary>
      ClientIdNotFoundOnService,

      /// <summary>
      /// An exception occured while executing the request in the service-application.
      /// </summary>
      ClientDetectedUnhandledExceptionOnService,

      /// <summary>
      /// An exception occured while executing the request in the service-application.
      /// </summary>
      UnhandledExceptionOnService,

      /// <summary>
      /// This and higher enum values are internally mapped to 'Undef' (used to check version compatibility).
      /// </summary>
      Last
    }
    
    /// <summary>
    /// z_error is public but used internally only! Use 'Error' instead!
    /// Reason: http://msdn.microsoft.com/en-us/library/bb924412%28v=VS.100%29.aspx
    /// Error is stramed as int in order to make it reverse compatible to older communication partners
    /// </summary>
    public int z_error = 0;
    
    /// <summary>
    /// Create an empty error message
    /// </summary>
    public ErrorMessage (){}
    
    /// <summary>
    /// Create a error message.
    /// </summary>
    /// <param name="err">general reason.</param>
    /// <param name="text">unique information where te error occured.</param>
    public ErrorMessage (Code err, string text)
    {
      Error        = err;
      Message      = text;
      InnerMessage = String.Empty;
      StackTrace   = String.Empty;
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
        Message      = String.Empty;
        InnerMessage = String.Empty;
        StackTrace   = String.Empty;
      }
      else
      {
        Message      = string.Concat (ex.GetType().Name, ": ", ex.Message);
        string mainText = ex.Message;
        InnerMessage = String.Empty;
        StackTrace   = ex.StackTrace;
        ex = ex.InnerException;
        while (ex != null)
        {
        //InnerMessage += " Inner exception: ";
        //InnerMessage += ex.Payload;
          if (mainText == ex.Message) {
            InnerMessage += " ...";
          }
          else {
            InnerMessage = string.Concat (InnerMessage, " ", ex.GetType().Name, ": ", ex.Message);
          }
          ex = ex.InnerException;
        }
      }
    }// CTOR 2
    
    /// <summary>
    /// Trace the errormessage
    /// </summary>
    /// <returns>string containing all information about the error</returns>
    public override string ToString ()
    {
      StringBuilder err = new StringBuilder (1000);
      err.Append("WcfError ");
      if (Error == Code.Undef) {err.Append("code="); err.Append(z_error);}
                          else {err.Append(Error.ToString());}
      err.Append (". ");
      err.Append (Message); 
      if (InnerMessage.Length > 0)
      {
        err.Append (Environment.NewLine);
        err.Append ("  ");
        err.Append (InnerMessage);
      }                  

      return err.ToString();
    }
  }
}
