using System;
using System.Collections.Concurrent;

namespace MagicEntry.Core.Services
{
    /// <summary>
    /// Статический провайдер сервисов для системы MagicEntry.
    /// Обеспечивает централизованный доступ к сервисам через dependency injection pattern.
    /// Потокобезопасен для использования в многопоточной среде.
    /// </summary>
    public static class ServiceProvider
    {
        #region Fields

        private static readonly ConcurrentDictionary<Type, object> _services = new ConcurrentDictionary<Type, object>();
        private static readonly object _lockObject = new object();

        #endregion

        #region Public Methods

        /// <summary>
        /// Получает экземпляр сервиса указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип сервиса для получения</typeparam>
        /// <returns>Экземпляр сервиса или null, если сервис не зарегистрирован</returns>
        public static T GetService<T>() where T : class
        {
            var serviceType = typeof(T);
            return _services.TryGetValue(serviceType, out var service) ? service as T : null;
        }

        /// <summary>
        /// Регистрирует экземпляр сервиса в провайдере.
        /// </summary>
        /// <typeparam name="T">Тип сервиса для регистрации</typeparam>
        /// <param name="service">Экземпляр сервиса</param>
        public static void RegisterService<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var serviceType = typeof(T);
            lock (_lockObject)
            {
                _services.AddOrUpdate(serviceType, service, (key, oldValue) => service);
            }
        }

        /// <summary>
        /// Проверяет, зарегистрирован ли сервис указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип сервиса для проверки</typeparam>
        /// <returns>True, если сервис зарегистрирован, иначе false</returns>
        public static bool IsServiceRegistered<T>() where T : class
        {
            var serviceType = typeof(T);
            return _services.ContainsKey(serviceType);
        }

        /// <summary>
        /// Удаляет регистрацию сервиса указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип сервиса для удаления</typeparam>
        /// <returns>True, если сервис был удален, иначе false</returns>
        public static bool UnregisterService<T>() where T : class
        {
            var serviceType = typeof(T);
            lock (_lockObject)
            {
                return _services.TryRemove(serviceType, out _);
            }
        }

        /// <summary>
        /// Очищает все зарегистрированные сервисы.
        /// </summary>
        public static void ClearAllServices()
        {
            lock (_lockObject)
            {
                _services.Clear();
            }
        }

        #endregion
    }
}