
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net;
using System.Reflection;

namespace Remact.TestUtilities
{
    class PluginSelector
    {
        /// <summary>
        /// Selects a plugin for message transport. The plugin will be installed as <see cref="RemactConfigDefault.Instance"/>.
        /// The plugin name is not case sensitive. The string has to start with one of the following tags:
        /// <para>- 'JSON': load "Remact.Net.Plugin.Json.Msgpack.Alchemy.dll"</para>
        /// <para>- 'BMS' : load "Remact.Net.Plugin.Bms.Tcp.dll"</para>
        /// An InvalidOperationException is thrown if no matching plugin is found.
        /// </summary>
        /// <param name="pluginName">Plugin names must start with: 'JSON' or 'BMS'</param>
        public static void LoadRemactConfigDefault(string pluginName)
        {
            string name = pluginName.ToUpper();
            if (name.StartsWith("JSON"))
            {
                LoadPluginDll(RemactConfigDefault.JsonProtocolPluginName);
            }
            else if (name.StartsWith("BMS"))
            {
                LoadPluginDll(RemactConfigDefault.DefaultProtocolPluginName);
                //var conf = Remact.Net.Plugin.Bms.Tcp.BmsProtocolConfig.Instance;
                //conf.AddKnownMessageType(Request.ReadFromBms1Stream, Request.WriteToBms1Stream);
                // TODO: copy Newtonsoft.Json (Replacement)?
            }
            else
            {
                throw new InvalidOperationException("unsupported plugin: " + pluginName + ". Allowed is 'BMS' and JSON'");
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
            var loadedAssembly = Assembly.GetAssembly(disposable.GetType());
            RaLog.Info("RemactConfigDefault.LoadPluginAssembly and its dependencies", loadedAssembly.CodeBase);
        }
    }
}
