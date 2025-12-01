using MagicEntry.Core.Models;
using System;
using System.IO;
using System.Xml.Serialization;

namespace MagicEntry.Services
{
    /// <summary>
    /// Реализация <see cref="IConfigurationReader"/> для чтения конфигурации из XML-файла.
    /// </summary>
    public class XmlConfigurationReader : IConfigurationReader
    {
        #region IConfigurationReader Implementation

        /// <summary>
        /// Считывает конфигурацию плагинов из XML-файла по указанному пути.
        /// </summary>
        /// <param name="configPath">Путь к XML-файлу конфигурации.</param>
        /// <returns>Объект <see cref="PluginConfiguration"/>.</returns>
        /// <exception cref="FileNotFoundException">Если файл конфигурации не найден.</exception>
        /// <exception cref="InvalidOperationException">Если произошла ошибка при десериализации XML.</exception>
        public PluginConfiguration ReadConfiguration(string configPath)
        {
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Файл конфигурации не найден: {configPath}");
            }

            try
            {
                var serializer = new XmlSerializer(typeof(PluginConfiguration));
                using (var reader = new FileStream(configPath, FileMode.Open, FileAccess.Read))
                {
                    return (PluginConfiguration)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Ошибка при чтении XML-конфигурации: {ex.Message}", ex);
            }
        }

        #endregion
    }
}