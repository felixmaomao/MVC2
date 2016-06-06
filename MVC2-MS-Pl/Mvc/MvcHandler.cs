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
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Web;
    using System.Web.Mvc.Async;
    using System.Web.Mvc.Resources;
    using System.Web.Routing;
    using System.Web.SessionState;

    [SuppressMessage("Microsoft.Security", "CA2112:SecuredTypesShouldNotExposeFields", Justification = "There's nothing secret about the value of this field.")]
    public class MvcHandler : IHttpAsyncHandler, IHttpHandler, IRequiresSessionState {
        private static readonly object _processRequestTag = new object();
        private ControllerBuilder _controllerBuilder;

        internal static readonly string MvcVersion = GetMvcVersionString();
        public static readonly string MvcVersionHeaderName = "X-AspNetMvc-Version";

        public MvcHandler(RequestContext requestContext) {
            if (requestContext == null) {
                throw new ArgumentNullException("requestContext");
            }
            RequestContext = requestContext;
        }

        internal ControllerBuilder ControllerBuilder {
            get {
                if (_controllerBuilder == null) {
                    _controllerBuilder = ControllerBuilder.Current;
                }
                return _controllerBuilder;
            }
            set {
                _controllerBuilder = value;
            }
        }

        public static bool DisableMvcResponseHeader {
            get;
            set;
        }

        protected virtual bool IsReusable {
            get {
                return false;
            }
        }

        public RequestContext RequestContext {
            get;
            private set;
        }

        protected internal virtual void AddVersionHeader(HttpContextBase httpContext) {
            if (!DisableMvcResponseHeader) {
                httpContext.Response.AppendHeader(MvcVersionHeaderName, MvcVersion);
            }
        }

        protected virtual IAsyncResult BeginProcessRequest(HttpContext httpContext, AsyncCallback callback, object state) {
            HttpContextBase iHttpContext = new HttpContextWrapper(httpContext);
            return BeginProcessRequest(iHttpContext, callback, state);
        }

        protected internal virtual IAsyncResult BeginProcessRequest(HttpContextBase httpContext, AsyncCallback callback, object state) {
            IController controller;
            IControllerFactory factory;
            ProcessRequestInit(httpContext, out controller, out factory);

            IAsyncController asyncController = controller as IAsyncController;
            if (asyncController != null) {
                // asynchronous controller
                BeginInvokeDelegate beginDelegate = delegate(AsyncCallback asyncCallback, object asyncState) {
                    try {
                        return asyncController.BeginExecute(RequestContext, asyncCallback, asyncState);
                    }
                    catch {
                        factory.ReleaseController(asyncController);
                        throw;
                    }
                };

                EndInvokeDelegate endDelegate = delegate(IAsyncResult asyncResult) {
                    try {
                        asyncController.EndExecute(asyncResult);
                    }
                    finally {
                        factory.ReleaseController(asyncController);
                    }
                };

                SynchronizationContext syncContext = SynchronizationContextUtil.GetSynchronizationContext();
                AsyncCallback newCallback = AsyncUtil.WrapCallbackForSynchronizedExecution(callback, syncContext);
                return AsyncResultWrapper.Begin(newCallback, state, beginDelegate, endDelegate, _processRequestTag);
            }
            else {
                // synchronous controller
                Action action = delegate {
                    try {
                        controller.Execute(RequestContext);
                    }
                    finally {
                        factory.ReleaseController(controller);
                    }
                };

                return AsyncResultWrapper.BeginSynchronous(callback, state, action, _processRequestTag);
            }
        }

        protected internal virtual void EndProcessRequest(IAsyncResult asyncResult) {
            AsyncResultWrapper.End(asyncResult, _processRequestTag);
        }

        private static string GetMvcVersionString() {
            // DevDiv 216459:
            // This code originally used Assembly.GetName(), but that requires FileIOPermission, which isn't granted in
            // medium trust. However, Assembly.FullName *is* accessible in medium trust.
            return new AssemblyName(typeof(MvcHandler).Assembly.FullName).Version.ToString(2);
        }

        protected virtual void ProcessRequest(HttpContext httpContext) {
            HttpContextBase iHttpContext = new HttpContextWrapper(httpContext);
            ProcessRequest(iHttpContext);
        }

        //这边是对于处理的实现，而真正的实现肯定是通过不同的controller来实现的
        //把工作分包到不同的controller，而这些不同的controller又是通过工厂创建的
        //当然看你可以完全写自己的工厂。找到想要的controller之后，由其进行处理
        //事实上大部分的寻找以及处理已经由基类controller完成了，我们后续自定义的xxxController更像是action方法的容器
        protected internal virtual void ProcessRequest(HttpContextBase httpContext) {
            //这种写法阐释了out的意义，定义好controller 以及工厂 然后通过out 来赋值。怎么赋值？反射。
            //写的好随性
            IController controller;
            IControllerFactory factory;       
            ProcessRequestInit(httpContext, out controller, out factory);
            try {
                //整个controller必定要有强大的继承结构才能完成这些强大的功能
                controller.Execute(RequestContext);
            }
            finally {
                factory.ReleaseController(controller);
            }
        }

        private void ProcessRequestInit(HttpContextBase httpContext, out IController controller, out IControllerFactory factory) {
            AddVersionHeader(httpContext);
            RemoveOptionalRoutingParameters();

            // Get the controller type
            string controllerName = RequestContext.RouteData.GetRequiredString("controller");

            // Instantiate the controller and call Execute
            //是通过controllerBuilder来找controller工厂的，为什么不是直接 new defaultControllerFactory()
            //是因为这边要开放出去，可能使用第三方工厂。
            factory = ControllerBuilder.GetControllerFactory();   
            controller = factory.CreateController(RequestContext, controllerName);
            //差距 在细节。比如异常处理这边
            if (controller == null) {
                throw new InvalidOperationException(
                    String.Format(
                        CultureInfo.CurrentUICulture,
                        MvcResources.ControllerBuilder_FactoryReturnedNull,
                        factory.GetType(),
                        controllerName));
            }
        }

        private void RemoveOptionalRoutingParameters() {
            RouteValueDictionary rvd = RequestContext.RouteData.Values;

            // Get all keys for which the corresponding value is 'Optional'.
            // ToArray() necessary so that we don't manipulate the dictionary while enumerating.
            string[] matchingKeys = (from entry in rvd
                                     where entry.Value == UrlParameter.Optional
                                     select entry.Key).ToArray();

            foreach (string key in matchingKeys) {
                rvd.Remove(key);
            }
        }

        #region IHttpHandler Members
        bool IHttpHandler.IsReusable {
            get {
                return IsReusable;
            }
        }

        void IHttpHandler.ProcessRequest(HttpContext httpContext) {
            ProcessRequest(httpContext);
        }
        #endregion

        #region IHttpAsyncHandler Members
        IAsyncResult IHttpAsyncHandler.BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData) {
            return BeginProcessRequest(context, cb, extraData);
        }

        void IHttpAsyncHandler.EndProcessRequest(IAsyncResult result) {
            EndProcessRequest(result);
        }
        #endregion
    }
}
