using MagicEntry.Core.Interfaces;
using MagicEntry.Core.Models;
using System.Collections.Generic;

namespace MagicEntry.Services
{
    /// <summary>
    /// Определяет контракт для сервиса, отвечающего за загрузку экземпляров плагинов.
    /// </summary>
    public interface IPluginLoader
    {
        #region Methods

        /// <summary>
        /// Загружает один плагин на основе его конфигурационной информации.
        /// </summary>
        /// <param name="pluginInfo">Информация о плагине.</param>
        /// <param name="basePath">Базовый путь для разрешения относительных путей к сборкам.</param>
        /// <returns>Экземпляр <see cref="IPlugin"/> или null, если плагин отключен.</returns>
        IPlugin LoadPlugin(PluginInfo pluginInfo, string basePath);

        #endregion
    }
}