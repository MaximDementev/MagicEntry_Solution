namespace MagicEntry.Core.Services
{
    /// <summary>
    /// Сервис для управления инициализацией и состоянием плагинов в системе MagicEntry.
    /// Отслеживает готовность плагинов и валидирует окружение.
    /// </summary>
    public interface IPluginInitializationService
    {
        #region Plugin Initialization

        /// <summary>
        /// Инициализирует плагин и подготавливает его к работе.
        /// </summary>
        /// <param name="pluginName">Имя плагина для инициализации</param>
        /// <returns>True, если инициализация прошла успешно, иначе false</returns>
        bool InitializePlugin(string pluginName);

        /// <summary>
        /// Проверяет, готов ли плагин к работе.
        /// </summary>
        /// <param name="pluginName">Имя плагина</param>
        /// <returns>True, если плагин готов к работе, иначе false</returns>
        bool IsPluginReady(string pluginName);

        /// <summary>
        /// Возвращает текущий статус плагина в виде строки.
        /// </summary>
        /// <param name="pluginName">Имя плагина</param>
        /// <returns>Строка с описанием статуса плагина</returns>
        string GetPluginStatus(string pluginName);

        #endregion

        #region Environment Validation

        /// <summary>
        /// Проверяет готовность окружения для работы всех плагинов.
        /// </summary>
        /// <returns>True, если окружение корректно настроено, иначе false</returns>
        bool ValidateEnvironment();

        #endregion
    }
}