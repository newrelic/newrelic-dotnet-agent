using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace NuGet_Package_Generator
{
	public class Program
	{
		static void Main(String[] commandLineArguments)
		{
			var configuration = Configuration.Create(commandLineArguments);
			var paths = new Paths(configuration);
			
			CreateNuGetPackage(paths);
		}

		private static void CreateNuGetPackage(Paths paths)
		{
			CreateNuSpecFile(paths);
			PackNuSpecFile(paths);
		}

		private static void CreateNuSpecFile(Paths paths)
		{
			var thisAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

			var settings = new XmlWriterSettings
			{
				Indent = true,
				IndentChars = "\t",
				WriteEndDocumentOnClose = true,
				CloseOutput = true,
				Encoding = Encoding.UTF8,
			};

			using (var xml = XmlWriter.Create(paths.NuspecFilePath, settings))
			{
				xml.WriteStartDocument();
				xml.WriteStartElement("package");

				xml.WriteStartElement("metadata");
				xml.WriteElementString("id", "NewRelic.Profiler");
				xml.WriteElementString("version", thisAssemblyVersion.ToString());
				xml.WriteElementString("title", "NewRelic.Profiler");
				xml.WriteElementString("description", "New Relic .NET Agent Profiler");
				xml.WriteElementString("authors", "New Relic");
				xml.WriteElementString("owners", "New Relic");
				xml.WriteElementString("requireLicenseAcceptance", "false");

				xml.WriteEndElement(); // metadata

				xml.WriteStartElement("files");

				var files = new[]
				{
					new { bitness = "x86", extension = "dll" },
					new { bitness = "x86", extension = "pdb" },
					new { bitness = "x64", extension = "dll" },
					new { bitness = "x64", extension = "pdb" },
				};

				foreach (var file in files)
				{
					var sourcePath = Path.Combine("Profiler", "bin", file.bitness, "Release", "NewRelic.Profiler." + file.extension).Replace('\\', '/');
					var destinationPath = Path.Combine("tools", file.bitness, "NewRelic.Profiler." + file.extension).Replace('\\', '/');

					xml.WriteStartElement("file");
					xml.WriteAttributeString("src", sourcePath);
					xml.WriteAttributeString("target", destinationPath);
					xml.WriteEndElement();
				}

				var soExists = File.Exists("libNewRelicProfiler.so");
				if (soExists)
				{
					Console.WriteLine("+ Adding libNewRelicProfiler.so to nuspec");

					xml.WriteStartElement("file");
					xml.WriteAttributeString("src", "libNewRelicProfiler.so");
					xml.WriteAttributeString("target", "tools/x64/linux/libNewRelicProfiler.so");
					xml.WriteEndElement();
				}
				else
				{
					Console.WriteLine("- Did not find ./libNewRelicProfiler.so");
				}

				xml.WriteEndElement(); // files

				xml.WriteEndElement(); // package
				xml.WriteEndDocument();
				xml.Flush();
			}
		}

		private static void PackNuSpecFile(Paths paths)
		{
			var arguments = String.Format(@"pack ""{0}"" -OutputDirectory ""{1}"" -BasePath ""{2}""", paths.NuspecFilePath.TrimEnd('/', '\\'), paths.OutputDirectoryPath.TrimEnd('/', '\\'), paths.SolutionDirectoryPath.TrimEnd('/', '\\'));
			Execute(paths.NugetFilePath, arguments, paths.SolutionDirectoryPath);
		}

		private static void Execute(String command, String arguments, String workingDirectory)
		{
			if (command == null)
				throw new ArgumentNullException("command");
			if (arguments == null)
				throw new ArgumentNullException("arguments");

			Console.WriteLine("Executing: {0} {1}", command, arguments);

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = command,
					Arguments = arguments,
					UseShellExecute = false,
				},
				EnableRaisingEvents = true
			};
			process.Start();
			process.WaitForExit();
			var exitCode = process.ExitCode;
			process.Close();
			if (exitCode != 0)
			{
				throw new Exception(command + " execution failed.");
			}
		}

		private class Paths
		{
			public readonly String SolutionDirectoryPath;
			public readonly String OutputDirectoryPath;
			public readonly String NuspecFilePath;
			public readonly String NugetFilePath;

			public Paths(Configuration configuration)
			{
				SolutionDirectoryPath = configuration.SolutionDirectoryPath;
				OutputDirectoryPath = configuration.OutputDirectoryPath;
				NuspecFilePath = Path.Combine(configuration.OutputDirectoryPath, "NewRelic.Profiler.nuspec");

				var nugetCommandLineFolder = configuration.UseModernNugetFolders ? Path.Combine("NuGet.CommandLine", "2.8.6") : "NuGet.CommandLine.2.8.6";
				NugetFilePath = Path.Combine(configuration.NugetPackageDir ?? configuration.SolutionDirectoryPath, "packages", nugetCommandLineFolder, "tools", "nuget.exe");
			}
		}

		private class Configuration
		{
			[CommandLine.Option("solution", Required = true, HelpText = "$(SolutionDir)")]
			public string SolutionDirectoryPath { get; set; }

			[CommandLine.Option("output", Required = true, HelpText = "Path where package and nuspec file will reside on completion.")]
			public string OutputDirectoryPath { get; set; }

			[CommandLine.Option("nugetPackageDir", Required = false, HelpText = "$(NuGetPackageRoot)")]
			public string NugetPackageDir { get; set; }

			[CommandLine.Option("useModernNugetFolders", DefaultValue = false, Required = false, HelpText = "Indicates if should use the {PackageName}/{Version} folder structure instead of the {PackageName}.{Version}/ structure.")]
			public bool UseModernNugetFolders { get; set; }

			public static Configuration Create(string[] commandLineArguments)
			{
				var defaultParser = CommandLine.Parser.Default;
				if (defaultParser == null)
					throw new NullReferenceException("defaultParser");

				var configuration = new Configuration();
				defaultParser.ParseArgumentsStrict(commandLineArguments, configuration);

				return configuration;
			}
		}
	}
}
