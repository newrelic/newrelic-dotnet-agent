using System;
using System.IO;
using System.Linq;
using System.Reflection;


namespace NewRelic.TypeInstantiation.UnitTests
{
	public static class AssemblyResolverHolder
	{
		public static Assembly AssemblyResolver(Object sender, ResolveEventArgs args)
		{
#if NCRUNCH
			var shortAssemblyName = GetShortAssemblyName(args.Name);
			return TryGetAssemblyFromNcrunch(shortAssemblyName);
#else
			return null;
#endif
		}

		private static Assembly TryGetAssemblyFromNcrunch(String shortAssemblyName)
		{
			return GetAllAssemblyLocations()
				.Where(location => String.Equals(Path.GetFileNameWithoutExtension(location), shortAssemblyName, StringComparison.InvariantCultureIgnoreCase))
				.Select(location => Assembly.LoadFrom(location))
				.FirstOrDefault();
		}

		public static string[] GetAllAssemblyLocations()
		{
			var dependencies = Environment.GetEnvironmentVariable("NCrunch.AllAssemblyLocations");
			if (dependencies == null)
				return null;

			return dependencies.Split(';');
		}

		private static string GetShortAssemblyName(String assemblyName)
		{
			if (assemblyName.Contains(","))
				return assemblyName.Substring(0, assemblyName.IndexOf(','));

			return assemblyName;
		}
	}
}
