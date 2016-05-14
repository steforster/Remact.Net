
// Copyright (c) https://github.com/steforster/Remact.Net

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
    }
}
