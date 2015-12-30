
// Copyright (c) https://github.com/steforster/Remact.Net

using System;

namespace Remact.Net.Plugin.Json.Msgpack.Alchemy
{
    /// <summary>
    /// Entry point for the dynamic loadable assembly.
    /// The default constructor of this class plugs JsonProtocolConfig as implementation of <see cref="Remact.Net.RemactConfigDefault"/>.
    /// </summary>
    public sealed class EntryPoint : IDisposable
    {
        /// <summary>
        /// Default constructor is called when assembly is loaded and initialized from <see cref="Remact.Net.RemactConfigDefault.LoadPluginAssembly"/>.
        /// </summary>
        public EntryPoint()
        {
            Remact.Net.RemactConfigDefault.Instance = new JsonProtocolConfig();
        }

        /// <summary>
        /// Releases all resource used by the <see cref="Remact.Net.Plugin.Json.Msgpack.Alchemy.EntryPoint"/> object.
        /// </summary>
        public void Dispose()
        {
        }
    }
}