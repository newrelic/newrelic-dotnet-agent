// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.TestUtilities
{
    public static class ServerlessPayloadHelpers
    {
        /// <summary>
        /// Takes a full serverless payload and extracts, unzips and returns the compressed portion of the payload
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        public static string GetUnzippedPayload(this string payload)
        {
            var payloadObj = JsonConvert.DeserializeObject<List<object>>(payload);

            Assert.That(payloadObj, Is.Not.Null);
            Assert.That(payloadObj, Has.Count.EqualTo(4));

            var compressedPayload = payloadObj[3].ToString();

            // Base-64 decode, then unzip to get the raw payload
            var zippedPayload = Convert.FromBase64String(compressedPayload);
            using var ms = new MemoryStream(zippedPayload);
            using var gzip = new GZipStream(ms, CompressionMode.Decompress);
            using var ms2 = new MemoryStream();
            gzip.CopyTo(ms2);
            var unzippedBytes = ms2.ToArray();
            var unzippedPayload = Encoding.UTF8.GetString(unzippedBytes);
            return unzippedPayload;
        }

    }
}
