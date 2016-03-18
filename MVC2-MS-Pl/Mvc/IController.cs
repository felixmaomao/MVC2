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
    using System.Web.Routing;

    public interface IController {
        //就定义了这样一个简单的方法。
        //这是整个控制器继承体系的最高抽象。
        //requestContext包含了整个请求相关的的信息
        //前面的路由模块会提供这里所需要的参数
        void Execute(RequestContext requestContext);
    }
}
