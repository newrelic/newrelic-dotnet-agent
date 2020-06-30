/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace AspNetCore3Features.Controllers
{
    [Route("api/[controller]")]
    public class AsyncStreamController : Controller
    {
        // GET: api/<controller>
        [HttpGet]
        public async Task<string> Get()
        {
            int sum = 0;

            await foreach (var number in GetNumbers())
            {
                sum += number;
            }

            return sum.ToString();
        }

        private async IAsyncEnumerable<int> GetNumbers()
        {
            for (var i = 0; i < 10; i++)
            {
                await DoSomethingAsync();
                yield return i;
            }
        }

        private async Task DoSomethingAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }
}
