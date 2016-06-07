﻿/* ****************************************************************************
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
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Web;
    using System.Web.Mvc.Resources;

    public class ControllerActionInvoker : IActionInvoker {

        private readonly static ControllerDescriptorCache _staticDescriptorCache = new ControllerDescriptorCache();

        private ModelBinderDictionary _binders;
        private ControllerDescriptorCache _instanceDescriptorCache;

        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly",
            Justification = "Property is settable so that the dictionary can be provided for unit testing purposes.")]
        protected internal ModelBinderDictionary Binders {
            get {
                if (_binders == null) {
                    _binders = ModelBinders.Binders;
                }
                return _binders;
            }
            set {
                _binders = value;
            }
        }

        internal ControllerDescriptorCache DescriptorCache {
            get {
                if (_instanceDescriptorCache == null) {
                    _instanceDescriptorCache = _staticDescriptorCache;
                }
                return _instanceDescriptorCache;
            }
            set {
                _instanceDescriptorCache = value;
            }
        }

        private static void AddControllerToFilterList<TFilter>(ControllerBase controller, IList<TFilter> filterList) where TFilter : class {
            TFilter controllerAsFilter = controller as TFilter;
            if (controllerAsFilter != null) {
                filterList.Insert(0, controllerAsFilter);
            }
        }

        protected virtual ActionResult CreateActionResult(ControllerContext controllerContext, ActionDescriptor actionDescriptor, object actionReturnValue) {
            if (actionReturnValue == null) {
                return new EmptyResult();
            }

            ActionResult actionResult = (actionReturnValue as ActionResult) ??
                new ContentResult { Content = Convert.ToString(actionReturnValue, CultureInfo.InvariantCulture) };
            return actionResult;
        }

        //这个方法用来获得controllerDescriptor  这些descriptor主要是处理特性。
        protected virtual ControllerDescriptor GetControllerDescriptor(ControllerContext controllerContext) {
            Type controllerType = controllerContext.Controller.GetType();
            ControllerDescriptor controllerDescriptor = DescriptorCache.GetDescriptor(controllerType, () => new ReflectedControllerDescriptor(controllerType));
            return controllerDescriptor;
        }
        //这个方法获得actionDescriptor
        protected virtual ActionDescriptor FindAction(ControllerContext controllerContext, ControllerDescriptor controllerDescriptor, string actionName) {
            ActionDescriptor actionDescriptor = controllerDescriptor.FindAction(controllerContext, actionName);
            return actionDescriptor;
        }

        protected virtual FilterInfo GetFilters(ControllerContext controllerContext, ActionDescriptor actionDescriptor) {
            FilterInfo filters = actionDescriptor.GetFilters();

            // if the current controller implements one of the filter interfaces, it should be added to the list at position 0
            ControllerBase controller = controllerContext.Controller;
            AddControllerToFilterList(controller, filters.ActionFilters);
            AddControllerToFilterList(controller, filters.ResultFilters);
            AddControllerToFilterList(controller, filters.AuthorizationFilters);
            AddControllerToFilterList(controller, filters.ExceptionFilters);

            return filters;
        }

        private IModelBinder GetModelBinder(ParameterDescriptor parameterDescriptor) {
            // look on the parameter itself, then look in the global table
            return parameterDescriptor.BindingInfo.Binder ?? Binders.GetBinder(parameterDescriptor.ParameterType);
        }

        protected virtual object GetParameterValue(ControllerContext controllerContext, ParameterDescriptor parameterDescriptor) {
            // collect all of the necessary binding properties
            Type parameterType = parameterDescriptor.ParameterType;
            IModelBinder binder = GetModelBinder(parameterDescriptor);
            IValueProvider valueProvider = controllerContext.Controller.ValueProvider;
            string parameterName = parameterDescriptor.BindingInfo.Prefix ?? parameterDescriptor.ParameterName;
            Predicate<string> propertyFilter = GetPropertyFilter(parameterDescriptor);

            // finally, call into the binder
            ModelBindingContext bindingContext = new ModelBindingContext() {
                FallbackToEmptyPrefix = (parameterDescriptor.BindingInfo.Prefix == null), // only fall back if prefix not specified
                ModelMetadata = ModelMetadataProviders.Current.GetMetadataForType(null, parameterType),
                ModelName = parameterName,
                ModelState = controllerContext.Controller.ViewData.ModelState,
                PropertyFilter = propertyFilter,
                ValueProvider = valueProvider
            };

            object result = binder.BindModel(controllerContext, bindingContext);
            return result ?? parameterDescriptor.DefaultValue;
        }

        protected virtual IDictionary<string, object> GetParameterValues(ControllerContext controllerContext, ActionDescriptor actionDescriptor) {
            Dictionary<string, object> parametersDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            ParameterDescriptor[] parameterDescriptors = actionDescriptor.GetParameters();

            foreach (ParameterDescriptor parameterDescriptor in parameterDescriptors) {
                parametersDict[parameterDescriptor.ParameterName] = GetParameterValue(controllerContext, parameterDescriptor);
            }
            return parametersDict;
        }

        private static Predicate<string> GetPropertyFilter(ParameterDescriptor parameterDescriptor) {
            ParameterBindingInfo bindingInfo = parameterDescriptor.BindingInfo;
            return propertyName => BindAttribute.IsPropertyAllowed(propertyName, bindingInfo.Include.ToArray(), bindingInfo.Exclude.ToArray());
        }

        //这边是执行部分
        public virtual bool InvokeAction(ControllerContext controllerContext, string actionName) {
            if (controllerContext == null) {
                throw new ArgumentNullException("controllerContext");
            }
            if (String.IsNullOrEmpty(actionName)) {
                throw new ArgumentException(MvcResources.Common_NullOrEmpty, "actionName");
            }
            //调用上面的方法 获得controllerDescriptor和actionDescriptor
            ControllerDescriptor controllerDescriptor = GetControllerDescriptor(controllerContext);
            ActionDescriptor actionDescriptor = FindAction(controllerContext, controllerDescriptor, actionName);
            //找到对应的action了
            if (actionDescriptor != null) {
                FilterInfo filterInfo = GetFilters(controllerContext, actionDescriptor);

                try {
                    AuthorizationContext authContext = InvokeAuthorizationFilters(controllerContext, filterInfo.AuthorizationFilters, actionDescriptor);
                    if (authContext.Result != null) {
                        // the auth filter signaled that we should let it short-circuit the request
                        InvokeActionResult(controllerContext, authContext.Result);
                    }
                    else {
                        if (controllerContext.Controller.ValidateRequest) {
                            ValidateRequest(controllerContext);
                        }
                        IDictionary<string, object> parameters = GetParameterValues(controllerContext, actionDescriptor);
                        ActionExecutedContext postActionContext = InvokeActionMethodWithFilters(controllerContext, filterInfo.ActionFilters, actionDescriptor, parameters);
                        InvokeActionResultWithFilters(controllerContext, filterInfo.ResultFilters, postActionContext.Result);
                    }
                }
                catch (ThreadAbortException) {
                    // This type of exception occurs as a result of Response.Redirect(), but we special-case so that
                    // the filters don't see this as an error.
                    throw;
                }
                catch (Exception ex) {
                    // something blew up, so execute the exception filters
                    ExceptionContext exceptionContext = InvokeExceptionFilters(controllerContext, filterInfo.ExceptionFilters, ex);
                    if (!exceptionContext.ExceptionHandled) {
                        throw;
                    }
                    InvokeActionResult(controllerContext, exceptionContext.Result);
                }

                return true;
            }
            //没有找到对应的action
            // notify controller that no method matched
            return false;
        }

        #region 根据filter查看争取到的执行环境
        protected virtual ActionResult InvokeActionMethod(ControllerContext controllerContext, ActionDescriptor actionDescriptor, IDictionary<string, object> parameters) {
            object returnValue = actionDescriptor.Execute(controllerContext, parameters);
            ActionResult result = CreateActionResult(controllerContext, actionDescriptor, returnValue);
            return result;
        }

        internal static ActionExecutedContext InvokeActionMethodFilter(IActionFilter filter, ActionExecutingContext preContext, Func<ActionExecutedContext> continuation) {
            filter.OnActionExecuting(preContext);
            if (preContext.Result != null) {
                return new ActionExecutedContext(preContext, preContext.ActionDescriptor, true /* canceled */, null /* exception */) {
                    Result = preContext.Result
                };
            }

            bool wasError = false;
            ActionExecutedContext postContext = null;
            try {
                postContext = continuation();
            }
            catch (ThreadAbortException) {
                // This type of exception occurs as a result of Response.Redirect(), but we special-case so that
                // the filters don't see this as an error.
                postContext = new ActionExecutedContext(preContext, preContext.ActionDescriptor, false /* canceled */, null /* exception */);
                filter.OnActionExecuted(postContext);
                throw;
            }
            catch (Exception ex) {
                wasError = true;
                postContext = new ActionExecutedContext(preContext, preContext.ActionDescriptor, false /* canceled */, ex);
                filter.OnActionExecuted(postContext);
                if (!postContext.ExceptionHandled) {
                    throw;
                }
            }
            if (!wasError) {
                filter.OnActionExecuted(postContext);
            }
            return postContext;
        }

        protected virtual ActionExecutedContext InvokeActionMethodWithFilters(ControllerContext controllerContext, IList<IActionFilter> filters, ActionDescriptor actionDescriptor, IDictionary<string, object> parameters) {
            ActionExecutingContext preContext = new ActionExecutingContext(controllerContext, actionDescriptor, parameters);
            Func<ActionExecutedContext> continuation = () =>
                new ActionExecutedContext(controllerContext, actionDescriptor, false /* canceled */, null /* exception */) {
                    Result = InvokeActionMethod(controllerContext, actionDescriptor, parameters)
                };

            // need to reverse the filter list because the continuations are built up backward
            Func<ActionExecutedContext> thunk = filters.Reverse().Aggregate(continuation,
                (next, filter) => () => InvokeActionMethodFilter(filter, preContext, next));
            return thunk();
        }

        protected virtual void InvokeActionResult(ControllerContext controllerContext, ActionResult actionResult) {
            actionResult.ExecuteResult(controllerContext);
        }

        internal static ResultExecutedContext InvokeActionResultFilter(IResultFilter filter, ResultExecutingContext preContext, Func<ResultExecutedContext> continuation) {
            filter.OnResultExecuting(preContext);
            if (preContext.Cancel) {
                return new ResultExecutedContext(preContext, preContext.Result, true /* canceled */, null /* exception */);
            }

            bool wasError = false;
            ResultExecutedContext postContext = null;
            try {
                postContext = continuation();
            }
            catch (ThreadAbortException) {
                // This type of exception occurs as a result of Response.Redirect(), but we special-case so that
                // the filters don't see this as an error.
                postContext = new ResultExecutedContext(preContext, preContext.Result, false /* canceled */, null /* exception */);
                filter.OnResultExecuted(postContext);
                throw;
            }
            catch (Exception ex) {
                wasError = true;
                postContext = new ResultExecutedContext(preContext, preContext.Result, false /* canceled */, ex);
                filter.OnResultExecuted(postContext);
                if (!postContext.ExceptionHandled) {
                    throw;
                }
            }
            if (!wasError) {
                filter.OnResultExecuted(postContext);
            }
            return postContext;
        }

        protected virtual ResultExecutedContext InvokeActionResultWithFilters(ControllerContext controllerContext, IList<IResultFilter> filters, ActionResult actionResult) {
            ResultExecutingContext preContext = new ResultExecutingContext(controllerContext, actionResult);
            Func<ResultExecutedContext> continuation = delegate {
                InvokeActionResult(controllerContext, actionResult);
                return new ResultExecutedContext(controllerContext, actionResult, false /* canceled */, null /* exception */);
            };

            // need to reverse the filter list because the continuations are built up backward
            Func<ResultExecutedContext> thunk = filters.Reverse().Aggregate(continuation,
                (next, filter) => () => InvokeActionResultFilter(filter, preContext, next));
            return thunk();
        }

        protected virtual AuthorizationContext InvokeAuthorizationFilters(ControllerContext controllerContext, IList<IAuthorizationFilter> filters, ActionDescriptor actionDescriptor) {
            AuthorizationContext context = new AuthorizationContext(controllerContext, actionDescriptor);
            foreach (IAuthorizationFilter filter in filters) {
                filter.OnAuthorization(context);
                // short-circuit evaluation
                if (context.Result != null) {
                    break;
                }
            }

            return context;
        }

        protected virtual ExceptionContext InvokeExceptionFilters(ControllerContext controllerContext, IList<IExceptionFilter> filters, Exception exception) {
            ExceptionContext context = new ExceptionContext(controllerContext, exception);
            foreach (IExceptionFilter filter in filters) {
                filter.OnException(context);
            }

            return context;
        }

        [SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "rawUrl",
            Justification = "We only care about the property getter's side effects, not the returned value.")]
        internal static void ValidateRequest(ControllerContext controllerContext) {
            if (controllerContext.IsChildAction) {
                return;
            }

            // DevDiv 214040: Enable Request Validation by default for all controller requests
            // 
            // Note that we grab the Request's RawUrl to force it to be validated. Calling ValidateInput()
            // doesn't actually validate anything. It just sets flags indicating that on the next usage of
            // certain inputs that they should be validated. We special case RawUrl because the URL has already
            // been consumed by routing and thus might contain dangerous data. By forcing the RawUrl to be
            // re-read we're making sure that it gets validated by ASP.NET.

            controllerContext.HttpContext.Request.ValidateInput();
            string rawUrl = controllerContext.HttpContext.Request.RawUrl;
        }
        #endregion
    }
}
