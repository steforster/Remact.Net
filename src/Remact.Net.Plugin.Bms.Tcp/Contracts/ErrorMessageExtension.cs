
// Copyright (c) https://github.com/steforster/Remact.Net

using Remact.Net.Bms1Serializer;

namespace Remact.Net.Plugin.Bms.Tcp
{
    /// <summary>
    /// Extension methods for the ErrorMessage.
    /// </summary>
    public static class ErrorMessageExtension
    {
        public const int Bms1BlockTypeId = 2;

        public static int GetBms1BlockTypeId(this ErrorMessage msg)
        {
            return Bms1BlockTypeId;
        }

        public static ErrorMessage ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock<ErrorMessage>((obj) => 
                {
                    var msg = (ErrorMessage)obj;
                    var error = reader.ReadInt32();
                    if (error <= 0 || error >= (int)ErrorCode.Last)
                    { 
                        msg.ErrorCode = ErrorCode.Undef; 
                    }
                    else if (error > (int)ErrorCode.LastAppCode && error < (int)ErrorCode.NotImplementedOnService)
                    { 
                        msg.ErrorCode = ErrorCode.Undef; 
                    }
                    else 
                    { 
                        msg.ErrorCode = (ErrorCode)error; 
                    }

                    msg.Message = reader.ReadString();
                    msg.InnerMessage = reader.ReadString();
                    msg.StackTrace = reader.ReadString();
                    return msg;
                });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(Bms1BlockTypeId, () => 
                {
                    var msg = (ErrorMessage)obj;
                    writer.WriteInt32((int)msg.ErrorCode);
                    writer.WriteString(msg.Message);
                    writer.WriteString(msg.InnerMessage);
                    writer.WriteString(msg.StackTrace);
                });
        }
    }
}
