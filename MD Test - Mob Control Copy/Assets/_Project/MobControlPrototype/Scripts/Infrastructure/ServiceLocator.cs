using System;
using System.Collections.Generic;

namespace MobControlPrototype.Infrastructure
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>(8);

        public static void Register<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Services[typeof(TService)] = service;
        }

        public static TService Get<TService>() where TService : class
        {
            if (TryGet(out TService service))
            {
                return service;
            }

            throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered.");
        }

        public static bool TryGet<TService>(out TService service) where TService : class
        {
            if (Services.TryGetValue(typeof(TService), out object value))
            {
                service = (TService)value;
                return true;
            }

            service = null;
            return false;
        }

        public static void Clear()
        {
            Services.Clear();
        }
    }
}
