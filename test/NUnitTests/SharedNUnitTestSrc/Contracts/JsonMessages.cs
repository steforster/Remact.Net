
// Copyright (c) https://github.com/steforster/Remact.Net

namespace RemactNUnitTest
{
#if (JSON)

    public class Request
    {
        public string Text;
        
        public override string ToString()
        {
            return GetType().Name + ": " + Text;
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
    }

    // This is the interface to the polymorph member
    public interface IInnerTestMessage
    {
        int Id {get; set; }
    }

    // This is an implementation of the polymorph member
    public class InnerTestMessage : IInnerTestMessage
    {
        public int Id {get; set; }
        public string Name;
    }

#endif
}
