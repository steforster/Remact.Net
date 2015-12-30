
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Bms1Serializer;
using System.Collections.Generic;

namespace Remact.Net.Plugin.Bms.Tcp
{
    /// <summary>
    /// Extension methods for the ActorInfo message.
    /// </summary>
    public static class ActorInfoExtension
    {
        public const int Bms1BlockTypeId = 1;

        public static int GetBms1BlockTypeId(this ActorInfo msg)
        {
            return Bms1BlockTypeId;
        }

        public static ActorInfo ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock<ActorInfo>((obj) => 
                {
                    var msg = (ActorInfo)obj;
                    msg.IsServiceName = reader.ReadBool();
                    msg.IsOpen = reader.ReadBool();
                    msg.Name = reader.ReadString();
                    msg.AppName = reader.ReadString();
                    msg.AppInstance = reader.ReadInt32();
                    msg.ProcessId = reader.ReadInt32();
                    msg.AppVersion = new Version(reader.ReadString());
                    msg.CifComponentName = reader.ReadString();
                    msg.CifVersion =  new Version(reader.ReadString());
                    msg.HostName = reader.ReadString();
                    msg.Uri = new Uri(reader.ReadString());
                    msg.ClientId = reader.ReadInt32();
                    msg.AddressList = (List<string>)reader.ReadStrings();
                    msg.TimeoutSeconds = reader.ReadInt32();
                    msg.CatalogHopCount = reader.ReadInt32();
                    msg.ApplicationRunTime = TimeSpan.FromTicks(reader.ReadInt64());
                    return msg;
                });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(Bms1BlockTypeId, () => 
                {
                    var msg = (ActorInfo)obj;
                    writer.WriteBool(msg.IsServiceName);
                    writer.WriteBool(msg.IsOpen);
                    writer.WriteString(msg.Name);
                    writer.WriteString(msg.AppName);
                    writer.WriteInt32(msg.AppInstance);
                    writer.WriteInt32(msg.ProcessId);
                    writer.WriteString(msg.AppVersion.ToString()); // TODO version
                    writer.WriteString(msg.CifComponentName);
                    writer.WriteString(msg.CifVersion.ToString()); // TODO version
                    writer.WriteString(msg.HostName);
                    writer.WriteString(msg.Uri.ToString());
                    writer.WriteInt32(msg.ClientId);
                    writer.WriteStrings(msg.AddressList);
                    writer.WriteInt32(msg.TimeoutSeconds);
                    writer.WriteInt32(msg.CatalogHopCount);
                    writer.WriteInt64(msg.ApplicationRunTime.Ticks);
                });
        }
/*
    //----------------------------------------------------------------------------------------------
    /// <summary>
    /// <para>This message payload contains a list of ActorInfo payloads.</para>
    /// <para>It is used by the catalogs to exchange informations.</para>
    /// </summary>
    public class ActorInfoList
    {
        /// <summary>
        /// List of services in a plant.
        /// </summary>
        public List<ActorInfo> Item;

        /// <summary>
        /// Create a ActorInfoList.
        /// </summary>
        public ActorInfoList()
        {
            Item = new List<ActorInfo>(20);
        }*/
    }
}
