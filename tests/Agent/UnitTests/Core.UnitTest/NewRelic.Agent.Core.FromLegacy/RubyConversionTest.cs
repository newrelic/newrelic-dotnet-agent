/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Net;
using NewRelic.Agent.Core.Exceptions;
using NUnit.Framework;

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class RubyConversionTest
    {
        [Test]
        public static void TestGenericHttpError()
        {
            Exception ex = ExceptionFactories.NewException(HttpStatusCode.HttpVersionNotSupported, "Dude");
            Assert.That(ex is HttpException);
        }

        [Test]
        public static void TestSerializationException()
        {
            Exception ex = ExceptionFactories.NewException(HttpStatusCode.UnsupportedMediaType, "Dude");
            Assert.That(ex is SerializationException);
            Assert.AreEqual("Dude", ex.Message);
        }

        [Test]
        public static void TestForceDisconnect()
        {
            Exception ex = ExceptionFactories.NewException("NewRelic::Agent::ForceDisconnectException", "Dude");

            Assert.That(ex is ForceDisconnectException);
            Assert.AreEqual("Dude", ex.Message);
        }

        [Test]
        public static void TestForceRestart()
        {
            Exception ex = ExceptionFactories.NewException("NewRelic::Agent::ForceRestartException", "Error");

            Assert.That(ex is ForceRestartException);
            Assert.AreEqual("Error", ex.Message);
        }

        [Test]
        public static void TestPostTooBigException()
        {
            Exception ex = ExceptionFactories.NewException("NewRelic::Agent::PostTooBigException", "Gigantic");

            Assert.That(ex is PostTooBigException);
            Assert.AreEqual("Gigantic", ex.Message);
        }
    }
}
