﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

namespace Shielded.ProxyGen
{
    /// <summary>
    /// Factory for generating Shielded proxy-subclasses for POCO classes.
    /// </summary>
    public static class Factory
    {
        /// <summary>
        /// Returns the proxy subtype for a given type. If arg already is a proxy type,
        /// then it just returns it directly. The proxy type implements backing storage
        /// for all virtual properties (with virtual getters and setters) in one
        /// Shielded struct container. Base setters will be called before any change,
        /// and may use the getter to obtain the old value.
        /// If the class has a virtual method Commute(Action), this will be overriden
        /// and the override will perform the action as a commute.
        /// If the class has a protected method OnChanged(string), this will be called
        /// after every property change, with the property name as argument.
        /// The subtype is generated once per type, and cached for future calls.
        /// </summary>
        public static Type ShieldedType(Type t)
        {
            return ProxyGen.GetFor(t);
        }

        private static ConcurrentDictionary<Type, Func<object>> _activators =
            new ConcurrentDictionary<Type, Func<object>>();

        private static Func<object> CreateActivator(Type t)
        {
            var dynamicMethod = new DynamicMethod("Ctor_" + t.FullName, t, Type.EmptyTypes, true);
            var generator = dynamicMethod.GetILGenerator();

            var constructor = t.GetConstructor(Type.EmptyTypes);
            generator.Emit(OpCodes.Newobj, constructor);
            generator.Emit(OpCodes.Ret);

            return (Func<object>)dynamicMethod.CreateDelegate(typeof(Func<object>));
        }

        /// <summary>
        /// Constructs a new instance of the subtype of T generated by
        /// <see cref="ShieldedType"/>. It implements backing storage
        /// for all virtual properties (with virtual getters and setters) in one
        /// Shielded struct container. Base setters will be called before any change,
        /// and may use the getter to obtain the old value.
        /// If the class has a virtual method Commute(Action), this will be overriden
        /// and the override will perform the action as a commute.
        /// If the class has a protected method OnChanged(string), this will be called
        /// after every property change, with the property name as argument.
        /// The subtype is generated once per type, and cached for future calls.
        /// </summary>
        public static T NewShielded<T>() where T : class, new()
        {
            return (T)_activators.GetOrAdd(ProxyGen.GetFor(typeof(T)), CreateActivator)();
        }

        /// <summary>
        /// Returns true if we can generate a proxy subclass for the given type.
        /// It would be recommended not to use this when preparing types - try to
        /// prepare every type you think should be prepared. That way, if one of your
        /// types is not OK, you will get an exception at preparation time, rather
        /// than later, when some piece of code tries to use the factory to construct
        /// one of those types.
        /// </summary>
        public static bool CanGenerateProxy(Type t)
        {
            return !NothingToDo.With(t);
        }

        /// <summary>
        /// Returns true if the type is a shielded proxy type.
        /// </summary>
        public static bool IsProxy(Type t)
        {
            return ProxyGen.IsProxy(t);
        }

        /// <summary>
        /// Prepares proxy subclasses for given types, all in one new assembly. (Normally,
        /// when doing one by one, each gets its own assembly.) If any of the types is
        /// not suitable, you will get an <see cref="InvalidOperationException"/>.
        /// </summary>
        public static void PrepareTypes(Type[] types)
        {
            ProxyGen.Prepare(types);
            foreach (var t in types.Where(typ => !_activators.ContainsKey(typ)))
            {
                var shT = ShieldedType(t);
                _activators.TryAdd(shT, CreateActivator(shT));
            }
        }
    }
}
