// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Mono.Cecil;
/// <summary>
/// Defines a contract for a job that performs a modification on a module definition.
/// </summary>
/// <remarks>Implementations of this interface encapsulate logic to modify a given module. The result of the
/// operation is indicated by the return value of the Run method.</remarks>
public interface IModificationJob
{
    /// <summary>
    /// Run the logic to modify the specified module.
    /// </summary>
    /// <param name="module">The module to modify</param>
    /// <returns>true if successful and false otherwise</returns>
    public bool Run(ModuleDefinition module);
}
