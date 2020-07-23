﻿using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	[TestFixture]
	public class ProfileNodeTests
	{
		[Test]
		public void serializes_correctly_without_children()
		{
			var profileNode = new ProfileNode(new IntPtr(1), 2, 3);
			profileNode.Details.ClassName = "myClass";
			profileNode.Details.MethodName = "myMethod";
			profileNode.Details.LineNumber = 4;

			var json = JsonConvert.SerializeObject(profileNode);

			const String expectedJson = @"[[""myClass"",""myMethod"",4],2,0,[]]";
			Assert.AreEqual(expectedJson, json);
		}

		[Test]
		public void serializes_correctly_with_children()
		{
			var profileNode1 = new ProfileNode(new IntPtr(1), 2, 3);
			profileNode1.Details.ClassName = "myClass1";
			profileNode1.Details.MethodName = "myMethod1";
			profileNode1.Details.LineNumber = 4;

			var profileNode2 = new ProfileNode(new IntPtr(11), 12, 13);
			profileNode2.Details.ClassName = "myClass2";
			profileNode2.Details.MethodName = "myMethod2";
			profileNode2.Details.LineNumber = 14;
			profileNode1.Children.Add(profileNode2);

			var json = JsonConvert.SerializeObject(profileNode1);

			const String expectedJson = @"[[""myClass1"",""myMethod1"",4],2,0,[[[""myClass2"",""myMethod2"",14],12,0,[]]]]";
			Assert.AreEqual(expectedJson, json);
		}
	}
}
