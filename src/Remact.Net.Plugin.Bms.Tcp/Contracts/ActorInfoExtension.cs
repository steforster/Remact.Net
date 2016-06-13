
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
            return reader.ReadBlock(() =>
                {
                    var dto = new ActorInfo();
                    dto.IsServiceName = reader.ReadBool();
                    dto.IsOpen = reader.ReadBool();
                    dto.Name = reader.ReadString();
                    dto.AppName = reader.ReadString();
                    dto.AppInstance = reader.ReadInt32();
                    dto.ProcessId = reader.ReadInt32();
                    dto.AppVersion = new Version(reader.ReadString());
                    dto.CifComponentName = reader.ReadString();
                    dto.CifVersion =  new Version(reader.ReadString());
                    dto.HostName = reader.ReadString();
                    dto.Uri = new Uri(reader.ReadString());
                    dto.ClientId = reader.ReadInt32();
                    dto.AddressList = (List<string>)reader.ReadStrings();
                    dto.TimeoutSeconds = reader.ReadInt32();
                    dto.CatalogHopCount = reader.ReadInt32();
                    dto.ApplicationRunTime = TimeSpan.FromTicks(reader.ReadInt64());
                    return dto;
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
