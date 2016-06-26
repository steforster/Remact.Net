
// Copyright (c) https://github.com/steforster/Remact.Net

using Remact.Net.Bms1Serializer;

namespace Remact.Net.Plugin.Bms.Tcp
{
    /// <summary>
    /// Extension methods for the ReadyMessage.
    /// </summary>
    public static class ReadyMessageExtension
    {
        public const int Bms1BlockTypeId = 3;

        public static int GetBms1BlockTypeId(this ReadyMessage msg)
        {
            return Bms1BlockTypeId;
        }

        public static ReadyMessage ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock<ReadyMessage>(() => new ReadyMessage());
        }

        public static void WriteToBms1Stream(object msg, IBms1Writer writer)
        {
            writer.WriteBlock(Bms1BlockTypeId, () => { });
        }
    }
}
