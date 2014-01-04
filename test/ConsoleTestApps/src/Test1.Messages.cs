using System;
using System.Collections.Generic;
using Remact.Net;              // Copyright (c) 2014, <http://github.com/steforster/Remact.Net>

namespace Test1.Messages
{
  public class Test1CommandMessage
  {
    public string Command = string.Empty;
    

    // Constructor
    public Test1CommandMessage (string cmd)
    {
        Command = cmd;
    }
  }
}
