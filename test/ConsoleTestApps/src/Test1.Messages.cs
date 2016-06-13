
// Copyright (c) https://github.com/steforster/Remact.Net

using Remact.Net.Bms1Serializer;

namespace Test1.Contracts
{
    public class Test1CommandMessage
    {
        public string Command = string.Empty;


        // Constructor
        public Test1CommandMessage(string cmd)
        {
            Command = cmd;
        }


        #region BMS1 serializer

        public static Test1CommandMessage ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() => new Test1CommandMessage(reader.ReadString()));
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(0, () => 
                {
                    var dto = (Test1CommandMessage)obj;
                    writer.WriteString(dto.Command);
                });
        }

        #endregion
    }
}
