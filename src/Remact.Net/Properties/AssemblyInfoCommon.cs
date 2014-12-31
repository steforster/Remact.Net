
// Copyright (c) 2014-2015, github.com/steforster/Remact.Net

using System.Reflection;

// General Information about assemblies of Remact.Net:
[assembly: AssemblyDescription("Remote Actors for .NET, Mono, Android and JavaScript")] // not displayed by windows explorer 
#if (DEBUG)
    [assembly: AssemblyConfiguration ("Debug")] // not displayed by windows explorer
    [assembly: AssemblyProduct       ("Remact.Net (debug build)")]
#else
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyProduct("Remact.Net")]
#endif
[assembly: AssemblyCompany("")]
[assembly: AssemblyCopyright("© 2015 github.com/steforster/Remact.Net")]
[assembly: AssemblyTrademark("Open source MIT license")]
[assembly: AssemblyCulture("")]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.
[assembly: AssemblyVersion("0.2.0.0")]


