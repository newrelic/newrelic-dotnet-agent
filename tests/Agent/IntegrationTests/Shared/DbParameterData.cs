// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Data.SqlTypes;
using System.Globalization;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public static class DbParameterData
    {
        private static readonly DateTimeOffset TestDateTimeOffset = new DateTimeOffset(new DateTime(2018, 1, 2), TimeSpan.FromHours(-1));

        public static DbParameter[] MsSqlParameters =
        {
            new DbParameter("bigint", "@typeBigInt", (long) 123),
            new DbParameter("decimal (30,0)", "@typeDecimal", (decimal) 123.4567),
            new DbParameter("float", "@typeFloat", (float) 123.4567),
            new DbParameter("int", "@typeInt", (int) 123),
            new DbParameter("real", "@typeReal", (double) 123.4567),
            new DbParameter("smallint", "@typeSmallInt", (short) 123),
            new DbParameter("tinyint", "@typeTinyInt", (byte) 123),
            new DbParameter("date", "@typeDate", DateTime.MaxValue) { ExpectedValue = DateTime.MaxValue.ToString(CultureInfo.InvariantCulture)},
            new DbParameter("uniqueidentifier", "@typeGuid", Guid.Empty) { ExpectedValue = Guid.Empty.ToString() },
            new DbParameter("nvarchar(20)", "@typeNVarChar", "some string"),
            new DbParameter("binary", "@typeBinary", new byte[] { 0, 1, 2 }) { ExpectedValue = new byte[0].ToString() },
            new DbParameter("int", "@typeEnumAsInt", DbParamTestingEnum.EnumValue1) { ExpectedValue = (int)DbParamTestingEnum.EnumValue1 },
            new DbParameter("nvarchar(20)", "@typeEnumAsNVarChar", DbParamTestingEnum.EnumValue2) { ExpectedValue = (int)DbParamTestingEnum.EnumValue2 },
            new DbParameter("nvarchar(20)", "@typeDbNull", DBNull.Value) { ExpectedValue = "Null" },
            new DbParameter("bit", "@typeBit", true),
            new DbParameter("char", "@typeChar", 'c'),
            new DbParameter("sql_variant", "@typeSqlVariant", DbParamTestingEnum.EnumValue1) { ExpectedValue = (int)DbParamTestingEnum.EnumValue1 },
            new DbParameter("time", "@typeTimeSpan", TimeSpan.FromHours(1)) { ExpectedValue = TimeSpan.FromHours(1).ToString("c") },
            new DbParameter("datetimeoffset", "@typeDateTimeOffset", TestDateTimeOffset) { ExpectedValue = TestDateTimeOffset.ToString(CultureInfo.InvariantCulture) },
            new DbParameter("nvarchar(20)", "@typeSqlString", new SqlString("test")) { ExpectedValue = "test" },
            new DbParameter("int", "@typeSqlInt", new SqlInt32(32)) { ExpectedValue = 32 },
            new DbParameter("nvarchar(20)", "@typeCharArray", new [] { 't', 'e', 's', 't' }) { ExpectedValue = "test" },
            new DbParameter("nvarchar(20)", "@typeSqlChars", new SqlChars(new [] { 't', 'e', 's', 't' })) { ExpectedValue = "test" },
            new DbParameter("binary", "@typeSqlBinary", new SqlBinary(new byte[]{ 0, 1, 3 })) { ExpectedValue = new SqlBinary(new byte[]{ 0, 1, 3 }).ToString() }
        };

        public static DbParameter[] MySqlParameters =
        {
            new DbParameter("boolean", "typeBoolean", (bool) true),
            new DbParameter("tinyint unsigned", "typeTinyIntUnsigned", (byte) 123),
            new DbParameter("binary(3)", "typeBinary", new byte[]{ 0, 1, 3 }) { ExpectedValue = new byte[]{ 0, 1, 3 }.ToString() },
            new DbParameter("datetime", "typeDatetime", new DateTime(1988, 3, 4)) { ExpectedValue = new DateTime(1988, 3, 4).ToString(CultureInfo.InvariantCulture) },
            new DbParameter("decimal", "typeDecimal", (decimal) 123.4567),
            new DbParameter("double", "typeDouble", (double) 123.4567),
            new DbParameter("char(36)", "typeGuid", Guid.Empty) { ExpectedValue = Guid.Empty.ToString()},
            new DbParameter("smallint", "typeSmallInt", (short) 123),
            new DbParameter("int", "typeInt", (int) 123),
            new DbParameter("bigint", "typeBigInt", (long) 123),
            new DbParameter("tinyint", "typeTinyInt", (sbyte) 123),
            new DbParameter("float", "typeFloat", (float) 123.4567),
            new DbParameter("varchar(20)", "typeVarChar", "some string"),
            new DbParameter("time", "typeTimeSpan", TimeSpan.FromHours(1)) { ExpectedValue = TimeSpan.FromHours(1).ToString("c") },
            new DbParameter("int", "typeIntNull", DBNull.Value) { ExpectedValue = "Null" },
            new DbParameter("varchar(20)", "typeVarCharNull", null) { ExpectedValue = "Null"}
        };

        public static DbParameter[] PostgresParameters =
        {
            new DbParameter("bigint", "typeBigInt", (long) 123),
            new DbParameter("numeric", "typeNumeric", (decimal) 123.4567),
            new DbParameter("real", "typeReal", (float) 123.4567),
            new DbParameter("integer", "typeInt", (int) 123),
            new DbParameter("double precision", "typeDouble", (double) 123.4567),
            new DbParameter("smallint", "typeSmallInt", (short) 123),
            new DbParameter("date", "typeDateTime", DateTime.MaxValue) { ExpectedValue = DateTime.MaxValue.ToString(CultureInfo.InvariantCulture)},
            new DbParameter("uuid", "typeGuid", Guid.Empty) { ExpectedValue = Guid.Empty.ToString() },
            new DbParameter("character varying", "typeCharacterVarying", "some string"),
            new DbParameter("bytea", "typeBinary", new byte[] { 0, 1, 2 }) { ExpectedValue = new byte[0].ToString() },
            new DbParameter("character varying", "typeDbNull", DBNull.Value) { ExpectedValue = "Null" },
            new DbParameter("boolean", "typeBoolean", true),
            new DbParameter("character", "typeCharacter", 'c'),
            new DbParameter("interval", "typeTimeSpan", TimeSpan.FromHours(1)) { ExpectedValue = TimeSpan.FromHours(1).ToString("c") },
            new DbParameter("timestamp with time zone", "typeDateTimeOffset", TestDateTimeOffset) { ExpectedValue = TestDateTimeOffset.ToString(CultureInfo.InvariantCulture) },
            new DbParameter("character varying", "typeCharacterVaryingArray", new [] { 't', 'e', 's', 't' }) { ExpectedValue = "test" }
        };

        public static DbParameter[] OracleParameters =
        {
            new DbParameter("number", "typeNumber", (byte) 123),
            new DbParameter("binary_integer", "typeBinaryInteger", (decimal) 123.4567),
            new DbParameter("raw", "typeRaw", new byte[]{ 0, 1 }) { ExpectedValue = new byte[]{ 0, 1 }.ToString() },
            new DbParameter("date", "typeDate", new DateTime(1988, 3, 4)) { ExpectedValue = new DateTime(1988, 3, 4).ToString(CultureInfo.InvariantCulture) },
            new DbParameter("decimal", "typeDecimal", (decimal) 123.4567),
            new DbParameter("double precision", "typeDouble", (double) 123.4567),
            new DbParameter("number", "typeSmallInt", (short) 123),
            new DbParameter("number", "typeInt", (int) 123),
            new DbParameter("long", "typeLong", (long) 123),
            new DbParameter("float", "typeFloat", (float) 123.4567),
            new DbParameter("string", "typeString", "some string"),
            new DbParameter("interval day to second", "typeTimeSpan", TimeSpan.FromHours(1)) { ExpectedValue = TimeSpan.FromHours(1).ToString("c") },
            new DbParameter("number", "typeIntNull", DBNull.Value) { ExpectedValue = "Null" },
            new DbParameter("string", "typeVarCharNull", null) { ExpectedValue = "Null"}
        };

        public static DbParameter[] IbmDb2Parameters =
        {
            new DbParameter("char(3) for bit data", "typeBinary", new byte[]{ 0, 1 }) { ExpectedValue = new byte[]{ 0, 1 }.ToString() },
            new DbParameter("date", "typeDate", new DateTime(1988, 3, 4)) { ExpectedValue = new DateTime(1988, 3, 4).ToString(CultureInfo.InvariantCulture) },
            new DbParameter("decimal", "typeDecimal", (decimal) 123.4567),
            new DbParameter("double precision", "typeDouble", (double) 123.4567),
            new DbParameter("smallint", "typeSmallInt", (short) 123),
            new DbParameter("int", "typeInt", (int) 123),
            new DbParameter("bigint", "typeBigInt", (long) 123),
            new DbParameter("real", "typeReal", (float) 123.4567),
            new DbParameter("varchar(30)", "typeVarChar", "some string"),
            new DbParameter("time", "typeTime", TimeSpan.FromHours(1)) { ExpectedValue = TimeSpan.FromHours(1).ToString("c") },
            new DbParameter("int", "typeIntNull", DBNull.Value) { ExpectedValue = "Null" },
            new DbParameter("varchar(30)", "typeVarCharNull", null) { ExpectedValue = "Null"}
        };

        public static DbParameter[] OdbcMsSqlParameters =
        {
            new DbParameter("bigint", "@typeBigInt", (long) 123),
            new DbParameter("decimal (30,0)", "@typeDecimal", (decimal) 123.4567),
            new DbParameter("float", "@typeFloat", (float) 123.4567),
            new DbParameter("int", "@typeInt", (int) 123),
            new DbParameter("real", "@typeReal", (double) 123.4567),
            new DbParameter("smallint", "@typeSmallInt", (short) 123),
            new DbParameter("tinyint", "@typeTinyInt", (byte) 123),
            new DbParameter("date", "@typeDate", new DateTime(2018, 1, 2)) { ExpectedValue = new DateTime(2018, 1, 2).ToString(CultureInfo.InvariantCulture)},
            new DbParameter("uniqueidentifier", "@typeGuid", Guid.Empty) { ExpectedValue = Guid.Empty.ToString() },
            new DbParameter("nvarchar(20)", "@typeNVarChar", "some string"),
            new DbParameter("binary", "@typeBinary", new byte[] { 0, 1, 2 }) { ExpectedValue = new byte[0].ToString() },
            new DbParameter("int", "@typeEnumAsInt", DbParamTestingEnum.EnumValue1) { ExpectedValue = (int)DbParamTestingEnum.EnumValue1 },
            new DbParameter("nvarchar(20)", "@typeEnumAsNVarChar", DbParamTestingEnum.EnumValue2) { ExpectedValue = (int)DbParamTestingEnum.EnumValue2 },
            new DbParameter("nvarchar(20)", "@typeDbNull", DBNull.Value) { ExpectedValue = "Null" },
            new DbParameter("bit", "@typeBit", true),
            new DbParameter("char", "@typeChar", 'c'),
            new DbParameter("sql_variant", "@typeSqlVariant", DbParamTestingEnum.EnumValue1) { ExpectedValue = (int)DbParamTestingEnum.EnumValue1 },
            new DbParameter("time", "@typeTimeSpan", TimeSpan.FromHours(1)) { ExpectedValue = TimeSpan.FromHours(1).ToString("c") },
            new DbParameter("nvarchar(20)", "@typeCharArray", new [] { 't', 'e', 's', 't' }) { ExpectedValue = "test" }
        };

        public static DbParameter[] OleDbMsSqlParameters =
        {
            new DbParameter("bigint", "@typeBigInt", (long) 123),
            new DbParameter("decimal (30,0)", "@typeDecimal", (decimal) 123.4567),
            new DbParameter("float", "@typeFloat", (float) 123.4567),
            new DbParameter("int", "@typeInt", (int) 123),
            new DbParameter("real", "@typeReal", (double) 123.4567),
            new DbParameter("smallint", "@typeSmallInt", (short) 123),
            new DbParameter("tinyint", "@typeTinyInt", (byte) 123),
            new DbParameter("date", "@typeDate", new DateTime(2018, 1, 2)) { ExpectedValue = new DateTime(2018, 1, 2).ToString(CultureInfo.InvariantCulture)},
            new DbParameter("uniqueidentifier", "@typeGuid", Guid.Empty) { ExpectedValue = Guid.Empty.ToString() },
            new DbParameter("nvarchar(20)", "@typeNVarChar", "some string"),
            new DbParameter("binary", "@typeBinary", new byte[] { 0, 1, 2 }) { ExpectedValue = new byte[0].ToString() },
            new DbParameter("int", "@typeEnumAsInt", DbParamTestingEnum.EnumValue1) { ExpectedValue = (int)DbParamTestingEnum.EnumValue1 },
            new DbParameter("nvarchar(20)", "@typeEnumAsNVarChar", DbParamTestingEnum.EnumValue2) { ExpectedValue = (int)DbParamTestingEnum.EnumValue2 },
            new DbParameter("nvarchar(20)", "@typeDbNull", DBNull.Value) { ExpectedValue = "Null" },
            new DbParameter("bit", "@typeBit", true),
            new DbParameter("char", "@typeChar", 'c'),
            new DbParameter("sql_variant", "@typeSqlVariant", DbParamTestingEnum.EnumValue1) { ExpectedValue = (int)DbParamTestingEnum.EnumValue1 },
            new DbParameter("time", "@typeTimeSpan", TimeSpan.FromHours(1)) { ExpectedValue = TimeSpan.FromHours(1).ToString("c") }
        };
    }

    public class DbParameter
    {
        public DbParameter(string dbTypeName, string parameterName, object value)
        {
            DbTypeName = dbTypeName;
            ParameterName = parameterName;
            Value = value;
            ExpectedValue = value;
        }

        public string DbTypeName { get; }
        public string ParameterName { get; }
        public object Value { get; }
        public object ExpectedValue { get; set; }
    }

    public enum DbParamTestingEnum
    {
        EnumValue1,
        EnumValue2
    }
}
