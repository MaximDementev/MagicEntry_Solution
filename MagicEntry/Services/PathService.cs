using MagicEntry.Core.Services;
using System;
using System.IO;

namespace MagicEntry.Services
{
    /// <summary>
    /// Реализация сервиса управления путями для системы MagicEntry.
    /// Предоставляет централизованное управление расположением файлов и папок.
    /// </summary>
    public class PathService : IPathService
    {
        #region Fields

        private readonly string _revitVersion;
        private readonly string _userDataBasePath;
        private readonly string _krgpAssembliesPath;
        private readonly string _revitApiPath;

        #endregion

        #region Constructor

        /// <summary>
        /// Инициализирует новый экземпляр PathService.
        /// </summary>
        /// <param name="revitVersion">Версия Revit (например, "2022")</param>
        public PathService(string revitVersion)
        {
            _revitVersion = revitVersion ?? throw new ArgumentNullException(nameof(revitVersion));

            _userDataBasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MagicEntry", "UserData");

            _krgpAssembliesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Autodesk", "Revit", "Addins", _revitVersion, "MagicEntry", "Dependencies");

            _revitApiPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Autodesk", $"Revit {_revitVersion}");
        }

        #endregion

        #region IPathService Implementation

        #region User Data Paths

        /// <summary>
        /// Возвращает базовый путь к пользовательским данным MagicEntry.
        /// </summary>
        public string GetUserDataBasePath()
        {
            return _userDataBasePath;
        }

        /// <summary>
        /// Возвращает путь к папке пользовательских данных конкретного плагина.
        /// </summary>
        public string GetPluginUserDataPath(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Имя плагина не может быть пустым", nameof(pluginName));

            return Path.Combine(_userDataBasePath, pluginName);
        }

        /// <summary>
        /// Возвращает полный путь к файлу в папке пользовательских данных плагина.
        /// </summary>
        public string GetPluginUserDataFilePath(string pluginName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                throw new ArgumentException("Имя плагина не может быть пустым", nameof(pluginName));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Имя файла не может быть пустым", nameof(fileName));

            return Path.Combine(GetPluginUserDataPath(pluginName), fileName);
        }

        #endregion

        #region Assembly Paths

        /// <summary>
        /// Возвращает путь к папке с дополнительными сборками MagicEntry.
        /// </summary>
        public string GetKRGPAssembliesPath()
        {
            return _krgpAssembliesPath;
        }

        /// <summary>
        /// Возвращает путь к папке с API Revit.
        /// </summary>
        public string GetRevitApiPath()
        {
            return _revitApiPath;
        }

        #endregion

        #region Directory Management

        /// <summary>
        /// Создает папку пользовательских данных для плагина, если она не существует.
        /// </summary>
        public bool EnsurePluginDirectoryExists(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                return false;

            try
            {
                var pluginPath = GetPluginUserDataPath(pluginName);
                if (!Directory.Exists(pluginPath))
                {
                    Directory.CreateDirectory(pluginPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #endregion
    }
}