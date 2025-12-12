using Autodesk.Revit.UI;
using MagicEntry.Core.Services;
using MagicEntry.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace MagicEntry
{
    /// <summary>
    /// Главный класс приложения, точка входа для системы плагинов MagicEntry в Revit.
    /// Отвечает за инициализацию менеджера плагинов, регистрацию сервисов и создание UI для них.
    /// </summary>
    public class MagicApplication : IExternalApplication
    {
        #region Fields

        private IPluginManager _pluginManager;
        private string _basePath; // Директория, где находится MagicEntry.dll и MagicEntry_Schema.xml


        #endregion

        #region Public Static Properties

        // Базовый путь к директории, где находится MagicEntry.dll и папка Config.
        public static string MagicEntryBasePath { get; private set; }

        #endregion

        #region IExternalApplication Implementation

        /// <summary>
        /// Вызывается при запуске Revit. Инициализирует систему плагинов и их UI.
        /// </summary>
        /// <param name="application">Контролируемое приложение Revit UI.</param>
        /// <returns>Результат операции.</returns>
        public Result OnStartup(UIControlledApplication application)
        {
            var sam = DebugUsersProvider.TryGetSamAccountFromAD();
            bool debugMode = DebugUsersProvider.ShouldEnableLogs(sam);

            MagicLogger.Init(sam, debugMode);


            BrokenPluginsRegistry.Load();

            using (StepTracker.Begin("OnStartup"))
            {
                try
                {
                    using (StepTracker.Begin("Определение путей"))
                    {
                        _basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        MagicEntryBasePath = _basePath;

                        MagicLogger.Write($"MagicEntryBasePath = {MagicEntryBasePath}");
                    }

                    using (StepTracker.Begin("InitializeServices"))
                        InitializeServices(application);

                    using (StepTracker.Begin("LoadPluginsAndCreateUI"))
                        LoadPluginsAndCreateUI(application);

                    MagicLogger.Write("Startup completed successfully");

                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    MagicLogger.WriteError("OnStartup", ex);

                    TaskDialog.Show(
                        "MagicEntry - Ошибка запуска",
                        "Произошла критическая ошибка при запуске MagicEntry.\n" +
                        "Лог автоматически записан. Обратитесь в BIM-поддержку."
                    );

                    return Result.Failed;
                }
            }
        }



        /// <summary>
        /// Вызывается при завершении работы Revit. Освобождает ресурсы плагинов.
        /// </summary>
        /// <param name="application">Контролируемое приложение Revit UI.</param>
        /// <returns>Результат операции.</returns>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _pluginManager?.ShutdownPlugins();
                ServiceProvider.ClearAllServices();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("MagicEntry Shutdown Error", $"Ошибка при завершении работы системы MagicEntry: {ex.Message}");
                return Result.Failed;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Инициализирует и регистрирует все сервисы системы MagicEntry.
        /// </summary>
        /// <param name="application">Контролируемое приложение Revit UI.</param>
        private void InitializeServices(UIControlledApplication application)
        {
            using (StepTracker.Begin("InitializeServices"))
            {
                try
                {
                    using (StepTracker.Begin("Определение версии Revit"))
                    {
                        var revitVersion = "2022";
                        MagicLogger.Write($"RevitVersion = {revitVersion}");
                    }

                    using (StepTracker.Begin("Создание и регистрация сервисов"))
                    {
                        var pathService = new PathService("2022");
                        ServiceProvider.RegisterService<IPathService>(pathService);
                        MagicLogger.Write("PathService создан и зарегистрирован");

                        var assemblyService = new AssemblyService(pathService);
                        ServiceProvider.RegisterService<IAssemblyService>(assemblyService);
                        MagicLogger.Write("AssemblyService создан и зарегистрирован");

                        var initializationService = new PluginInitializationService(pathService, assemblyService);
                        ServiceProvider.RegisterService<IPluginInitializationService>(initializationService);
                        MagicLogger.Write("PluginInitializationService создан и зарегистрирован");
                    }

                    using (StepTracker.Begin("Создание PluginManager"))
                    {
                        var configurationReader = new XmlConfigurationReader();
                        var pluginLoader = new ReflectionPluginLoader();
                        _pluginManager = new PluginManager(configurationReader, pluginLoader);

                        MagicLogger.Write("PluginManager успешно создан");
                    }

                    using (StepTracker.Begin("Валидация окружения"))
                    {
                        var initializationService = ServiceProvider.GetService<IPluginInitializationService>();

                        if (!initializationService.ValidateEnvironment())
                        {
                            MagicLogger.Write("ValidateEnvironment: окружение содержит ошибки");
                            TaskDialog.Show("MagicEntry Warning",
                                "Обнаружены проблемы с окружением. Некоторые функции могут работать некорректно.");
                        }
                        else
                        {
                            MagicLogger.Write("ValidateEnvironment: OK");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MagicLogger.WriteError("InitializeServices", ex);
                    throw;
                }
            }
        }


        /// <summary>
        /// Загружает плагины и создает для них элементы пользовательского интерфейса.
        /// </summary>
        /// <param name="application">Контролируемое приложение Revit UI.</param>
        private void LoadPluginsAndCreateUI(UIControlledApplication application)
        {
            using (StepTracker.Begin("LoadPluginsAndCreateUI"))
            {
                try
                {
                    using (StepTracker.Begin("Формирование пути к конфигурации"))
                    {
                        var configurationFilePath = Path.Combine(_basePath, "MagicEntry_Schema.xml");
                        MagicLogger.Write($"Config path = {configurationFilePath}");

                        using (StepTracker.Begin("LoadPlugins"))
                        {
                            _pluginManager.LoadPlugins(configurationFilePath, _basePath);
                            MagicLogger.Write("Плагины загружены");
                        }
                    }

                    using (StepTracker.Begin("InitializePluginsAndCreateUI"))
                    {
                        _pluginManager.InitializePluginsAndCreateUI(application);
                        MagicLogger.Write("UI создано успешно");
                    }
                }
                catch (Exception ex)
                {
                    MagicLogger.WriteError("LoadPluginsAndCreateUI", ex);
                    throw;
                }
            }
        }



        #endregion
    }
}