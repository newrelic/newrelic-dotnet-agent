// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.Shared;
using StackExchange.Redis;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.StackExchangeRedis
{
    [Library]
    public class StackExchangeRedisExerciser
    {
        private ConfigurationOptions _redisConfigOptions;

        [LibraryMethod]
        [Transaction]
        public void DoSomeWork()
        {
            using (var redis = ConnectionMultiplexer.Connect(GetRedisConnectionOptions()))
            {
                // KeyRename throws an exception if the key doesn't exist, which can be a problem if this
                // code is run in parallel. Make sure we have a unique one
                string key = Guid.NewGuid().ToString();
                var db = redis.GetDatabase();
                db.StringSet(key, "myvalue");
                string value = db.StringGet(key);

                db.StringAppend(key, "morevalue");
                db.StringGetRange(key, 0, 1);
                db.StringSetRange(key, 1, "more");
                db.StringLength("mynumberkey");

                db.StringDecrement("mynumberkey", 1);
                db.StringIncrement("mynumberkey", 1);

                db.HashSet("myhashkey", new HashEntry[] { new HashEntry("one", "a"), new HashEntry("two", 1) });
                db.HashDecrement("myhashkey", "two");
                db.HashIncrement("myhashkey", "two");
                db.HashExists("myhashkey", "one");
                db.HashLength("myhashkey");
                db.HashValues("myhashkey");

                db.KeyExists(key);
                db.KeyRandom();
                db.KeyRename(key, "newmykey");
                db.KeyDelete("newmykey");

                db.Ping();
                db.IdentifyEndpoint();

                db.SetAdd("myset", "woot");
                db.SetAdd("myotherset", "cool");
                db.SetCombine(SetOperation.Union, "myset", "myotherset"); //22
                db.SetContains("myset", "woot");
                db.SetLength("myset");
                db.SetMembers("myset");
                db.SetMove("myotherset", "myset", "cool");
                db.SetRandomMember("myset");
                db.SetRemove("myset", "cool");
                db.SetPop("myset");

                db.Publish(new RedisChannel("mychannel", RedisChannel.PatternMode.Literal), "cable"); // 31
            }

            ConsoleMFLogger.Info("All done!");
        }

        [LibraryMethod]
        [Transaction]
        public async Task DoSomeWorkAsync()
        {
            using (var redis = await ConnectionMultiplexer.ConnectAsync(GetRedisConnectionOptions()))
            {
                // KeyRename throws an exception if the key doesn't exist, which can be a problem if this
                // code is run in parallel. Make sure we have a unique one
                string key = Guid.NewGuid().ToString();
                var db = redis.GetDatabase();
                await db.StringSetAsync(key, "myvalue");
                string value = await db.StringGetAsync(key);

                await db.StringDecrementAsync("mynumberkey", 1);
                await db.StringIncrementAsync("mynumberkey", 1);
                await db.StringAppendAsync(key, "morevalue");
                await db.StringLengthAsync("mynumberkey");
                await db.StringGetRangeAsync(key, 0, 1);
                await db.StringSetRangeAsync(key, 1, "more");

                await db.HashSetAsync("myhashkey", new HashEntry[] { new HashEntry("one", "a"), new HashEntry("two", 1) });
                await db.HashDecrementAsync("myhashkey", "two");
                await db.HashIncrementAsync("myhashkey", "two");
                await db.HashExistsAsync("myhashkey", "one");
                await db.HashLengthAsync("myhashkey");
                await db.HashValuesAsync("myhashkey");

                await db.KeyExistsAsync(key);
                await db.KeyRandomAsync();
                await db.KeyRenameAsync(key, "newmykey");
                await db.KeyDeleteAsync("newmykey");

                await db.PingAsync();
                await db.IdentifyEndpointAsync();

                await db.SetAddAsync("myset", "woot");
                await db.SetAddAsync("myotherset", "cool");
                await db.SetCombineAsync(SetOperation.Union, "myset", "myotherset"); //22
                await db.SetContainsAsync("myset", "woot");
                await db.SetLengthAsync("myset");
                await db.SetMembersAsync("myset");
                await db.SetMoveAsync("myotherset", "myset", "cool");
                await db.SetRandomMemberAsync("myset");
                await db.SetRemoveAsync("myset", "cool");
                await db.SetPopAsync("myset");

                await db.PublishAsync(new RedisChannel("mychannel", RedisChannel.PatternMode.Literal), "cable"); // 31
            }

            ConsoleMFLogger.Info("All done!");
        }

        private ConfigurationOptions GetRedisConnectionOptions()
        {
            if (_redisConfigOptions == null)
            {
                var connectionString = StackExchangeRedisConfiguration.StackExchangeRedisConnectionString;
                _redisConfigOptions = ConfigurationOptions.Parse(connectionString);
                _redisConfigOptions.Password = StackExchangeRedisConfiguration.StackExchangeRedisPassword;
            }
            return _redisConfigOptions;
        }

    }
}
