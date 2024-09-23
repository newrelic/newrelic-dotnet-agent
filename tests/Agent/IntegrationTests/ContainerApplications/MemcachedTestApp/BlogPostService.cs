// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace MemcachedTestApp
{
    public class BlogPostService : IBlogPostService
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<Dictionary<string, List<BlogPost>>> GetRecent(int itemCount)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var dict = new Dictionary<string, List<BlogPost>>();

            var posts = new List<BlogPost>();
            for (int i = 0; i < itemCount; i++)
            {
                posts.Add(
                    new BlogPost
                    {
                        Title = "Hello World" + itemCount,
                        Body = "EnyimCachingCore" + itemCount
                    });
            }


            dict.Add(DateTime.Today.ToString("yyyy-MM-dd"), posts);

            return dict;
        }
    }
}
