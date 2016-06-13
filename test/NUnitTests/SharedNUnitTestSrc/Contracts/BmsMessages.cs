
// Copyright (c) https://github.com/steforster/Remact.Net

namespace RemactNUnitTest
{
#if (BMS)
    using Remact.Net.Bms1Serializer;

    public class Request
    {
        public string Text;
        
        public override string ToString()
        {
            return GetType().Name + ": " + Text;
        }


        public static Request ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() => new Request() {Text = reader.ReadString()});
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(0, () => 
                {
                    var dto = (Request)obj;
                    writer.WriteString(dto.Text);
                });
        }
    }

    public class RequestA1 : Request
    {
    }

    public class RequestA2 : RequestA1
    {
    }


    public class Response
    {
        public string Text;

        public override string ToString()
        {
            return GetType().Name + ": " + Text;
        }


        public static Response ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() => new Response() { Text = reader.ReadString() });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(0, () =>
                {
                    var dto = (Response)obj;
                    writer.WriteString(dto.Text);
                });
        }
    }

    public class ResponseA1 : Response
    {
    }

    public class ResponseA2 : ResponseA1
    {
    }

    // This is a test message containing a polymorph member
    public class TestMessage
    {
        public IInnerTestMessage Inner;


        public static TestMessage ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() =>
            {
                var dto = new TestMessage();
                if (reader.Internal.BlockTypeId == 1)
                {
                    dto.Inner = InnerTestMessage.ReadFromBms1Stream(reader);
                }
                return dto;
            });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(0, () =>
                {
                    var dto = (TestMessage)obj;
                    if (dto.Inner == null)
                    {
                        writer.WriteBlock(null);
                    }
                    else if (dto.Inner is InnerTestMessage)
                    {
                        InnerTestMessage.WriteToBms1Stream(dto.Inner, writer);
                    }
                });
        }
    }

    // This is the interface to the polymorph member
    public interface IInnerTestMessage
    {
        int Id { get; set; }
    }

    // This is an implementation of the polymorph member
    public class InnerTestMessage : IInnerTestMessage
    {
        public int Id { get; set; }
        public string Name;


        public static InnerTestMessage ReadFromBms1Stream(IBms1Reader reader)
        {
            return reader.ReadBlock(() => new InnerTestMessage()
                {
                    Id = reader.ReadInt32(),
                    Name = reader.ReadString(),
                });
        }

        public static void WriteToBms1Stream(object obj, IBms1Writer writer)
        {
            writer.WriteBlock(1, () => 
                {
                    var dto = (InnerTestMessage)obj;
                    writer.WriteInt32(dto.Id);
                    writer.WriteString(dto.Name);
                });
        }
    }


#endif
}
