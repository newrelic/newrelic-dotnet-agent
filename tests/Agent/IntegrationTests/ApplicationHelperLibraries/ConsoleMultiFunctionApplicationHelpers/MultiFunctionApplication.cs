// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.ApplicationHelpers;
using System;
using System.Diagnostics;
using System.Linq;

namespace MultiFunctionApplicationHelpers
{
    public static class MultiFunctionApplication
    {
        public static void Execute(string[] args)
        {
            var methodExecutor = new DynamicMethodExecutor();
            var keepAliveOnError = args.Any(x => x.Equals("KeepAliveOnError", StringComparison.OrdinalIgnoreCase));
            var shouldExit = false;

            Console.WriteLine("Console Multi Function Application");
            Console.WriteLine($"Process Info: {Process.GetCurrentProcess().ProcessName} {Process.GetCurrentProcess().Id}");
            Console.WriteLine($"Keep Alive on Error: {keepAliveOnError}");
            Console.WriteLine();


            while (!shouldExit)
            {
                Console.Write($"{DateTime.Now.ToLongTimeString()} >");
                var command = Console.ReadLine().Trim();

                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                var startPos = 0;
                for (startPos = 0; startPos < command.Length; startPos++)
                {
                    if (char.IsLetter(command[startPos]))
                    {
                        break;
                    }
                }

                command = command.Substring(startPos);

                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }
                
                Console.WriteLine();
                if (command.Equals("exit", StringComparison.OrdinalIgnoreCase) || command.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    shouldExit = true;
                    continue;
                }

                if (command.Equals("help", StringComparison.OrdinalIgnoreCase) || command.Equals("usage", StringComparison.OrdinalIgnoreCase) || command.Equals("?"))
                {
                    PrintUsage();
                    continue;
                }

                ExecuteCommand(methodExecutor, keepAliveOnError, command);
            }

        }

        static void ExecuteCommand(DynamicMethodExecutor methodExecutor, bool keepAliveOnError, string commandText)
        {
            Logger.Info($"EXECUTING: '{commandText}'");

            var args = commandText.Split(' ');
            
            if (args.Count() < 2)
            {
                Logger.Error($"INVALID FORMAT: '{commandText}'");
                PrintUsage();

                if (!keepAliveOnError)
                {
                    Environment.Exit(-1);
                }

                return;
            }

            var libraryName = args[0];
            var methodName = args[1];
            var methodParams = args.Skip(2).ToArray();
            var countMethodParams = methodParams.Count();

            var matchedMethods = ReflectionUtil.FindMethodUsingAttributes<LibraryAttribute, LibraryMethodAttribute>(libraryName, methodName)
                .Where(x => x.GetParameters().Count() == countMethodParams)
                .ToArray();

            if (matchedMethods.Length == 0)
            {
                Logger.Error($"\tMETHOD MISMATCH: Unable to find method {libraryName}.{methodName} that accepts {countMethodParams} parameters.");
                PrintUsage();

                if (!keepAliveOnError)
                {
                    Environment.Exit(-2);
                }
                return;
            }

            if (matchedMethods.Length > 1)
            {
                Logger.Error($"\tMETHOD MISMATCH: Ambiguous Match on {libraryName}.{methodName} with {countMethodParams} parameters");
                PrintUsage();

                if (!keepAliveOnError)
                {
                    Environment.Exit(-3);
                }
                return;
            }

            try
            {
                methodExecutor.ExecuteDynamicMethod(matchedMethods[0], methodParams);
            }
            catch (Exception ex)
            {
                Logger.Error($"\tEXECUTION ERROR: {ex}");

                if (!keepAliveOnError)
                {
                    Environment.Exit(-4);
                }
                return;
            }
        }

        private static void PrintUsage()
        {
            Logger.Info("USAGE:");
            Logger.Info("");
            Logger.Info("Here's a list of registered Library Methods:");

            var libraries = ReflectionUtil.FindTypesWithAttribute<LibraryAttribute>()
                .OrderBy(t => t.Name)
                .ToArray();

            foreach (var library in libraries)
            {
                Logger.Info();
                Logger.Info($"\t{library.Name.ToUpper()}");
                Logger.Info();

                var methods = ReflectionUtil.FindMethodsWithAttribute<LibraryMethodAttribute>(library)
                    .OrderBy(m => m.Name)
                    .ToArray();

                if (!methods.Any())
                {
                    continue;
                }

                foreach (var method in methods)
                {
                    var paramInfos = method.GetParameters()
                        .Select(p => $"{{{p.Name}:{p.ParameterType.Name}}}")
                        .ToList();

                    Logger.Info($"\t\t{library.Name} {method.Name} {string.Join(" ", paramInfos)}");
                }
            }

            Logger.Info();
        }


    }
}
