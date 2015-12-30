
// Copyright (c) https://github.com/steforster/Remact.Net

namespace DemoUnitTest
{
#if (!BMS1)

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

#endif
}
