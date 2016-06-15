/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. All rights reserved.
 *
 * This software is subject to the Microsoft Public License (Ms-PL). 
 * A copy of the license can be found in the license.htm file included 
 * in this distribution.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

namespace System.Web.Mvc {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    //以后写缓存也可以模仿这个工具类
    internal static class TypeCacheUtil {

        //在没有缓存的情况下 遍历所有的dll，得到所有符合要求的类型
        //公开 且有另外的要求（外界提供：即是controller）
        //这边写的真美
        private static IEnumerable<Type> FilterTypesInAssemblies(IBuildManager buildManager, Predicate<Type> predicate) {
            // Go through all assemblies referenced by the application and search for types matching a predicate
            IEnumerable<Type> typesSoFar = Type.EmptyTypes;

            ICollection assemblies = buildManager.GetReferencedAssemblies();
            foreach (Assembly assembly in assemblies) {
                Type[] typesInAsm;
                try {
                    typesInAsm = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex) {
                    typesInAsm = ex.Types;
                }
                //对于两个IEnumerable的拼接
                typesSoFar = typesSoFar.Concat(typesInAsm);
            }
            return typesSoFar.Where(type => TypeIsPublicClass(type) && predicate(type));
        }

        public static List<Type> GetFilteredTypesFromAssemblies(string cacheName, Predicate<Type> predicate, IBuildManager buildManager) {
            TypeCacheSerializer serializer = new TypeCacheSerializer();

            // first, try reading from the cache on disk
            List<Type> matchingTypes = ReadTypesFromCache(cacheName, predicate, buildManager, serializer);
            if (matchingTypes != null) {
                return matchingTypes;
            }
            //从所有的dll中寻找。然后把找到的“珍贵”的结果放入缓存
            // if reading from the cache failed, enumerate over every assembly looking for a matching type
            matchingTypes = FilterTypesInAssemblies(buildManager, predicate).ToList();

            // finally, save the cache back to disk
            SaveTypesToCache(cacheName, matchingTypes, buildManager, serializer);

            return matchingTypes;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Cache failures are not fatal, and the code should continue executing normally.")]
        internal static List<Type> ReadTypesFromCache(string cacheName, Predicate<Type> predicate, IBuildManager buildManager, TypeCacheSerializer serializer) {
            try {
                using (Stream stream = buildManager.ReadCachedFile(cacheName)) {
                    if (stream != null) {
                        using (StreamReader reader = new StreamReader(stream)) {
                            List<Type> deserializedTypes = serializer.DeserializeTypes(reader);
                            if (deserializedTypes != null && deserializedTypes.All(type => TypeIsPublicClass(type) && predicate(type))) {
                                // If all read types still match the predicate, success!
                                return deserializedTypes;
                            }
                        }
                    }
                }
            }
            catch {
            }
            return null;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Cache failures are not fatal, and the code should continue executing normally.")]
        //存入缓存文件。需要缓存的文件名，需要缓存的集合，，实现序列化的类
            //至于怎么放入缓存，这里采用的是xml序列化
        internal static void SaveTypesToCache(string cacheName, IList<Type> matchingTypes, IBuildManager buildManager, TypeCacheSerializer serializer) {
            try {
                using (Stream stream = buildManager.CreateCachedFile(cacheName)) {
                    if (stream != null) {
                        using (StreamWriter writer = new StreamWriter(stream)) {
                            serializer.SerializeTypes(matchingTypes, writer);
                        }
                    }
                }
            }
            catch {
            }
        }

        private static bool TypeIsPublicClass(Type type) {
            return (type != null && type.IsPublic && type.IsClass && !type.IsAbstract);
        }

    }
}
