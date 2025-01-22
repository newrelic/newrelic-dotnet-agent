// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers
{
    public class ObjectHelperTests
    {
        private class NestedClass
        {
            public string NestedProperty { get; set; } = "NestedProperty";
#pragma warning disable CS0414 // Field is assigned but its value is never used
            public int NestedField = 99;
#pragma warning restore CS0414 // Field is assigned but its value is never used
        }

        private class TestClass
        {
            public string PublicProperty { get; set; } = "PublicProperty";
            private string PrivateProperty { get; set; } = "PrivateProperty";
#pragma warning disable CS0414 // Field is assigned but its value is never used
            public int PublicField = 42;
            private int PrivateField = 24;
#pragma warning restore CS0414 // Field is assigned but its value is never used
            public List<string> ListProperty { get; set; } = new List<string> { "Item1", "Item2" };
            public NestedClass NestedObject { get; set; } = new NestedClass();
            public List<NestedClass> NestedListProperty { get; set; } = new List<NestedClass>
                            {
                                new NestedClass { NestedProperty = "NestedItem1", NestedField = 1 },
                                new NestedClass { NestedProperty = "NestedItem2", NestedField = 2 }
                            };
            public string LicenseKey { get; set; } = "12345-ABCDE";
            public string ApiKey { get; set; } = "ABCDE-12345";
            public string AccountId { get; set; } = "123456789012";
        }

        [Test]
        public void GetObjectAsString_ShouldIncludePublicProperties()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Contain("PublicProperty: PublicProperty"));
        }

        [Test]
        public void GetObjectAsString_ShouldNotIncludePrivateProperties()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Not.Contain("PrivateProperty: PrivateProperty"));
        }

        [Test]
        public void GetObjectAsString_ShouldIncludePublicFields()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Contain("PublicField: 42"));
        }

        [Test]
        public void GetObjectAsString_ShouldNotIncludePrivateFields()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Not.Contain("PrivateField: 24"));
        }

        [Test]
        public void GetObjectAsString_ShouldIncludeListItems()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Contain("ListProperty[0]: Item1"));
            Assert.That(result, Does.Contain("ListProperty[1]: Item2"));
        }

        [Test]
        public void GetObjectAsString_ShouldIncludeNestedObject()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Contain("NestedObject.NestedProperty: NestedProperty"));
            Assert.That(result, Does.Contain("NestedObject.NestedField: 99"));
        }

        [Test]
        public void GetObjectAsString_ShouldIncludeNestedListItems()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Contain("NestedListProperty[0].NestedProperty: NestedItem1"));
            Assert.That(result, Does.Contain("NestedListProperty[0].NestedField: 1"));
            Assert.That(result, Does.Contain("NestedListProperty[1].NestedProperty: NestedItem2"));
            Assert.That(result, Does.Contain("NestedListProperty[1].NestedField: 2"));
        }

        [Test]
        public void GetObjectAsString_ShouldObfuscateLicenseKeys()
        {
            var testObj = new TestClass();
            var result = ObjectHelper.GetObjectAsString(testObj);

            Assert.That(result, Does.Contain("LicenseKey: ***********"));
            Assert.That(result, Does.Contain("ApiKey: ***********"));
            Assert.That(result, Does.Contain("AccountId: ***********"));
        }

        [Test]
        public void GetObjectAsString_ShouldHandleObjectArray()
        {
            var testArray = new object[] { "string", 123, new TestClass() };
            var result = ObjectHelper.GetObjectAsString(testArray);

            Assert.That(result, Does.Contain("[0]: string"));
            Assert.That(result, Does.Contain("[1]: 123"));
            Assert.That(result, Does.Contain("[2].PublicProperty: PublicProperty"));
        }
    }
}
