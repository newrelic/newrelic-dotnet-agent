// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.SignalRPoc.Blazor.Services;

public interface ISearchService
{
    Task<IReadOnlyList<string>> FindAsync(string prefix, CancellationToken cancellationToken);
}
