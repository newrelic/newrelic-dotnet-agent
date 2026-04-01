// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Mono.Cecil;

if (args.Length == 0)
{
    Console.WriteLine("Usage: <program> <assembly-path> <optional-search-directory>");
    return 1;
}

string assemblyPath = args[0];
var logger = GetLogger(assemblyPath);

if (!File.Exists(assemblyPath))
{
    logger.Log($"Error: Assembly not found at '{assemblyPath}'");
    return 1;
}

try
{
    var resolver = new DefaultAssemblyResolver();

    if (args.Length > 1 && Directory.Exists(args[1]))
    {
        resolver.AddSearchDirectory(args[1]);
    }

    var parameters = new ReaderParameters
    {
        AssemblyResolver = resolver,
        ReadWrite = true
    };

    using ModuleDefinition module = ModuleDefinition.ReadModule(assemblyPath, parameters);

    IEnumerable<IModificationJob> jobs = [new InternalizeMetricsEventSourceName(logger)];

    foreach (var job in jobs)
    {
        if (!job.Run(module))
        {
            logger.Log("Modification Job {0} failed.", job.GetType().Name);
            logger.Log("Not saving changes to the assembly.");
            return 2;
        }
    }

    // Save the modified assembly
    module.Write();
}
catch (Exception ex)
{
    logger.Log($"Error reading assembly: {ex.Message}");
    return 3;
}

logger.Log("Completed all assembly modifications.");
return 0;

static Logger GetLogger(string assemblyPath)
{
    string context = "unknown: ";

    var assemblyName = Path.GetFileName(assemblyPath);
    var directoryName = Path.GetFileName(Path.GetDirectoryName(assemblyPath));

    if (string.IsNullOrWhiteSpace(assemblyName) || string.IsNullOrWhiteSpace(directoryName))
        return new Logger(context);

    return new Logger($"{directoryName}\\{assemblyName}: ");
}

public class Logger(string context)
{
    public void Log(string message, params scoped ReadOnlySpan<object?> arg)
    {
        Console.WriteLine(context + message, arg);
    }
}
