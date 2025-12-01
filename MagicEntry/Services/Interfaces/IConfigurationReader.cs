using MagicEntry.Core.Models;

namespace MagicEntry.Services
{
    /// <summary>
    /// Определяет контракт для сервиса, отвечающего за чтение конфигурации плагинов.
    /// </summary>
    public interface IConfigurationReader
    {
        #region Methods

        /// <summary>
        /// Считывает конфигурацию плагинов из указанного файла.
        /// </summary>
        /// <param name="configPath">Путь к файлу конфигурации.</param>
        /// <returns>Объект <see cref="PluginConfiguration"/>, содержащий информацию о плагинах.</returns>
        PluginConfiguration ReadConfiguration(string configPath);

        #endregion
    }
}