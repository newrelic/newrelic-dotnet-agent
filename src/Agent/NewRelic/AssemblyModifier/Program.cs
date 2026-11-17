// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Mono.Cecil;
using Mono.Cecil.Cil;

const string KeyOption = "--key=";
const string NormalizeOnlyOption = "--normalize-only";

var positionalArgs = args.Where(a => !a.StartsWith("--")).ToArray();

if (positionalArgs.Length == 0)
{
    Console.WriteLine($"Usage: <program> <assembly-path> [<search-directory>] [{KeyOption}<strong-name-key-file>] [{NormalizeOnlyOption}]");
    return 1;
}

string assemblyPath = positionalArgs[0];
string? keyPath = args.FirstOrDefault(a => a.StartsWith(KeyOption))?[KeyOption.Length..];
bool normalizeOnly = args.Contains(NormalizeOnlyOption);
var logger = GetLogger(assemblyPath);

if (!File.Exists(assemblyPath))
{
    logger.Log($"Error: Assembly not found at '{assemblyPath}'");
    return 1;
}

try
{
    var resolver = new DefaultAssemblyResolver();

    if (positionalArgs.Length > 1 && Directory.Exists(positionalArgs[1]))
    {
        resolver.AddSearchDirectory(positionalArgs[1]);
    }

    var parameters = new ReaderParameters
    {
        AssemblyResolver = resolver,
        ReadWrite = true,
        ReadSymbols = true,
        ThrowIfSymbolsAreNotMatching = false,
        SymbolReaderProvider = new DefaultSymbolReaderProvider(throwIfNoSymbol: false)
    };

    using ModuleDefinition module = ModuleDefinition.ReadModule(assemblyPath, parameters);

    var hasEmbeddedPdb = module.GetDebugHeader().Entries
        .Any(e => e.Directory.Type == ImageDebugType.EmbeddedPortablePdb);

    IEnumerable<IModificationJob> jobs = normalizeOnly ? [] : [new InternalizeMetricsEventSourceName(logger)];

    foreach (var job in jobs)
    {
        if (!job.Run(module))
        {
            logger.Log("Modification Job {0} failed.", job.GetType().Name);
            logger.Log("Not saving changes to the assembly.");
            return 2;
        }
    }

    // The merged assembly is always rewritten so that its identity depends only on its content: ILRepack stamps a random MVID,
    // a random PE timestamp and a debug path through a randomly named temporary directory into its output.
    var writerParameters = new WriterParameters { DeterministicMvid = true };

    if (keyPath != null)
    {
        // Cecil does not re-sign on its own, which would leave the signature ILRepack produced zeroed out.
        writerParameters.StrongNameKeyBlob = File.ReadAllBytes(keyPath);
    }

    writerParameters.WriteSymbols = true;

    if (module.HasSymbols && hasEmbeddedPdb)
    {
        writerParameters.SymbolWriterProvider = new EmbeddedPortablePdbWriterProvider();
    }
    else
    {
        // Any other symbol format routes Mono.Cecil to NativePdbWriter, which faults intermittently, so drop symbols instead.
        // This also drops the debug directory, which would otherwise carry the random ILRepack temp path and PDB id.
        writerParameters.SymbolWriterProvider = new DropSymbolsWriterProvider();
    }

    module.Write(writerParameters);
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

public class DropSymbolsWriterProvider : ISymbolWriterProvider
{
    public ISymbolWriter GetSymbolWriter(ModuleDefinition module, string fileName) => new DropSymbolsWriter();

    public ISymbolWriter GetSymbolWriter(ModuleDefinition module, Stream symbolStream) => new DropSymbolsWriter();
}

public class DropSymbolsWriter : ISymbolWriter
{
    public ISymbolReaderProvider GetReaderProvider() => new DefaultSymbolReaderProvider(throwIfNoSymbol: false);

    public ImageDebugHeader GetDebugHeader() => new ImageDebugHeader();

    public void Write(MethodDebugInformation info)
    {
    }

    public void Write()
    {
    }

    public void Write(ICustomDebugInformationProvider provider)
    {
    }

    public void Dispose()
    {
    }
}
