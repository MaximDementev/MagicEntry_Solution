using MagicEntry.Core.Interfaces;
using MagicEntry.Core.Models;
using System;
using System.IO;
using System.Reflection;

namespace MagicEntry.Services
{
    /// <summary>
    /// Реализация <see cref="IPluginLoader"/>, использующая рефлексию для загрузки сборок плагинов.
    /// Загружает только те плагины, которые реализуют интерфейс IPlugin.
    /// </summary>
    public class ReflectionPluginLoader : IPluginLoader
    {
        #region IPluginLoader Implementation

        /// <summary>
        /// Загружает экземпляр плагина (если он реализует IPlugin) из сборки.
        /// </summary>
        /// <param name="pluginInfo">Конфигурационная информация о плагине.</param>
        /// <param name="basePath">Базовый путь, относительно которого указан <see cref="PluginInfo.AssemblyPath"/>.</param>
        /// <returns>Экземпляр <see cref="IPlugin"/>, если загрузка успешна, плагин включен и реализует IPlugin, иначе null.</returns>
        public IPlugin LoadPlugin(PluginInfo pluginInfo, string basePath)
        {
            if (pluginInfo == null) throw new ArgumentNullException(nameof(pluginInfo));
            if (!pluginInfo.Enabled) return null;

            var assemblyPath = Path.Combine(basePath, pluginInfo.AssemblyPath);
            if (!File.Exists(assemblyPath))
            {
                // Вместо выбрасывания исключения, можно вернуть null и залогировать ошибку выше
                return null;
            }

            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                // Ищем тип, указанный в ClassName. Это должен быть класс, реализующий IExternalCommand.
                // Он также МОЖЕТ реализовывать IPlugin, но это не обязательно.
                var commandType = assembly.GetType(pluginInfo.ClassName, throwOnError: false);

                if (commandType == null)
                {
                    // TaskDialog.Show("Plugin Load Error", $"Класс команды {pluginInfo.ClassName} не найден в сборке {assemblyPath}");
                    return null;
                }

                // Проверяем, реализует ли этот тип IPlugin. Если да, создаем экземпляр.
                if (typeof(IPlugin).IsAssignableFrom(commandType))
                {
                    var pluginInstance = (IPlugin)Activator.CreateInstance(commandType);
                    pluginInstance.Info = pluginInfo; // Это важно, чтобы IPlugin.Info было установлено
                    pluginInstance.IsEnabled = pluginInfo.Enabled;
                    return pluginInstance;
                }

                return null;
            }
            catch (Exception)
            {
                return null; // Ошибка при загрузке или создании экземпляра
            }
        }


        #endregion
    }
}