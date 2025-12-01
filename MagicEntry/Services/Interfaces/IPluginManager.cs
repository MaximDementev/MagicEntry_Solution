using Autodesk.Revit.UI;
using MagicEntry.Core.Interfaces;
using System.Collections.Generic;

namespace MagicEntry.Services
{
    /// <summary>
    /// Определяет контракт для сервиса, управляющего жизненным циклом плагинов:
    /// их загрузкой, инициализацией, созданием UI и завершением работы.
    /// </summary>
    public interface IPluginManager
    {
        #region Properties

        /// <summary>
        /// Получает коллекцию всех загруженных плагинов (реализующих IPlugin).
        /// </summary>
        IReadOnlyCollection<IPlugin> LoadedPlugins { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Загружает плагины на основе указанного конфигурационного файла и базового пути.
        /// </summary>
        /// <param name="configurationPath">Путь к файлу конфигурации плагинов.</param>
        /// <param name="basePath">Базовый путь для разрешения относительных путей к сборкам плагинов.</param>
        void LoadPlugins(string configurationPath, string basePath);

        /// <summary>
        /// Инициализирует все загруженные плагины (вызывает их метод Initialize)
        /// и создает для них элементы пользовательского интерфейса в Revit.
        /// </summary>
        /// <param name="application">Контролируемое приложение Revit UI.</param>
        void InitializePluginsAndCreateUI(UIControlledApplication application);

        /// <summary>
        /// Выполняет процедуру завершения работы для всех загруженных плагинов.
        /// </summary>
        void ShutdownPlugins();

        #endregion
    }
}