// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System;
using System.Diagnostics;
using System.Text;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    public class ProcessRunner
    {
        private readonly ProcessStartInfo _processStartInfo;
        private Process _process;
        private bool _overwriteEnvVars;
        private bool _isStarted;
        private readonly StringBuilder _stdOutput;
        private readonly StringBuilder _stdError;

        public ProcessRunner()
        {
            _processStartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            _overwriteEnvVars = false;
            _isStarted = false;
            _stdOutput = new StringBuilder();
            _stdError = new StringBuilder();
        }

        /// <summary>
        /// Builds a Process object from the previously supplied information.
        /// 
        /// Does not start the process.
        /// </summary>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner Build()
        {
            ConsoleMFLogger.Info("Preparing to build process");
            if (_process != null)
            {
                ConsoleMFLogger.Error("ProcessRunner has already built the process.  Did you mean Start()?");
                return this;
            }

            // Build the Process, but not start it
            ConsoleMFLogger.Info("Initializing Process object");
            _process = new Process
            {
                StartInfo = _processStartInfo
            };

            _process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _stdOutput.AppendLine(e.Data);
                }
            };
            _process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _stdError.AppendLine(e.Data);
                }
            };

            ConsoleMFLogger.Info("Process was initialized successfully and should be ready to start");
            return this;
        }

        /// <summary>
        /// Starts the process if it has been built or builds and then starts the process.
        /// </summary>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner Start()
        {
            ConsoleMFLogger.Info("Preparing to start process");
            if (_process == null)
            {
                ConsoleMFLogger.Info("Process was null so attempting to build it");
                Build();
            }

            if (_isStarted)
            {
                ConsoleMFLogger.Info("Process is already started.");
                return this;
            }

            ConsoleMFLogger.Info("Starting process");
            _isStarted = _process.Start();

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            if (_isStarted)
            {
                ConsoleMFLogger.Info("Process started");
            }
            else
            {
                ConsoleMFLogger.Error("WARNING: Process did not report that it started.");
            }

            return this;
        }

        /// <summary>
        /// Stops the process using Process.Kill().
        /// </summary>
        [LibraryMethod]
        public void Stop()
        {
            ConsoleMFLogger.Info("Received request to stop the process");
            if (_process != null && _isStarted && !_process.HasExited)
            {
                ConsoleMFLogger.Info("Process was start and apprears to be running, attempting to kill it");
                _process.Kill();
                ConsoleMFLogger.Info("Process should be stopped now");
            }
        }

        /// <summary>
        /// Prints out whether or not the process has exited.
        /// </summary>
        [LibraryMethod]
        public void HasExited()
        {
            if (_process == null)
            {
                ConsoleMFLogger.Error(new NullReferenceException("Process is null."));
                return;
            }

            if (_process.HasExited)
            {
                ConsoleMFLogger.Info("Process has exited.");
            }
            else
            {
                ConsoleMFLogger.Info("Process has not exited.");
            }
        }

        /// <summary>
        /// Forces the console to wait for the process to exit.
        /// 
        /// If you need to run more than one command at a time (see W3C), do not use this.
        /// </summary>
        /// <param name="milliseconds"></param>
        [LibraryMethod]
        public void WaitForExit(int milliseconds)
        {
            if (_process == null)
            {
                ConsoleMFLogger.Error(new NullReferenceException("Process is null."));
                return;
            }

            ConsoleMFLogger.Info($"Waiting for {milliseconds} milliseconds.");
            var timedOut = _process.WaitForExit(milliseconds);
            var logMessage = timedOut ? "exited normally" : "timed out";
            ConsoleMFLogger.Info($"Finished waiting. Process {logMessage}.");
        }

        /// <summary>
        /// Get the exit code of the process if it has exited.
        /// </summary>
        [LibraryMethod]
        public void ExitCode()
        {
            if (_process == null)
            {
                ConsoleMFLogger.Error(new NullReferenceException("Process is null."));
                return;
            }

            if (_process.HasExited)
            {
                ConsoleMFLogger.Info($"LASTEXITCODE = {_process.ExitCode}");
            }
            else
            {
                ConsoleMFLogger.Info("Process is still running, cannot get exit code.");
            }
        }

        [LibraryMethod]
        public void DisplayStandardOutput()
        {
            if (_process == null)
            {
                ConsoleMFLogger.Error(new NullReferenceException("Process is null."));
                return;
            }

            ConsoleMFLogger.Info(_stdOutput.ToString());
        }

        [LibraryMethod]
        public void DisplayStandardError()
        {
            if (_process == null)
            {
                ConsoleMFLogger.Error(new NullReferenceException("Process is null."));
                return;
            }

            ConsoleMFLogger.Error(_stdError.ToString());
        }

        [LibraryMethod]
        public void RawStandardOutput()
        {
            // We don't want to use the logger here since we will be wanting to parse the output.
            Console.WriteLine(_stdOutput.ToString());
        }

        [LibraryMethod]
        public void RawStandardError()
        {
            // We don't want to use the logger here since we will be wanting to parse the output.
            Console.WriteLine(_stdError.ToString());
        }

        /// <summary>
        /// Sets the process name to start.  Can include the full path to the process.
        /// </summary>
        /// <param name="processName">The process name, either on its own or fully qualified.</param>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner ProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return this;
            }

            _processStartInfo.FileName = processName;
            return this;
        }

        /// <summary>
        /// This is the working directory where the process should be run.  This does not need to be the path to the process executable.
        /// </summary>
        /// <param name="workingDir">The working directory.</param>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner WorkingDirectory(string workingDir)
        {
            if (string.IsNullOrWhiteSpace(workingDir))
            {
                return this;
            }

            _processStartInfo.WorkingDirectory = workingDir;
            return this;
        }

        /// <summary>
        /// Adds an argument without a value such as --version.
        /// 
        /// See AddArgument for arguments that include a value.
        /// </summary>
        /// <param name="switchArg">The swtich.</param>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner AddSwitch(string switchArg)
        {
            if (string.IsNullOrWhiteSpace(switchArg))
            {
                return this;
            }

            _processStartInfo.Arguments += " " + switchArg.Trim(' ');
            return this;
        }

        /// <summary>
        /// Adds an argument that includes a key, a value, and a separator.
        /// 
        /// Example: --name MyName
        /// Example: -f=FileName.txt
        /// </summary>
        /// <param name="argumentName">The name of the argument.</param>
        /// <param name="argumentValue">The value of the argument.</param>
        /// <param name="separator">Ususally and space ' ' or a equals sign '='.</param>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner AddArgument(string argumentName, string argumentValue, string separator)
        {
            if (string.IsNullOrWhiteSpace(argumentName) || string.IsNullOrWhiteSpace(argumentValue) || string.IsNullOrEmpty(separator))
            {
                return this;
            }

            _processStartInfo.Arguments += " " + argumentName.Trim(' ') + separator + argumentValue.Trim(' ');
            return this;
        }

        /// <summary>
        /// Adds an argument that includes a key, a value, using a space as a separator.
        /// 
        /// Example: --name MyName
        /// Example: -f=FileName.txt
        /// </summary>
        /// <param name="argumentName">The name of the argument.</param>
        /// <param name="argumentValue">The value of the argument.</param>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner AddArgument(string argumentName, string argumentValue) => AddArgument(argumentName, argumentValue, " ");

        /// <summary>
        /// If set to <see langword="true"/>, allow existing environment variables to be overwritten, otherwise they are not.
        /// 
        /// Defaults to <see langword="false"/>.
        /// </summary>
        /// <param name="value">Boolean.</param>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner OverwriteEnvironmentVariables(bool value)
        {
            _overwriteEnvVars = value;
            return this;
        }

        /// <summary>
        /// Attempts to add an environment variable.
        /// </summary>
        /// <param name="name">Name of the environment variable</param>
        /// <param name="value">Value of the environment variable</param>
        /// <returns>The current ProcessRunner.</returns>
        [LibraryMethod]
        public ProcessRunner AddEnvironmentVariable(string name, string value)
        {
            if (_processStartInfo.EnvironmentVariables != null && _processStartInfo.EnvironmentVariables.ContainsKey(name))
            {
                if (_overwriteEnvVars)
                {
                    _processStartInfo.EnvironmentVariables[name] = value;
                }

                return this;
            }

            _processStartInfo.EnvironmentVariables.Add(name, value);
            return this;
        }
    }
}
