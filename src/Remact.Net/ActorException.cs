
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class ActorException ==

    /// <summary>
    /// An exceptional ActorMessage.
    /// </summary>
    public class ActorException : Exception
    {
        public ActorException (ActorMessage actorMsg, string message = null, Exception innerEx = null)
            : base (message, innerEx)
        {
            ActorMessage = actorMsg;
        }

        public ActorMessage ActorMessage { get; private set; }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class ActorException<T> ==

    /// <summary>
    /// An exceptional typed ActorMessage.
    /// <typeparam name="T">The type of the payload.</typeparam>.
    /// </summary>
    public class ActorException<T> : ActorException
    {
        public ActorException(ActorMessage<T> actorMsg, string message = null, Exception innerEx = null)
            : base(actorMsg, message, innerEx)
        {
            ActorMessage = actorMsg;
        }

        public ActorMessage<T> ActorMessage { get; private set; }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
}
