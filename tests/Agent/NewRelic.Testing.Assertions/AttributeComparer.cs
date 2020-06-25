/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
namespace NewRelic.Testing.Assertions
{
    public static class AttributeComparer
    {
        public static bool IsEqualTo(this object val1, object val2)
        {
            if (val1 == null && val2 == null) return true;
            if (val1 != null && val2 == null) return false;
            if (val1 == null && val2 != null) return false;

            //Will return consistent types
            var val1Comp = ConvertForCompare(val1);
            var val2Comp = ConvertForCompare(val2);

            switch (Type.GetTypeCode(val1Comp.GetType()))
            {
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return Math.Abs((double)val1Comp - (double)val2Comp) < .0001;

                default:
                    return val1Comp.Equals(val2Comp);
            }
        }

        public static bool IsNotEqualTo(this object val1, object val2)
        {
            return !IsEqualTo(val1, val2);
        }

        public static object ConvertForCompare(object val)
        {
            if (val == null)
            {
                return null;
            }

            if (val is TimeSpan)
            {
                return ((TimeSpan)val).TotalSeconds;
            }

            if (val is DateTimeOffset)
            {
                return ((DateTimeOffset)val).ToString("o");
            }

            switch (Type.GetTypeCode(val.GetType()))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return Convert.ToInt64(val);

                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return Convert.ToDouble(val);

                case TypeCode.DateTime:
                    return ((DateTime)val).ToString("o");

                case TypeCode.String:
                    return val;

                default:
                    return val.ToString();
            }
        }
    }
}
