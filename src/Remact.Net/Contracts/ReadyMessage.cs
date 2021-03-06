
// Copyright (c) https://github.com/steforster/Remact.Net

using System.Runtime.Serialization;// IExtensibleDataObject
using System.Threading;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class IExtensibleRemactMessage ==

    /// <summary>
    /// Represents the interface used for the base message class.
    /// It is provided for library users wishing to write their own base message implementation.
    /// </summary>
    public interface IExtensibleRemactMessage : IExtensibleDataObject
    {
        /// <summary>
        /// This SynchronizationContext is currently allowed to modify the message members. 
        /// The value is automatically set by Remact.
        /// </summary>
        SynchronizationContext BoundSyncContext { get; set; }

        /// <summary>
        /// True, when a message has been sent and the BoundSyncContext has to match the current SynchronizationContext in order to be allowed to modify message members.
        /// The value is automatically set by Remact.
        /// </summary>
        bool IsSent { get; set; }

        /// <summary>
        /// Returns true, when the message is safe for read and write by an Actor in its own SyncronizationContext.
        /// </summary>
        bool IsThreadSafe { get; }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class ReadyMessage ==

    /// <summary>
    /// <para>A message payload without information content. Just for alive check or default response.</para>
    /// </summary>
    public class ReadyMessage
    {
    }

    #endregion
    //----------------------------------------------------------------------------------------------
}
