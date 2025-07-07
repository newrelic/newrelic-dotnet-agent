// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enyim.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MemcachedTestApp.Controllers
{
    [ApiController]
    [Route("Memcached")]
    public class MemcachedController : ControllerBase
    {
        private readonly IMemcachedClient _memcachedClient;
        private readonly IBlogPostService _blogPostService;
        private readonly ILogger<MemcachedController> _logger;

        public MemcachedController(IMemcachedClient memcachedClient, IBlogPostService blogPostService, ILogger<MemcachedController> logger)
        {
            _memcachedClient = memcachedClient;
            _blogPostService = blogPostService;
            _logger = logger;
        }

        [HttpGet]
        [Route("testallmethods")]
        public async Task<string> TestAllMethods()
        {
            FlushAll();
            await GetValueOrCreateAsync();
#pragma warning disable VSTHRD103
            Get();
#pragma warning restore VSTHRD103
            GetGen();
            await GetAsync();
            await GetAsyncGen();
            Increment();
            Decrement();
#if NET9_0
            await TouchAsync();
#endif
#pragma warning disable VSTHRD103
            GetMany();
#pragma warning restore VSTHRD103
            await GetManyAsync();
#pragma warning disable VSTHRD103
            Remove();
#pragma warning restore VSTHRD103
            await RemoveAsync();

            return "Complete";
        }

        private void FlushAll()
        {
            _memcachedClient.FlushAll();
        }

        private async Task GetValueOrCreateAsync()
        {
            var cacheKey = "GetValueOrCreateAsync";
            var cacheSeconds = 600;

            var posts = await _memcachedClient.GetValueOrCreateAsync(
                cacheKey,
                cacheSeconds,
                async () => await Task.FromResult(_blogPostService.GetRecent(2)));
        }

        private void Get()
        {
            var value = _blogPostService.GetRecent(2);
            _memcachedClient.Add("Get", value, 600);

#pragma warning disable CS0618 // Type or member is obsolete
            var posts = _memcachedClient.Get("Get");
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private void GetGen()
        {
            var value = _blogPostService.GetRecent(2);
            _memcachedClient.Add("GetGen", value, 600);

            var posts = _memcachedClient.Get<object>("GetGen");
        }

        private async Task GetAsync()
        {
            var value = _blogPostService.GetRecent(2);
            await _memcachedClient.AddAsync("GetAsync", value, 600);
#pragma warning disable CS0618 // Type or member is obsolete
#if NET8_0
            var posts = await _memcachedClient.GetAsync<object>("GetAsync");
#elif NET9_0
            var posts = await _memcachedClient.GetAsync("GetAsync");
#endif
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private async Task GetAsyncGen()
        {
            var value = _blogPostService.GetRecent(2);
            await _memcachedClient.AddAsync("GetAsyncGen", value, 600);

            var posts = await _memcachedClient.GetAsync<object>("GetAsyncGen");
        }

        private void Increment()
        {
            var value = _blogPostService.GetRecent(2);
            _memcachedClient.Add("Increment", value, 600);

            var posts = _memcachedClient.Increment("Increment", 1, 1, 1);
        }

        private void Decrement()
        {
            var value = _blogPostService.GetRecent(2);
            _memcachedClient.Add("Decrement", value, 600);

            var posts = _memcachedClient.Decrement("Decrement", 1, 1, 1);
        }

#if NET9_0
        private async Task TouchAsync()
        {
            var value = _blogPostService.GetRecent(2);
            await _memcachedClient.AddAsync("TouchAsync", value, 600);

            var posts = await _memcachedClient.TouchAsync("TouchAsync", DateTime.Now.AddDays(5));
        }
#endif
        // This is not instrumented
        private void GetMany()
        {
            var value = _blogPostService.GetRecent(2);
            _memcachedClient.Add("GetMany", value, 600);

            var keys = new List<string> { "GetMany" };
            var posts = _memcachedClient.Get<object>(keys);
        }

        // This is not instrumented
        private async Task GetManyAsync()
        {
            var value = _blogPostService.GetRecent(2);
            await _memcachedClient.AddAsync("GetManyAsync", value, 600);

            var keys = new List<string> { "GetManyAsync" };
            var posts = await _memcachedClient.GetAsync<object>(keys);
        }

        private void Remove()
        {
            var value = _blogPostService.GetRecent(2);
            _memcachedClient.Add("Remove", value, 600);

            var posts = _memcachedClient.Remove("Remove");
        }

        private async Task RemoveAsync()
        {
            var value = _blogPostService.GetRecent(2);
            await _memcachedClient.AddAsync("RemoveAsync", value, 600);

            var posts = await _memcachedClient.RemoveAsync("RemoveAsync");
        }
    }
}
