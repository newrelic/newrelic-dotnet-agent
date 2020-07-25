using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NewRelic.Agent.Core.Configuration;

namespace NewRelic.Agent.Core
{
    public static class AgentInitializer
    {
        public static event EventHandler OnExit = (sender, args) => { };

        static AgentInitializer()
        {
            InitializeAgent = () => CallOnce.TouchMe();
        }

        /// <summary>
        /// THIS FIELD SHOULD ONLY BE CHANGED BY UNIT TESTS.
        /// 
        /// This is the one place in our agent where we are capitulating the needs of unit tests by providing functionality that only tests should use.
        /// </summary>
        public static Action InitializeAgent { get; private set; }

        private static class CallOnce
        {
            static CallOnce()
            {
#if NETSTANDARD2_0
                // for some reason our assemblies aren't resolving.  This forces them to be resolved.
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.AssemblyResolve += new ResolveEventHandler(LoadFromSameFolder);
#endif
                // we must ensure that we hook up to ProcessExit and DomainUnload *before* log4net.  Otherwise we can't log anything during OnExit.
                AppDomain.CurrentDomain.ProcessExit += (sender, args) => OnExit(sender, args);
                AppDomain.CurrentDomain.DomainUnload += (sender, args) => OnExit(sender, args);
                LoggerBootstrapper.Initialize();

                // Force agent to be initialized
                var agent = Agent.Instance;
            }

            static Assembly LoadFromSameFolder(object sender, ResolveEventArgs args)
            {
                string folderPath = System.Environment.GetEnvironmentVariable(DefaultConfiguration.NewRelicInstallPathEnvironmentVariable); ;
                if (folderPath == null) folderPath = System.Environment.GetEnvironmentVariable(DefaultConfiguration.NewRelicHomeEnvironmentVariable); ;
                string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
                if (!File.Exists(assemblyPath)) return null;
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                return assembly;
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
            public static void TouchMe()
            {
            }
        }
    }
}
