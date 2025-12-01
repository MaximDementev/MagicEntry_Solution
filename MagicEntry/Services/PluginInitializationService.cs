using MagicEntry.Core.Services;
using System;
using System.Collections.Concurrent;

namespace MagicEntry.Services
{
    /// <summary>
    /// Реализация сервиса инициализации плагинов для системы MagicEntry.
    /// Управляет состоянием плагинов и валидирует окружение.
    /// </summary>
    public class PluginInitializationService : IPluginInitializationService
    {
        #region Enums

        /// <summary>
        /// Возможные состояния плагина в системе.
        /// </summary>
        private enum PluginState
        {
            NotInitialized,
            Initializing,
            Ready,
            Failed
        }

        #endregion

        #region Fields

        private readonly ConcurrentDictionary<string, PluginState> _pluginStates;
        private readonly IPathService _pathService;
        private readonly IAssemblyService _assemblyService;

        #endregion

        #region Constructor

        /// <summary>
        /// Инициализирует новый экземпляр PluginInitializationService.
        /// </summary>
        /// <param name="pathService">Сервис управления путями</param>
        /// <param name="assemblyService">Сервис управления сборками</param>
        public PluginInitializationService(IPathService pathService, IAssemblyService assemblyService)
        {
            _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
            _assemblyService = assemblyService ?? throw new ArgumentNullException(nameof(assemblyService));
            _pluginStates = new ConcurrentDictionary<string, PluginState>();
        }

        #endregion

        #region IPluginInitializationService Implementation

        #region Plugin Initialization

        /// <summary>
        /// Инициализирует плагин и подготавливает его к работе.
        /// </summary>
        public bool InitializePlugin(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                return false;

            try
            {
                _pluginStates.AddOrUpdate(pluginName, PluginState.Initializing, (key, oldValue) => PluginState.Initializing);

                // Создаем папку пользовательских данных для плагина
                if (!_pathService.EnsurePluginDirectoryExists(pluginName))
                {
                    _pluginStates.TryUpdate(pluginName, PluginState.Failed, PluginState.Initializing);
                    return false;
                }

                // Проверяем готовность окружения
                if (!ValidateEnvironment())
                {
                    _pluginStates.TryUpdate(pluginName, PluginState.Failed, PluginState.Initializing);
                    return false;
                }

                _pluginStates.TryUpdate(pluginName, PluginState.Ready, PluginState.Initializing);
                return true;
            }
            catch
            {
                _pluginStates.TryUpdate(pluginName, PluginState.Failed, PluginState.Initializing);
                return false;
            }
        }

        /// <summary>
        /// Проверяет, готов ли плагин к работе.
        /// </summary>
        public bool IsPluginReady(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                return false;

            return _pluginStates.TryGetValue(pluginName, out var state) && state == PluginState.Ready;
        }

        /// <summary>
        /// Возвращает текущий статус плагина в виде строки.
        /// </summary>
        public string GetPluginStatus(string pluginName)
        {
            if (string.IsNullOrWhiteSpace(pluginName))
                return "Неизвестный плагин";

            if (!_pluginStates.TryGetValue(pluginName, out var state))
                return "Не инициализирован";

            switch (state)
            {
                case PluginState.NotInitialized:
                    return "Не инициализирован";
                case PluginState.Initializing:
                    return "Инициализируется";
                case PluginState.Ready:
                    return "Готов к работе";
                case PluginState.Failed:
                    return "Ошибка инициализации";
                default:
                    return "Неизвестное состояние";
            }
        }

        #endregion

        #region Environment Validation

        /// <summary>
        /// Проверяет готовность окр��жения для работы всех плагинов.
        /// </summary>
        public bool ValidateEnvironment()
        {
            try
            {
                // Проверяем доступность базовых сервисов
                if (_pathService == null || _assemblyService == null)
                    return false;

                // Проверяем доступность базовых путей
                var userDataPath = _pathService.GetUserDataBasePath();
                var assembliesPath = _pathService.GetKRGPAssembliesPath();
                var revitApiPath = _pathService.GetRevitApiPath();

                // Проверяем валидность сборок
                if (!_assemblyService.ValidateAssemblies())
                    return false;

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