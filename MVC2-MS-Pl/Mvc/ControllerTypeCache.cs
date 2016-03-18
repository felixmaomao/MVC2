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
    using System.Linq;
    using System.Reflection;

//       ControllerTypeCache._cache 保存了所有载入程序集中的 Controller 类型;
//              EnsureInitialized方法:依据 "controllerName(不包含 Controller 字符串)"和 对应的Namespace 进行二级分组。
//              ControllerTypeCache.GetControllerTypes() 首先通过 controllerName 提取一级分组，然后用 namespaces 参数提取最终的类型数组。如果没有提供 Namespaces 则返回所有的同名 ControllerType。

//              分组示例：
//Home
//  Namespace1
//    Namespace1.HomeController
//GuestBoke
//  Namespace1
//    Namespace1.GuestBokeController
//  Namespace2
//    Namespace2.GuestbokeController 


    //这边属于mvc中的缓存机制。
    internal sealed class ControllerTypeCache {

        //缓存的文件名称
        private const string _typeCacheName = "MVC-ControllerTypeCache.xml";
        //这个字典的string存放的是不带命名空间的controllername
        //ilookup 中的string 存放的是命名空间的名称
        
        //_cache保存了 所有载入程序集的controller类，这边相当于是二级字典，联系上面的分组示例
        //可是为什么要分成二级字典？有什么好处?
        //虽然说下面这个名字叫做缓存，但真正缓存的并不是下面这个对象，下面这个是从缓存中分组提取出来的对象。
        //但是也很奇怪，为什么不对分组之后的字典进行缓存 而要对 整个list进行缓存呢？
        //因为事情没有想象的那么简单，我们的整个系统的所有controller类型可能分布在不同的dll中，要想得到一个完整的 IList<Type>已经需要很复杂的步骤了。
        //所以对其进行缓存。
        private Dictionary<string, ILookup<string, Type>> _cache;
        private object _lockObj = new object();


        //二级字典算总数
        internal int Count {
            get {
                int count = 0;
                foreach (var lookup in _cache.Values) {
                    foreach (var grouping in lookup) {
                        count += grouping.Count();
                    }
                }
                return count;
            }
        }

        public void EnsureInitialized(IBuildManager buildManager) {
            if (_cache == null) {
                lock (_lockObj) {
                    if (_cache == null) {
                        //将一个集合装入奇怪的二级字典的方法
                        //这边才是真正缓存中提取出来的对象。
                        List<Type> controllerTypes = TypeCacheUtil.GetFilteredTypesFromAssemblies(_typeCacheName, IsControllerType, buildManager);
                        var groupedByName = controllerTypes.GroupBy(
                            t => t.Name.Substring(0, t.Name.Length - "Controller".Length),
                            StringComparer.OrdinalIgnoreCase);
                        _cache = groupedByName.ToDictionary(
                            g => g.Key,
                            g => g.ToLookup(t => t.Namespace ?? String.Empty, StringComparer.OrdinalIgnoreCase),
                            StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
        }

        public ICollection<Type> GetControllerTypes(string controllerName, HashSet<string> namespaces) {
            HashSet<Type> matchingTypes = new HashSet<Type>();
            ILookup<string, Type> nsLookup;
            if (_cache.TryGetValue(controllerName, out nsLookup)) {
                // this friendly name was located in the cache, now cycle through namespaces
                if (namespaces != null) {
                    foreach (string requestedNamespace in namespaces) {
                        foreach (var targetNamespaceGrouping in nsLookup) {
                            if (IsNamespaceMatch(requestedNamespace, targetNamespaceGrouping.Key)) {
                                matchingTypes.UnionWith(targetNamespaceGrouping);
                            }
                        }
                    }
                }
                else {
                    // if the namespaces parameter is null, search *every* namespace
                    foreach (var nsGroup in nsLookup) {
                        matchingTypes.UnionWith(nsGroup);
                    }
                }
            }
            return matchingTypes;
        }

        //这边和Predict委托匹配。判断是否是 controller。
        //这种把条件单独写的写法配合Predict还是相当值得学习的。特别是当条件复杂易变的时候，分隔开会使代码更加清晰。
        internal static bool IsControllerType(Type t) {
            return
                t != null &&
                t.IsPublic &&
                t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) &&
                !t.IsAbstract &&
                typeof(IController).IsAssignableFrom(t);
        }
        internal static bool IsNamespaceMatch(string requestedNamespace, string targetNamespace) {
            // degenerate cases
            if (requestedNamespace == null) {
                return false;
            }
            else if (requestedNamespace.Length == 0) {
                return true;
            }

            if (!requestedNamespace.EndsWith(".*", StringComparison.OrdinalIgnoreCase)) {
                // looking for exact namespace match
                return String.Equals(requestedNamespace, targetNamespace, StringComparison.OrdinalIgnoreCase);
            }
            else {
                // looking for exact or sub-namespace match
                requestedNamespace = requestedNamespace.Substring(0, requestedNamespace.Length - ".*".Length);
                if (!targetNamespace.StartsWith(requestedNamespace, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                if (requestedNamespace.Length == targetNamespace.Length) {
                    // exact match
                    return true;
                }
                else if (targetNamespace[requestedNamespace.Length] == '.') {
                    // good prefix match, e.g. requestedNamespace = "Foo.Bar" and targetNamespace = "Foo.Bar.Baz"
                    return true;
                }
                else {
                    // bad prefix match, e.g. requestedNamespace = "Foo.Bar" and targetNamespace = "Foo.Bar2"
                    return false;
                }
            }
        }

    }
}
