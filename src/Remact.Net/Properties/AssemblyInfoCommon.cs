
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about assemblies of Remact.Net:
[assembly: AssemblyDescription   ( "Remote Actors for .NET, Mono and JavaScript" )] // not displayed by windows explorer 
#if (DEBUG)
    [assembly: AssemblyConfiguration ("Debug")] // not displayed by windows explorer
    [assembly: AssemblyProduct       ("Remact.Net (debug build)")]
#else
    [assembly: AssemblyConfiguration ("Release")]
    [assembly: AssemblyProduct       ("Remact.Net")]
#endif
[assembly: AssemblyCompany       ( "" )]
[assembly: AssemblyCopyright     ( "© 2014 github.com/steforster/Remact.Net" )]
[assembly: AssemblyTrademark     ( "Open source MIT license" )]
[assembly: AssemblyCulture       ( "" )]
[assembly: AssemblyVersion       ( "0.1.0.0" )]

// Setting ComVisible to false makes the types in this assembly not visible to COM components.
[assembly: ComVisible (false)]

// The assembly may be used from other CLS languages. Therefore, it is checked for compatiblity.
[assembly: CLSCompliant(true)]


