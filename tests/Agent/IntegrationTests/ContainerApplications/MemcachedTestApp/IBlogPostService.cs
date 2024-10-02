// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace MemcachedTestApp
{
    public interface IBlogPostService
    {
        Dictionary<string, List<BlogPost>> GetRecent(int itemCount);
    }
}
