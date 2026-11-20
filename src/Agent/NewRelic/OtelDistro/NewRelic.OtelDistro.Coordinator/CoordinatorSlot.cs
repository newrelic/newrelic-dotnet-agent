// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>Find-or-create logic for the process's single coordinator. The caller holds the cross-domain mutex.</summary>
public static class CoordinatorSlot
{
    /// <summary>Name under which the coordinator is stored (AppDomain data key on .NET Framework).</summary>
    public const string SlotName = "NewRelic.OtelDistro.Coordinator";

    /// <summary>
    /// Returns the slot's coordinator, or creates one with <paramref name="create"/> and stores it. Returns null and sets
    /// <paramref name="failure"/> when the slot holds a different coordinator version or a non-coordinator object, or when
    /// reading the slot, creating, or storing throws. Never throws.
    /// </summary>
    public static RegistrationCoordinator FindOrCreate(ICoordinatorSlot slot, string expectedVersion, Func<RegistrationCoordinator> create, out bool created, out string failure)
    {
        created = false;
        failure = null;

        try
        {
            var existing = slot.Get();
            if (existing != null)
            {
                if (existing is not RegistrationCoordinator coordinator)
                {
                    failure = "slot holds " + existing.GetType().FullName;
                    return null;
                }

                if (!string.Equals(coordinator.Version, expectedVersion, StringComparison.Ordinal))
                {
                    failure = "coordinator version mismatch: slot has " + coordinator.Version + ", expected " + expectedVersion;
                    return null;
                }

                return coordinator;
            }
        }
        catch (Exception ex)
        {
            failure = Describe("slot get", ex);
            return null;
        }

        RegistrationCoordinator fresh;
        try
        {
            fresh = create();
        }
        catch (Exception ex)
        {
            failure = Describe("create", ex);
            return null;
        }

        if (fresh == null)
        {
            failure = "create: returned null";
            return null;
        }

        try
        {
            slot.Set(fresh);
        }
        catch (Exception ex)
        {
            failure = Describe("slot set", ex);
            return null;
        }

        created = true;
        return fresh;
    }

    /// <summary>Formats a failure as "&lt;step&gt;: &lt;ExceptionType&gt;: &lt;message&gt;".</summary>
    public static string Describe(string step, Exception ex) => step + ": " + ex.GetType().Name + ": " + ex.Message;
}
