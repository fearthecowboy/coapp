//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Eric Schultz. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    
    public static class ObjectExtensions
    {
        private static int[] tenPrimes = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29 };

        /// <summary>
        /// Create a pretty good hashcode using the hashcodes from a bunch of objects.
        /// <para>
        /// Say you have an class with a bunch of properties (usually strings). You want
        /// instantiations to have the same hashcode when all the properties are the same.
        /// This method provides the following functionality:
        /// <list type="bullet">
        /// <item>When <paramref name="objects"/> has 10 or fewer items, returns the sums of the result of multiplying the hashcode of the item at index i by 
        /// the i+1'th prime (The object at index 0 with have it's hashcode multiplied by 2, index 1 * 3, etc. all summed together)</item>
        /// <item>When <paramref name="objects"/> has 11 or more items, takes the result from the previous item and then adds the hashcode
        /// of each item from index 10 on. (The result from the previous item + objects[11] + objects[12] + ...)</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="input">The <see cref="System.Object"/> to create a hashcode for.</param>
        /// <param name="objects">The objects whose hashcodes you want to use.</param>
        /// <returns>The resultant <see cref="System.Int32"/> hashcode</returns>
        public static int CreateHashCode(this Object input, params object[] objects)
        {
            if (objects.Length == 0)
                return 0;

            var hashCodesWithPrimes = tenPrimes.Zip(objects, (prime, obj) => prime * (obj == null ? 0 : obj.GetHashCode())).Aggregate((result, i) => result + i);
            if (objects.Length <= 10)
            {
                return hashCodesWithPrimes;
            }

            return objects.Skip(10).Aggregate(hashCodesWithPrimes, (result, obj) => result + (obj == null ? 0 : obj.GetHashCode()));    
        }

        public static T With<T>(this T item, Action<T> action, Action onNullOrDefault = null) {
            if (item == null || item.Equals(default(T))) {
                if (onNullOrDefault != null) {
                    onNullOrDefault();
                }
            }
            else {
                action(item);
            }
            return item;
        }

        public static U With<T,U>(this T item, Func<T,U> action, Func<U> onNullOrDefault = null) {
            return item.Equals(default(T)) ? (onNullOrDefault != null ? onNullOrDefault() : default(U)) : action(item);
        }

        public static object SimpleEval(this object instance, string simpleCode) {
            var cmd = simpleCode.Split('.');
            var subString = cmd[0];
            object returnValue;
            var t = instance.GetType();
            if (subString.Contains("(")) {
                var paramString = subString.Split('(');
                var parameters = paramString[1].Replace(")", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var hasNoParams = parameters.Length == 0;

                List<Type> typeArray = null;
                if (hasNoParams) {
                    typeArray = new List<Type>();
                }
                foreach (var parameter in parameters.Where(parameter => parameter.Contains(":"))) {
                    if (typeArray == null) {
                        typeArray = new List<Type>();
                    }
                    var typeValue = parameter.Split(':');
                    var paramType = Type.GetType(typeValue[0].Replace('_', '.'));

                    typeArray.Add(paramType);
                }
                var info = typeArray == null ? t.GetMethod(paramString[0]) : t.GetMethod(paramString[0], typeArray.ToArray());
                var pInfo = info.GetParameters();
                var paramList = new List<object>();
                for (var i = 0; i < pInfo.Length; i++) {
                    var currentParam = parameters[i];
                    if (currentParam.Contains(":")) {
                        currentParam = currentParam.Split(':')[1];
                    }
                    var pram = pInfo[i];
                    var pType = pram.ParameterType;
                    var obj = Convert.ChangeType(currentParam, pType);
                    paramList.Add(obj);
                }
                returnValue = info.Invoke(instance, paramList.ToArray());
            } else {
                var pi = t.GetProperty(subString);
                returnValue = pi == null ? null : pi.GetValue(instance, null);
            }
            if (returnValue == null || cmd.Length == 1) {
                return returnValue;
            }
            returnValue = SimpleEval(returnValue, simpleCode.Replace(cmd[0] + ".", ""));
            return returnValue;
        }
    }
}
