using System;
using System.IO;
using System.Reflection;

namespace TwitterIrcGatewayCLIBootstrap
{
    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            Assembly asmTigCli = Assembly.Load("TwitterIrcGatewayCLI");
            asmTigCli.EntryPoint.Invoke(null, new object[]{ args });
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var asmName = (args.Name.IndexOf(',') > -1) ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name;
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), Path.Combine("MonoBundleGAC", asmName) + ".dll");
            if (File.Exists(path))
            {
                return Assembly.LoadFile(path);
            }

            return null;
        }
    }
}
