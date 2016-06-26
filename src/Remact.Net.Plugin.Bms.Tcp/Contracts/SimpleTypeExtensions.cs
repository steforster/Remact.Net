
// Copyright (c) https://github.com/steforster/Remact.Net

using Remact.Net.Bms1Serializer;

namespace Remact.Net.Plugin.Bms.Tcp
{
    static class SimpleTypeExtensions
    {
        public static string ReadStringFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock<string>(reader.ReadString);
        }

        public static void WriteStringToBms1Stream(object msg, IBms1Writer writer)
        {
            writer.WriteBlock(() => writer.WriteString((string)msg));
        }

        public static object ReadBoolFromBms1Stream(IBms1Reader reader)
        {
            return (object)reader.ReadBlock<bool>(reader.ReadBool);
        }

        public static void WriteBoolToBms1Stream(object msg, IBms1Writer writer)
        {
            writer.WriteBlock(() => writer.WriteBool((bool)msg));
        }

        public static object ReadIntFromBms1Stream(IBms1Reader reader)
        {
            return (object)reader.ReadBlock<int>(reader.ReadInt32);
        }

        public static void WriteIntToBms1Stream(object msg, IBms1Writer writer)
        {
            writer.WriteBlock(() => writer.WriteInt32((int)msg));
        }
    }
}
