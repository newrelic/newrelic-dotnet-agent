// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace MemcachedTestApp
{
    public class BlogPostService : IBlogPostService
    {
        public Dictionary<string, List<BlogPost>> GetRecent(int itemCount)
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
