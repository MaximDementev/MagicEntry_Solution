namespace MagicEntry.Core.Services
{
    /// <summary>
    /// Сервис для управления путями к файлам и папкам в системе MagicEntry.
    /// Обеспечивает централизованное управление расположением пользовательских данных, сборок и ресурсов.
    /// </summary>
    public interface IPathService
    {
        #region User Data Paths

        /// <summary>
        /// Возвращает базовый путь к пользовательским данным MagicEntry.
        /// </summary>
        /// <returns>Путь к папке C:\Users\[CurrentUser]\AppData\Roaming\MagicEntry\UserData\</returns>
        string GetUserDataBasePath();

        /// <summary>
        /// Возвращает путь к папке пользовательских данных конкретного плагина.
        /// </summary>
        /// <param name="pluginName">Имя плагина</param>
        /// <returns>Путь к папке пользовательских данных плагина</returns>
        string GetPluginUserDataPath(string pluginName);

        /// <summary>
        /// Возвращает полный путь к файлу в папке пользовательских данных плагина.
        /// </summary>
        /// <param name="pluginName">Имя плагина</param>
        /// <param name="fileName">Имя файла</param>
        /// <returns>Полный путь к файлу</returns>
        string GetPluginUserDataFilePath(string pluginName, string fileName);

        #endregion

        #region Assembly Paths

        /// <summary>
        /// Возвращает путь к папке с дополнительными сборками MagicEntry.
        /// </summary>
        /// <returns>Путь к папке Dependencies</returns>
        string GetKRGPAssembliesPath();

        /// <summary>
        /// Возвращает путь к папке с API Revit.
        /// </summary>
        /// <returns>Путь к папке установки Revit</returns>
        string GetRevitApiPath();

        #endregion

        #region Directory Management

        /// <summary>
        /// Создает папку пользовательских данных для плагина, если она не существует.
        /// </summary>
        /// <param name="pluginName">Имя плагина</param>
        /// <returns>True, если папка создана или уже существует, иначе false</returns>
        bool EnsurePluginDirectoryExists(string pluginName);

        #endregion
    }
}