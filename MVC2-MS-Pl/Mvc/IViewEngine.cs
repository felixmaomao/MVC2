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

    public interface IViewEngine {
        ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName, bool useCache);
        //真正寻找视图是由IViewEngine来实现的，最后一个参数意味着是否要使用缓存
        ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache);
        void ReleaseView(ControllerContext controllerContext, IView view);
    }
}
