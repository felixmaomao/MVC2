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
    using System.Web.Mvc.Async;
    using System.Web.Mvc.Resources;
    using System.Web.Routing;

    //这个抽象类是接口之下最高的抽象类
    public abstract class ControllerBase : IController {

        private readonly SingleEntryGate _executeWasCalledGate = new SingleEntryGate();

        //临时数据字典？
        private TempDataDictionary _tempDataDictionary;
        private bool _validateRequest = true;
        private IValueProvider _valueProvider;
        private ViewDataDictionary _viewDataDictionary;

        public ControllerContext ControllerContext {
            get;
            set;
        }

        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly",
            Justification = "This property is settable so that unit tests can provide mock implementations.")]
        public TempDataDictionary TempData {
            get {
                if (ControllerContext != null && ControllerContext.IsChildAction) {
                    return ControllerContext.ParentActionViewContext.TempData;
                }
                if (_tempDataDictionary == null) {
                    _tempDataDictionary = new TempDataDictionary();
                }
                return _tempDataDictionary;
            }
            set {
                _tempDataDictionary = value;
            }
        }

        public bool ValidateRequest {
            get {
                return _validateRequest;
            }
            set {
                _validateRequest = value;
            }
        }

        //ValueProviders和ModelBinders 一起为actionMethod的参数提供数据
        public IValueProvider ValueProvider {
            get {
                if (_valueProvider == null) {
                    _valueProvider = ValueProviderFactories.Factories.GetValueProvider(ControllerContext);
                }
                return _valueProvider;
            }
            set {
                _valueProvider = value;
            }
        }

        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly",
            Justification = "This property is settable so that unit tests can provide mock implementations.")]
        public ViewDataDictionary ViewData {
            get {
                if (_viewDataDictionary == null) {
                    _viewDataDictionary = new ViewDataDictionary();
                }
                return _viewDataDictionary;
            }
            set {
                _viewDataDictionary = value;
            }
        }

        //通过这个虚方法作为对接口的实现，把抽象提取到 executecore中
        protected virtual void Execute(RequestContext requestContext) {
            if (requestContext == null) {
                throw new ArgumentNullException("requestContext");
            }
            //做一些相关验证
            VerifyExecuteCalledOnce();
            Initialize(requestContext);
            //把真正的核心工作转移到另一个抽象方法中
            ExecuteCore();
        }

        protected abstract void ExecuteCore();

        protected virtual void Initialize(RequestContext requestContext) {
            ControllerContext = new ControllerContext(requestContext, this);
        }

        internal void VerifyExecuteCalledOnce() {
            if (!_executeWasCalledGate.TryEnter()) {
                string message = String.Format(CultureInfo.CurrentUICulture, MvcResources.ControllerBase_CannotHandleMultipleRequests, GetType());
                throw new InvalidOperationException(message);
            }
        }

        //显示接口实现 EIMI
        #region IController Members
        void IController.Execute(RequestContext requestContext) {
            Execute(requestContext);
        }
        #endregion
    }
}
