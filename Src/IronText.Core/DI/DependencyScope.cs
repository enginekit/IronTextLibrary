﻿using System;
using System.Collections.Generic;

namespace IronText.DI
{
    internal class DependencyScope : IDependencyResolver, IDisposable
    {
        private readonly Dictionary<Type, Func<object>> typeToGetter 
            = new Dictionary<Type, Func<object>>();
        private DependencyScope parent;

        public DependencyScope()
            : this(null)
        { }

        public DependencyScope(DependencyScope parent)
        {
            this.parent = parent;
        }

        public object Resolve(Type type)
        {
            var getter = ResolveGetter(type);
            return getter();
        }

        public void Register<TImpl>()
        {
            Register(typeof(TImpl));
        }

        public void Register(Type impl)
        {
            var interfaces = impl.GetInterfaces();
      
            Register(impl, impl);

            foreach (var intf in interfaces)
            {
                Register(intf, impl);
            }
        }

        public void Register<TContract, TImpl>()
        {
            Register(typeof(TContract), typeof(TImpl));
        }

        public void Register(Type contract, Type impl)
        {
            var cs = impl.GetConstructors(); 
            if (cs.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{impl.FullName} should have exactly one public constructor.");
            }

            var c = cs[0];
            var ps = Array.ConvertAll(c.GetParameters(), p => p.ParameterType);

            Register(
                contract,
                () => c.Invoke(Array.ConvertAll(ps, Resolve)));
        }

        public void Register<T>(T instance)
        {
            if (typeof(T).IsInterface)
            {
                Register(typeof(T), () => instance);
            }
            else
            {
                Register((object)instance);
            }
        }

        public void Register(object instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var interfaces = instance.GetType().GetInterfaces();
            Register(instance.GetType(), () => instance);
            
            foreach (var intf in interfaces)
            {
                Register(intf, () => instance);
            }
        }

        public void Register<TContract,T1>(Func<T1,TContract> getter)
        {
            Register(typeof(TContract), (Delegate)getter);
        }

        public void Register<TContract,T1,T2>(Func<T1,T2,TContract> getter)
        {
            Register(typeof(TContract), (Delegate)getter);
        }

        public void Register<TContract,T1,T2,T3>(Func<T1,T2,T3,TContract> getter)
        {
            Register(typeof(TContract), (Delegate)getter);
        }
        
        public void Register(Type contract, Delegate parameterizedGetter)
        {
            var invoke = parameterizedGetter.GetType().GetMethod("Invoke");
            var ps = Array.ConvertAll(invoke.GetParameters(), p => p.ParameterType);
            Register(
                contract,
                () => parameterizedGetter.DynamicInvoke(
                        Array.ConvertAll(ps, Resolve)));
        }

        public DependencyScope Nest()
        {
            return new DependencyScope(this);
        }

        public void Register(Type contract, Func<object> getter)
        {
            if (typeToGetter.ContainsKey(contract))
            {
                throw new InvalidOperationException(
                    $"Dependency {contract.FullName} is already defined");
            }

            if (contract is IHasSideEffects)
            {
                typeToGetter[contract] = getter;
            }
            else
            {
                bool isMemoized = false;
                object memoized = null;
                typeToGetter[contract] = () =>
                {
                    if (!isMemoized)
                    {
                        isMemoized = true;
                        memoized = getter();
                    }

                    return memoized;
                };
            }
        }

        private Func<object> ResolveGetter(Type type)
        {
            Func<object> result;
            if (!typeToGetter.TryGetValue(type, out result)
                && !TryGetFromParent(type, out result)
                && !AutoRegister(type, out result))
            {
                throw new InvalidOperationException(
                    $"Unable to resolve dependency for type {type.FullName}.");
            }

            return result;
        }

        private bool TryResolveNoAuto(Type type, out Func<object> getter)
        {
            bool result = typeToGetter.TryGetValue(type, out getter)
                        || TryGetFromParent(type, out getter);
            return result;
        }

        private bool TryGetFromParent(Type type, out Func<object> getter)
        {
            if (parent == null)
            {
                getter = null;
                return false;
            }

            return parent.TryResolveNoAuto(type, out getter);
        }

        private bool AutoRegister(Type typeToResolve, out Func<object> getter)
        {
            var cs = typeToResolve.GetConstructors(); 
            if (cs.Length != 1)
            {
                getter = null;
                return false;
            }

            Register(typeToResolve, typeToResolve);
            getter = typeToGetter[typeToResolve];
            return true;
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DependencyScope()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
        }
    }
}