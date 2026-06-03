// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// Handles building and writing the serverless payload. Created primarily to facilitate unit testing.
/// </summary>
public interface IServerlessModePayloadManager
{
    void WritePayload(string jsonPayload, string outputPath);
    string BuildPayload(WireData data);
}


public class ServerlessModePayloadManager : IServerlessModePayloadManager
{
    private readonly IFileWrapper _fileWrapper;
    private readonly IEnvironment _environment;
    private readonly object _writeLock = new object();
    private static IDictionary<string, object> _metadata = null;
    private static string _functionVersion;
    private static string _arn;

    public ServerlessModePayloadManager(IFileWrapper fileWrapper, IEnvironment environment)
    {
        _fileWrapper = fileWrapper;
        _environment = environment;
    }

    public void WritePayload(string payloadJson, string path)
    {
        bool success = false;

        // Make sure we aren't trying to write two payloads at the same time
        lock (_writeLock)
        {
            try
            {
                var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
                if (_fileWrapper.Exists(path))
                {
                    using (var fs = _fileWrapper.CreateOpenOverwrite(path))
                    {
                        fs.Write(payloadBytes, 0, payloadBytes.Length);
                        fs.Flush(true);
                    }

                    success = true;
                }
                else
                {
                    Log.Warn("Unable to write serverless payload. '{0}' not found", path);
                }
            }
            catch (Exception e)
            {
                Log.Warn(e, "Failed to write serverless payload to {path}.", path);
            }
        }

        if (!success)
        {
            // Fall back to writing to stdout.
            // Write directly to file descriptor 1 (stdout) to bypass the .NET Lambda runtime's
            // structured logging wrapper (WrapperTextWriter / LogLevelLoggerWriter), which intercepts
            // Console.SetOut and prepends a timestamp\trequestId\tinfo\t prefix to every Console.WriteLine
            // call. That prefix prevents aws-log-ingestion from parsing the NR_LAMBDA_MONITORING payload.
            // Other agents (Python, Node.js, Ruby, Go, Java) write to fd 1 directly and produce clean
            // output that aws-log-ingestion can parse without additional configuration.
            Log.Debug("Writing serverless payload to stdout");
            try
            {
                using var fd1 = new System.IO.FileStream(
                    new Microsoft.Win32.SafeHandles.SafeFileHandle(new System.IntPtr(1), ownsHandle: false),
                    System.IO.FileAccess.Write, bufferSize: 4096, isAsync: false);
                var bytes = System.Text.Encoding.UTF8.GetBytes(payloadJson + System.Environment.NewLine);
                fd1.Write(bytes, 0, bytes.Length);
                fd1.Flush();
            }
            catch
            {
                // Fallback to Console.WriteLine if direct fd write is unavailable
                Console.WriteLine(payloadJson);
            }
        }
    }

    public string BuildPayload(WireData eventsToFlush)
    {
        InitializeMetadata();
        var metadata = GetMetadata();
        var basePayload = GetCompressiblePayload(eventsToFlush);

        if (Log.IsFinestEnabled)
        {
            var uncompressedPayload = new List<object> { 2, "NR_LAMBDA_MONITORING", metadata, basePayload };
            Log.Finest("Serverless payload: {0}", JsonConvert.SerializeObject(uncompressedPayload));
        }

        var compressedAndEncodedPayload = CompressAndEncode(JsonConvert.SerializeObject(basePayload));
        var payload = new List<object> { 2, "NR_LAMBDA_MONITORING", metadata, compressedAndEncodedPayload };

        return JsonConvert.SerializeObject(payload);
    }


    private Dictionary<string, object> GetCompressiblePayload(WireData eventsToFlush)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        foreach (var kvp in eventsToFlush)
        {
            if (kvp.Value.Any())
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    // gzip compress and base64 encode.
    private string CompressAndEncode(string compressiblePayload)
    {
        try
        {
            using MemoryStream output = new MemoryStream();
            using GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal);
            var data = Encoding.UTF8.GetBytes(compressiblePayload);
            gzip.Write(data, 0, data.Length);
            gzip.Flush();
            gzip.Close();
            return Convert.ToBase64String(output.ToArray());
        }
        catch (IOException e)
        {
            Log.Error(e, "Failed to compress payload");
        }
        return string.Empty;
    }

    private void InitializeMetadata()
    {
        if (_metadata == null)
        {
            _metadata = new Dictionary<string, object>()
            {
                { "protocol_version", 17 },
                { "agent_version", AgentInstallConfiguration.AgentVersion },
                { "metadata_version", 2 },
                { "agent_language", "dotnet" } // Should match "connect" string
            };
            _metadata.AddStringIfNotNullOrEmpty("execution_environment", _environment.GetEnvironmentVariable("AWS_EXECUTION_ENV"));
            _metadata.AddStringIfNotNullOrEmpty("function_version", _functionVersion);
            _metadata.AddStringIfNotNullOrEmpty("arn", _arn);
        }
    }

    // Metadata is not compressed or encoded.
    private static IDictionary<string, object> GetMetadata() => _metadata;

    public static void SetMetadata(string functionVersion, string arn)
    {
        _functionVersion = functionVersion;
        _arn = arn;
    }
}
