﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2010  Garrett Serack. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) 2009 Microsoft Corporation -- All rights reserved
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Courtesy of the Open Source Techology Center: http://port25.technet.com
// -----------------------------------------------------------------------

namespace CoApp.DllExport {
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using Toolkit.Extensions;
    using Toolkit.Utility;

    public class DllExportUtility {
        private static string help =
            @"
DllExport for .NET 4.0
----------------------

DllExport will create a DLL that exposes standard C style function calls
which are transparently thunked to static methods in a .NET assembly.

To export a static method in a .NET class, mark each method with an 
attribute called DllExportAttribute (see help for an example attribute class)

Usage:
   DllExport [options] Assembly.dll -- This creates the native thunks in an 
                                       assembly called $TargetAssembly.dll

                                       This is good for development--it 
                                       preserves the original assembly and 
                                       allows for easy debugging.
   Options:
        --merge                     -- this will generate the thunking 
                                       functions and merge them into the 
                                       target assembly. 

                                       This is good for when you want to 
                                       produce a release build.

                                       ** Warning **
                                       This overwrites the target assembly.

        --keep-temp-files           -- leaves the working files in the 
                                       current directory. 

        --rescan-tools              -- causes the tool to search for its
                                       dependent tools (ilasm and ildasm)
                                       instead of using the cached values.

        --no-logo                   -- suppresses informational messages.

   More Help:

        DllExport --help            -- Displays this help.
 
        DllExport --sampleClass     -- Displays the DllExportAttribute 
                                       source code that you should include
                                       in your assembly.

        DllExport --sampleUsage     -- Displays some examples of using the 
                                       DllExport attribute.";

        internal static List<ExportableMember> members = new List<ExportableMember>();
        private static bool keepTempFiles;
        private static bool quiet;
        private static bool debug;

        internal class ExportableMember {
            internal MemberInfo member;
            internal string exportedName;
            internal CallingConvention callingConvention;
        }

        internal static Type Modopt(CallingConvention cc) {
            switch(cc) {
                case CallingConvention.Cdecl:
                    return typeof(System.Runtime.CompilerServices.CallConvCdecl);
                case CallingConvention.FastCall:
                    return typeof(System.Runtime.CompilerServices.CallConvFastcall);
                case CallingConvention.Winapi:
                case CallingConvention.StdCall:
                    return typeof(System.Runtime.CompilerServices.CallConvStdcall);
                case CallingConvention.ThisCall:
                    return typeof(System.Runtime.CompilerServices.CallConvThiscall);
            }
            return null;
        }

        static bool FindExport(MemberInfo mi, object obj) {
            foreach(object attrib in mi.GetCustomAttributes(false))
                if(attrib.GetType().Name.Equals("DllExportAttribute")) {
                    try {
                        members.Add(new ExportableMember { member = mi, exportedName = attrib.GetType().GetProperty("ExportedName").GetValue(attrib, null).ToString(), callingConvention = (CallingConvention)attrib.GetType().GetProperty("CallingConvention").GetValue(attrib, null) });
                    }
                    catch(Exception) {
                        Console.Error.WriteLine("Warning: Found DllExport on Member {0}, but unable to get ExportedName or CallingConvention property.");
                        return false;
                    }
                    return true;
                }
            return false;
        }

        static void Delete(string filename) {
            if(keepTempFiles) {
                if(!quiet)
                    Console.WriteLine("   Warning: leaving temporary file [{0}]", filename);
            }
            else
                File.Delete(filename);
        }

        static void Main(string[] args) {
            new DllExportUtility().main(args);
        }

        int main(string[] args) {
            int firstArg = 0;
            bool mergeAssemblies = false;

            while(firstArg < args.Length && args[firstArg].StartsWith("--"))
                switch(args[firstArg++].ToLower()) {
                    case "--merge":
                        mergeAssemblies = true;
                        break;

                    case "--keep-temp-files":
                        keepTempFiles = true;
                        break;

                    case "--rescan-tools":
                        ProgramFinder.IgnoreCache = true;
                        break;

                    case "--no-logo":
                        quiet = true;
                        break;

                    case "--debug":
                        debug = true;
                        break;

                    case "--sampleusage":
                        SampleUsage();
                        return 0;

                    case "--sampleclass":
                        SampleClass();
                        return 0;

                    case "--help":
                        Help();
                        return 0;

                    default:
                        Logo();
                        return Fail("Error: unrecognized switch:{0}", args[firstArg-1]);
                }
            

            if(firstArg >= args.Length) {
                Help();
                return 0;
            }

            if(!quiet)
                Logo();

            string targetAssembly = Path.GetFullPath(args[firstArg]);
            if(!File.Exists(targetAssembly)) {
                return Fail("Error: the specified target assembly \r\n   [{0}]\r\ndoes not exist.", targetAssembly);
            }

            var ILDasm = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("ildasm.exe", "4.0.30319.1"));
            var ILAsm = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("ilasm.exe", "4.0.30319.1"));

            Assembly assembly;
            try {
                byte[] rawAssembly = File.ReadAllBytes(targetAssembly);
                assembly = Assembly.Load(rawAssembly);

            }
            catch(Exception) {
                return Fail("Error: unable to load the specified target assembly \r\n   [{0}].\r\n\r\nMost likely, it has already been modified--and can't be modified again.", targetAssembly);
            }

            foreach(Type type in assembly.GetTypes())
                type.FindMembers(MemberTypes.All, BindingFlags.Public|BindingFlags.Static, FindExport, null);

            if(members.Count == 0) {
                return Fail("No members found with DllExport attributes in the target assembly \r\n   [{0}]", targetAssembly);

            }

            var assemblyName = new AssemblyName("$"+assembly.GetName());
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Save);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");

            var index =0;
            var modopts = new Type[1];

            foreach(ExportableMember exportableMember in members) {
                var methodInfo = exportableMember.member as MethodInfo;
                if(methodInfo != null) {
                    ParameterInfo[] pinfo = methodInfo.GetParameters();
                    var parameterTypes = new Type[pinfo.Length];
                    for(int i=0;i<pinfo.Length;i++)
                        parameterTypes[i] = pinfo[i].ParameterType;

                    modopts[0] = Modopt(exportableMember.callingConvention);

                    MethodBuilder methodBuilder = moduleBuilder.DefineGlobalMethod(methodInfo.Name, MethodAttributes.Static|MethodAttributes.Public, CallingConventions.Standard, methodInfo.ReturnType, null, modopts, parameterTypes, null, null);
                    ILGenerator ilGenerator = methodBuilder.GetILGenerator();

                    // this is to pull the ol' swicheroo later.
                    ilGenerator.Emit(OpCodes.Ldstr, string.Format(".export [{0}] as {1}", index++, exportableMember.exportedName));

                    int n = 0;
                    foreach(ParameterInfo parameterInfo in pinfo) {
                        switch(n) {
                            case 0:
                                ilGenerator.Emit(OpCodes.Ldarg_0);
                                break;
                            case 1:
                                ilGenerator.Emit(OpCodes.Ldarg_1);
                                break;
                            case 2:
                                ilGenerator.Emit(OpCodes.Ldarg_2);
                                break;
                            case 3:
                                ilGenerator.Emit(OpCodes.Ldarg_3);
                                break;
                            default:
                                ilGenerator.Emit(OpCodes.Ldarg_S, (byte)n);
                                break;
                        }
                        n++;
                        methodBuilder.DefineParameter(n, parameterInfo.Attributes, parameterInfo.Name); //1-based... *sigh*
                    }
                    ilGenerator.EmitCall(OpCodes.Call, methodInfo, null);
                    ilGenerator.Emit(OpCodes.Ret);
                }
            }

            moduleBuilder.CreateGlobalFunctions();

            var outputFilename = assemblyName.Name + ".dll";
            var temporaryIlFilename = assemblyName.Name + ".il";
            assemblyBuilder.Save(outputFilename);

            int rc = ILDasm.Exec(@"/text /nobar /typelist ""{0}""", outputFilename);
            Delete(outputFilename); // eliminate it regardless of result.
            if(0 != rc) {
                return Fail("Error: unable to disassemble the temporary assembly\r\n   [{0}]\r\nMore Information:\r\n{1}", outputFilename, ILDasm.StandardOut);
            }
            string ilSource = ILDasm.StandardOut;

            ilSource = System.Text.RegularExpressions.Regex.Replace(ilSource, @"IL_0000:.*ldstr.*\""(?<x>.*)\""", "${x}");

            if(mergeAssemblies) {
                int start = ilSource.IndexOf("\r\n.method");
                int end = ilSource.LastIndexOf("// end of global method");

                ilSource = ilSource.Substring(start, end - start);

                // arg! needed this to make sure the resources came out. grrr
                rc = ILDasm.Exec(@"/nobar /typelist ""{0}"" /out=""{1}""", targetAssembly, temporaryIlFilename);

                rc = ILDasm.Exec(@"/nobar /text /typelist ""{0}""", targetAssembly);
                if(0 != rc) {
                    return Fail("Error: unable to disassemble the target assembly\r\n   [{0}]\r\nMore Information:\r\n{1}", outputFilename, ILDasm.StandardOut);
                }
                string ilTargetSource = ILDasm.StandardOut;

                start = Math.Min(ilTargetSource.IndexOf(".method"), ilTargetSource.IndexOf(".class"));
                string ilFinalSource = ilTargetSource.Substring(0, start) + ilSource + ilTargetSource.Substring(start);

                File.WriteAllText(temporaryIlFilename, ilFinalSource);
                rc = ILAsm.Exec(@"/dll {2} /output={0} ""{1}""", outputFilename, temporaryIlFilename, debug ? "/debug" : "");
                Delete(temporaryIlFilename); // delete temp file regardless of result.
                if(0 != rc) {
                    return Fail("Error: unable to assemble the merged assembly\r\n   [{0}]\r\n   [{1}]\r\nMore Information:\r\n{2}", outputFilename, temporaryIlFilename, ILAsm.StandardError);
                }

                File.Delete(targetAssembly + ".bak");

                File.Move(targetAssembly, targetAssembly + ".bak");
                File.Move(outputFilename, targetAssembly);

                if(!quiet)
                    Console.WriteLine("Merged Export functions into Assembly: {0}", targetAssembly);

            }
            else {
                File.WriteAllText(temporaryIlFilename, ilSource);
                rc = ILAsm.Exec(@"/dll {2} /output={0} ""{1}""", outputFilename, temporaryIlFilename, debug ? "/debug" : "");
                if(!debug)
                    Delete(temporaryIlFilename);

                if(0 != rc) {
                    return Fail("Error: unable to assemble the output assembly\r\n   [{0}]\r\n   [{1}]\r\nMore Information:\r\n{2}", outputFilename, temporaryIlFilename, ILAsm.StandardError);
                }

                if(!quiet)
                    Console.WriteLine("Created Export Assembly: {0}", outputFilename);
            }
            return 0;
        }



        public static void SampleClass() {
            using(new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black))
                Console.WriteLine(@"
using System;
using System.Runtime.InteropServices;

/// <summary>
/// This class is used by the DllExport utility to generate a C-style
/// native binding for any static methods in a .NET assembly.
/// 
/// Namespace is not important--feel free to set the namespace to anything
/// convenient for your project.
/// -----------------------------------------------------------------------
/// (c) 2009 Microsoft Corporation -- All rights reserved
/// This code is licensed under the MS-PL
/// http://www.opensource.org/licenses/ms-pl.html
/// Courtesy of the Open Source Techology Center: http://port25.technet.com
/// -----------------------------------------------------------------------
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DllExportAttribute: Attribute {
    public DllExportAttribute(string exportName) 
        : this(CallingConvention.StdCall,exportName) {
    }

    public DllExportAttribute(CallingConvention convention, string name) {
        ExportedName = name;
        this.CallingConvention = convention;
    }

    public string ExportedName { get; set; }
    public CallingConvention CallingConvention { get; set; }
}");

        }

        public static void SampleUsage() {
            using(new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black))
                Console.WriteLine(@"
DllExport Usage
----------------

To use the DllExport Attribute in your code, include the class in your 
project. Namespace is not important.

On any method you wish to export as a C-style function, simply use the 
attribute on any static method in a class:

...

// example 1
// note the exported function name doesn't have to match the method name
[DllExport(""myFunction"")]     
public static int MyFunction( int age, string name ){
   // ....
}

// example 2
// On this example, we've marked set the calling convention to CDECL
[DllExport( CallingConvention.Cdecl, ""myNextFunction"")]     
public static int MyFunctionTwo( float someParameter, string name ){
   // ....
}

");
        }

        #region fail/help/logo
        public static int Fail(string text, params object[] par) {
            using(new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black))
                Console.WriteLine("Error:{0}", text.format(par));
            return 1;
        }

        static int Help() {
            using(new ConsoleColors(ConsoleColor.White, ConsoleColor.Black))
                help.Print();
            return 0;
        }

        void Logo() {
            using(new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black))
                this.Assembly().Logo().Print();
            this.Assembly().SetLogo("");
        }
        #endregion
    }
}
