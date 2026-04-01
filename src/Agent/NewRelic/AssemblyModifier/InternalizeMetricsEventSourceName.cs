// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Mono.Cecil;
/// <summary>
/// Updates the name of the EventSource used to make metric data available out-of-process
/// so that tools like dotnet-counters will use the application's EventSource data
/// instead of the agent's EventSource data.
/// </summary>
public class InternalizeMetricsEventSourceName(Logger logger) : IModificationJob
{
    public bool Run(ModuleDefinition module)
    {
        TypeDefinition? metricsEventSourceType = module.Types
                .FirstOrDefault(t => t.FullName == "System.Diagnostics.Metrics.MetricsEventSource");

        if (metricsEventSourceType == null)
        {
            logger.Log("Type 'System.Diagnostics.Metrics.MetricsEventSource' not found in the assembly.");
            return false;
        }

        CustomAttribute? eventSourceAttribute = metricsEventSourceType.CustomAttributes
        .FirstOrDefault(a => a.AttributeType.FullName == "System.Diagnostics.Tracing.EventSourceAttribute");

        if (eventSourceAttribute == null)
        {
            logger.Log("EventSource attribute not found on the type.");
            return false;
        }

        // Modify the EventSource attribute to add "Vendored." prefix
        var nameProperty = eventSourceAttribute.Properties.FirstOrDefault(p => p.Name == "Name");
        if (nameProperty.Name == null)
        {
            logger.Log("Name property not found in EventSource attribute.");
            return false;
        }

        string currentName = nameProperty.Argument.Value?.ToString() ?? string.Empty;
        if (currentName.StartsWith("Vendored."))
        {
            logger.Log($"EventSource Name already has 'Vendored.' prefix: {currentName}");
        }

        string newName = "Vendored." + currentName;

        // Remove the old property
        eventSourceAttribute.Properties.Remove(nameProperty);

        // Add the new property with updated value
        eventSourceAttribute.Properties.Add(new CustomAttributeNamedArgument(
            nameProperty.Name,
            new CustomAttributeArgument(nameProperty.Argument.Type, newName)));

        logger.Log($"Updated EventSource Name from '{currentName}' to '{newName}'");

        return true;
    }
}
