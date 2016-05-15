
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net;

namespace Remact.TestUtilities
{
    class PluginSelector
    {
        public static void LoadRemactConfigDefault(string pluginArgument)
        {
            string arg = pluginArgument.ToUpper();
            if (arg == "BMS")
            {
                LoadPluginDll(RemactConfigDefault.DefaultProtocolPluginName);
                //var conf = Remact.Net.Plugin.Bms.Tcp.BmsProtocolConfig.Instance;
                //conf.AddKnownMessageType(Request.ReadFromBms1Stream, Request.WriteToBms1Stream);
                // TODO: copy Newtonsoft.Json (Replacement)?
            }
            else if (arg == "JSON")
            {
                LoadPluginDll(RemactConfigDefault.JsonProtocolPluginName);
            }
            else
            {
                throw new InvalidOperationException("unsupported plugin: " + pluginArgument + ". Allowed is 'BMS' and JSON'");
            }
        }

        private static void LoadPluginDll(string fileName)
        {
            #if(DEBUG)
                var path = @"../../../../../src/bin/Debug/" + fileName;
            #else
                var path = @"../../../../../src/bin/Release/" + fileName;
            #endif

            var disposable = RemactConfigDefault.LoadPluginAssembly(path);
            if (disposable == null)
            {
                throw new InvalidOperationException("cannot dynamically load dll: " + path);
            }
            RaLog.Info("RemactConfigDefault.LoadPluginAssembly", fileName);
        }
    }
}
