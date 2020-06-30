/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

extern alias StackExchangeStrongNameAlias;
using NewRelic.Agent.IntegrationTests.Shared;
using System;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    /// <remarks>
    /// StackExchange.Redis.StrongName has an alias of StackExchangeStrongNameAlias to avoid namespace collision with the standard assembly.
    /// extern alias StackExchangeStrongNameAlias; makes this usable in the file.
    /// </remarks>
    public class RedisController : Controller
    {
        [HttpGet]
        public string StackExchangeRedis()
        {
            var connectionString = StackExchangeRedisConfiguration.StackExchangeRedisConnectionString;
            string value;
            using (var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString))
            {
                var db = redis.GetDatabase();
                db.StringSet("mykey", "myvalue");
                value = db.StringGet("mykey");

                db.StringAppend("mykey", "morevalue");
                db.StringGetRange("mykey", 0, 1);
                db.StringSetRange("mykey", 1, "more");
                db.StringLength("mynumberkey");

                db.StringDecrement("mynumberkey", 1);
                db.StringIncrement("mynumberkey", 1);

                db.HashSet("myhashkey", new StackExchange.Redis.HashEntry[] { new StackExchange.Redis.HashEntry("one", "a"), new StackExchange.Redis.HashEntry("two", 1) });
                db.HashDecrement("myhashkey", "two");
                db.HashIncrement("myhashkey", "two");
                db.HashExists("myhashkey", "one");
                db.HashLength("myhashkey");
                db.HashValues("myhashkey");

                db.KeyExists("mykey");
                db.KeyRandom();
                db.KeyRename("mykey", "newmykey");
                db.KeyDelete("newmykey");

                db.Ping();
                db.IdentifyEndpoint();

                db.SetAdd("myset", "woot");
                db.SetAdd("myotherset", "cool");
                db.SetCombine(StackExchange.Redis.SetOperation.Union, "myset", "myotherset"); //22
                db.SetContains("myset", "woot");
                db.SetLength("myset");
                db.SetMembers("myset");
                db.SetMove("myotherset", "myset", "cool");
                db.SetRandomMember("myset");
                db.SetRemove("myset", "cool");
                db.SetPop("myset");

                db.Publish("mychannel", "cable"); // 31
            }

            return value;
        }

        [HttpGet]
        public string StackExchangeRedisStrongName()
        {
            var connectionString = StackExchangeRedisConfiguration.StackExchangeRedisConnectionString;
            string value;
            //Alias StrongName assembly to avoid type collisions
            using (var redis = StackExchangeStrongNameAlias::StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString))
            {
                var db = redis.GetDatabase();
                db.StringSet("mykey", "myvalue");
                value = db.StringGet("mykey");

                db.StringDecrement("mynumberkey", 1);
                db.StringIncrement("mynumberkey", 1);
                db.StringAppend("mykey", "morevalue");
                db.StringLength("mynumberkey");
                db.StringGetRange("mykey", 0, 1);
                db.StringSetRange("mykey", 1, "more");

                db.HashSet("myhashkey", new StackExchangeStrongNameAlias::StackExchange.Redis.HashEntry[] { new StackExchangeStrongNameAlias::StackExchange.Redis.HashEntry("one", "a"), new StackExchangeStrongNameAlias::StackExchange.Redis.HashEntry("two", 1) });
                db.HashDecrement("myhashkey", "two");
                db.HashIncrement("myhashkey", "two");
                db.HashExists("myhashkey", "one");
                db.HashLength("myhashkey");
                db.HashValues("myhashkey");

                db.KeyExists("mykey");
                db.KeyRandom();
                db.KeyRename("mykey", "newmykey");
                db.KeyDelete("newmykey");

                db.Ping();
                db.IdentifyEndpoint();

                db.SetAdd("myset", "woot");
                db.SetAdd("myotherset", "cool");
                db.SetCombine(StackExchangeStrongNameAlias::StackExchange.Redis.SetOperation.Union, "myset", "myotherset"); //22
                db.SetContains("myset", "woot");
                db.SetLength("myset");
                db.SetMembers("myset");
                db.SetMove("myotherset", "myset", "cool");
                db.SetRandomMember("myset");
                db.SetRemove("myset", "cool");
                db.SetPop("myset");

                db.Publish("mychannel", "cable"); // 31
            }

            return value;
        }

        [HttpGet]
        public async Task<string> StackExchangeRedisAsync()
        {
            var connectionString = StackExchangeRedisConfiguration.StackExchangeRedisConnectionString;
            string value;
            using (var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString))
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync("mykey", "myvalue");
                value = await db.StringGetAsync("mykey");

                await db.StringDecrementAsync("mynumberkey", 1);
                await db.StringIncrementAsync("mynumberkey", 1);
                await db.StringAppendAsync("mykey", "morevalue");
                await db.StringLengthAsync("mynumberkey");
                await db.StringGetRangeAsync("mykey", 0, 1);
                await db.StringSetRangeAsync("mykey", 1, "more");

                await db.HashSetAsync("myhashkey", new StackExchange.Redis.HashEntry[] { new StackExchange.Redis.HashEntry("one", "a"), new StackExchange.Redis.HashEntry("two", 1) });
                await db.HashDecrementAsync("myhashkey", "two");
                await db.HashIncrementAsync("myhashkey", "two");
                await db.HashExistsAsync("myhashkey", "one");
                await db.HashLengthAsync("myhashkey");
                await db.HashValuesAsync("myhashkey");

                await db.KeyExistsAsync("mykey");
                await db.KeyRandomAsync();
                await db.KeyRenameAsync("mykey", "newmykey");
                await db.KeyDeleteAsync("newmykey");

                await db.PingAsync();
                await db.IdentifyEndpointAsync();

                await db.SetAddAsync("myset", "woot");
                await db.SetAddAsync("myotherset", "cool");
                await db.SetCombineAsync(StackExchange.Redis.SetOperation.Union, "myset", "myotherset"); //22
                await db.SetContainsAsync("myset", "woot");
                await db.SetLengthAsync("myset");
                await db.SetMembersAsync("myset");
                await db.SetMoveAsync("myotherset", "myset", "cool");
                await db.SetRandomMemberAsync("myset");
                await db.SetRemoveAsync("myset", "cool");
                await db.SetPopAsync("myset");

                await db.PublishAsync("mychannel", "cable"); // 31
            }

            return value;
        }

        [HttpGet]
        public async Task<string> StackExchangeRedisAsyncStrongName()
        {
            var connectionString = StackExchangeRedisConfiguration.StackExchangeRedisConnectionString;
            string value;
            //Alias StrongName assembly to avoid type collisions
            using (var redis = StackExchangeStrongNameAlias::StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString))
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync("mykey", "myvalue");
                value = await db.StringGetAsync("mykey");

                await db.StringDecrementAsync("mynumberkey", 1);
                await db.StringIncrementAsync("mynumberkey", 1);
                await db.StringAppendAsync("mykey", "morevalue");
                await db.StringLengthAsync("mynumberkey");
                await db.StringGetRangeAsync("mykey", 0, 1);
                await db.StringSetRangeAsync("mykey", 1, "more");

                await db.HashSetAsync("myhashkey", new StackExchangeStrongNameAlias::StackExchange.Redis.HashEntry[] { new StackExchangeStrongNameAlias::StackExchange.Redis.HashEntry("one", "a"), new StackExchangeStrongNameAlias::StackExchange.Redis.HashEntry("two", 1) });
                await db.HashDecrementAsync("myhashkey", "two");
                await db.HashIncrementAsync("myhashkey", "two");
                await db.HashExistsAsync("myhashkey", "one");
                await db.HashLengthAsync("myhashkey");
                await db.HashValuesAsync("myhashkey");

                await db.KeyExistsAsync("mykey");
                await db.KeyRandomAsync();
                await db.KeyRenameAsync("mykey", "newmykey");
                await db.KeyDeleteAsync("newmykey");

                await db.PingAsync();
                await db.IdentifyEndpointAsync();

                await db.SetAddAsync("myset", "woot");
                await db.SetAddAsync("myotherset", "cool");
                await db.SetCombineAsync(StackExchangeStrongNameAlias::StackExchange.Redis.SetOperation.Union, "myset", "myotherset"); //22
                await db.SetContainsAsync("myset", "woot");
                await db.SetLengthAsync("myset");
                await db.SetMembersAsync("myset");
                await db.SetMoveAsync("myotherset", "myset", "cool");
                await db.SetRandomMemberAsync("myset");
                await db.SetRemoveAsync("myset", "cool");
                await db.SetPopAsync("myset");

                await db.PublishAsync("mychannel", "cable"); // 31
            }

            return value;
        }
    }
}
