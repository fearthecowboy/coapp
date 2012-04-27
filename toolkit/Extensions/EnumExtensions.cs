//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Linq;

    public static class EnumExtensions {
        public static T ParseEnum<T>(this string value, T defaultValue = default(T)) where T : struct, IConvertible {
            if (!typeof (T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            if (Enum.IsDefined(typeof (T), value)) {
                return (T)Enum.Parse(typeof (T), value, true);
            }
            return defaultValue;
        }

        public static T CastToEnum<T>(this string value) where T : struct, IConvertible {
            if (!typeof (T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            if (value.Contains("+")) {
                var values = value.Split('+');
                Type numberType = Enum.GetUnderlyingType(typeof (T));
                if (numberType.Equals(typeof (int))) {
                    var newResult = values.Aggregate(0, (current, val) => current | (int)(Object)ParseEnum<T>(val));
                    return (T)(Object)newResult;
                }
            }
            return ParseEnum<T>(value);
        }

        public static string CastToString<T>(this T value) where T : struct, IConvertible {
            return Enum.Format(typeof (T), value, "G").Replace(", ", "+");
        }
    }
}