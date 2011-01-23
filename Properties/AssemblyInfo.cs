using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("DllExport")]
[assembly: AssemblyDescription("Courtesy of the Open Source Technology Center: http://port25.technet.com")] // 'comments'
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Microsoft")]
[assembly: AssemblyProduct("DllExport")]
[assembly: AssemblyCopyright("Copyright © Microsoft 2009, Garrett Serack © 2010")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
#if SIGN_ASSEMBLY
// disable warning about using /keyfile instead of AssemblyKeyFile
#pragma warning disable 1699
[assembly: AssemblyKeyFileAttribute(@"..\coapp-signing\coapp-release.snk")]
#pragma warning restore 1699
#endif

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("e17d97e2-0732-4e5c-a189-19109759ec46")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]

[assembly: AssemblyVersion("2.0.0.*")]
// by removing the following it defaults to the generated number above.
// [assembly: AssemblyFileVersion("1.0.0.0")]