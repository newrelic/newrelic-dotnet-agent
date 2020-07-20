﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	[TestFixture]
	public class ThreadProfilingModelTests
	{
		[Test]
		public void serializes_correctly()
		{
			var threadProfilingModel = new ThreadProfilingModel
				(
				1,
				new DateTime(2),
				new DateTime(3),
				4,
				new Dictionary<String, Object> { { "OTHER", new ProfileNode(new IntPtr(5), 6, 7) } },
				10,
				11
				);

			var json = JsonConvert.SerializeObject(threadProfilingModel);

			const String expectedJson = @"[1,-62135596800.0,-62135596800.0,4,{""OTHER"":[[null,null,0],6,0,[]]},10,11]";
			Assert.AreEqual(expectedJson, json);
		}
	}
}
