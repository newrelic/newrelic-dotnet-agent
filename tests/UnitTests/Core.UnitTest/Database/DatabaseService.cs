﻿using JetBrains.Annotations;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.Database.UnitTest
{
	public class Class_DatabaseService
	{

		[TestFixture, Category("JustMock")]
		public class Request_GetObfuscatedSql
		{
			[NotNull]
			private DatabaseService _databaseService;

			[SetUp]
			public void Setup()
			{
				_databaseService = new DatabaseService();
			}

			[TearDown]
			public void Teardown()
			{
				_databaseService.Dispose();
			}

			[Test]
			public void when_connected_then_responds_to_GetObfuscatedSqlRequest()
			{
				// ARRANGE
				const string unobfuscatedSql = "select foo from bar where credit_card=123456789";

				// ACT
				var obfuscatedSql = _databaseService.SqlObfuscator.GetObfuscatedSql(unobfuscatedSql);

				// ASSERT
				Assert.IsNotNull(obfuscatedSql);
				Assert.IsNotEmpty(unobfuscatedSql);
				Assert.AreNotEqual(unobfuscatedSql, obfuscatedSql);
			}
		}
	}
}
