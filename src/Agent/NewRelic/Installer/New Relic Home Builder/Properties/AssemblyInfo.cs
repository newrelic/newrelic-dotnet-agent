using System.Reflection;

[assembly: AssemblyTitle("New Relic Home Builder")]
[assembly: AssemblyDescription("A project that will populate 32-bit and 64-bit New Relic Home folders on build.")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyCompany("New Relic")]
[assembly: AssemblyProduct("New Relic .NET Agent")]
[assembly: AssemblyCopyright("Copyright © 2012")]
[assembly: AssemblyTrademark("")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Retail")]
#endif
