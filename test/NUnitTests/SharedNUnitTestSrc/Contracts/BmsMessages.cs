
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
            return reader.ReadBlock<Request>((dto) => new Request());
        }

        public static void WriteToBms1Stream(object msg, IBms1Writer writer)
        {
            writer.WriteBlock(0, () => { });
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
            return reader.ReadBlock<Response>((dto) => new Response());
        }

        public static void WriteToBms1Stream(object msg, IBms1Writer writer)
        {
            writer.WriteBlock(0, () => { });
        }
    }

    public class ResponseA1 : Response
    {
    }

    public class ResponseA2 : ResponseA1
    {
    }

#endif
}
