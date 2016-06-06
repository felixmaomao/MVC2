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
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Web.Mvc.Resources;

    public class ControllerBuilder {
        //为什么要定义委托？（是为了写的更优美。因为创建fac的方式）
        private Func<IControllerFactory> _factoryThunk;
        //伪单例模式，这种方式也可以创建对象，但当你通过.current获取到的一定是同一个对象。
        private static ControllerBuilder _instance = new ControllerBuilder();
        private HashSet<string> _namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ControllerBuilder() {
            SetControllerFactory(new DefaultControllerFactory() {
                ControllerBuilder = this
            });
        }

        public static ControllerBuilder Current {
            get {
                return _instance;
            }
        }

        public HashSet<string> DefaultNamespaces {
            get {
                return _namespaces;
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate",
            Justification = "Calling method multiple times might return different objects.")]
        public IControllerFactory GetControllerFactory() {
            IControllerFactory controllerFactoryInstance = _factoryThunk();
            return controllerFactoryInstance;
        }

        //在mvc2.0中， 我们还是通过controllerbuilder.current.setcontrollerFactory来更换工厂的
        public void SetControllerFactory(IControllerFactory controllerFactory) {
            if (controllerFactory == null) {
                throw new ArgumentNullException("controllerFactory");
            }
            _factoryThunk = () => controllerFactory;
        }

        //这边还提供一共通过类型创建的重载
        public void SetControllerFactory(Type controllerFactoryType) {
            if (controllerFactoryType == null) {
                throw new ArgumentNullException("controllerFactoryType");
            }
            if (!typeof(IControllerFactory).IsAssignableFrom(controllerFactoryType)) {
                throw new ArgumentException(
                    String.Format(
                        CultureInfo.CurrentUICulture,
                        MvcResources.ControllerBuilder_MissingIControllerFactory,
                        controllerFactoryType),
                    "controllerFactoryType");
            }

            _factoryThunk = delegate() {
                try {
                    return (IControllerFactory)Activator.CreateInstance(controllerFactoryType);
                    }
                catch (Exception ex) {
                    throw new InvalidOperationException(
                        String.Format(
                            CultureInfo.CurrentUICulture,
                            MvcResources.ControllerBuilder_ErrorCreatingControllerFactory,
                            controllerFactoryType),
                        ex);
                }
            };
        }
    }
}
