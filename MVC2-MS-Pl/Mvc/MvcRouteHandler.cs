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
    using System.Web.Routing;  //mvc是建立在 路由系统上的

    //这边是由路由系统过渡到mvc中的类，通过在路由模块注册之后就或直接绑定MvcRouteHandler进入mvc模块
    //当然 也可以自定义 IRouteHandler的实现，这样的话就根本不会进入MVC系统，而是进入自己的实现。
    //在MVCrouteHandler里面会直接得到一个IhttpHandler （内部的MVCHandler实现）

   
    //你会发现 用于携带数据的都喜欢叫做context 如requestContext还有 EF中的DataContext.

    public class MvcRouteHandler : IRouteHandler {
        protected virtual IHttpHandler GetHttpHandler(RequestContext requestContext) {
            return new MvcHandler(requestContext);    //不知道这边写死的 会不会耦合度太高
        }

        #region IRouteHandler Members
        IHttpHandler IRouteHandler.GetHttpHandler(RequestContext requestContext) {
            return GetHttpHandler(requestContext);
        }
        #endregion
    }
}
