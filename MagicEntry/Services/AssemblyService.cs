using MagicEntry.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MagicEntry.Services
{
    /// <summary>
    /// Реализация сервиса управления сборками для системы MagicEntry.
    /// Обеспечивает загрузку и валидацию сборок MagicEntry и Revit API.
    /// </summary>
    public class AssemblyService : IAssemblyService
    {
        #region Fields

        private readonly IPathService _pathService;

        #endregion

        #region Constructor

        /// <summary>
        /// Инициализирует новый экземпляр AssemblyService.
        /// </summary>
        /// <param name="pathService">Сервис управления путями</param>
        public AssemblyService(IPathService pathService)
        {
            _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        }

        #endregion

        #region IAssemblyService Implementation

        #region Assembly Loading

        /// <summary>
        /// Загружает сборку MagicEntry по имени из папки Dependencies.
        /// </summary>
        public Assembly LoadKRGPAssembly(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return null;

            try
            {
                var assembliesPath = _pathService.GetKRGPAssembliesPath();
                var assemblyFileName = assemblyName.EndsWith(".dll") ? assemblyName : $"{assemblyName}.dll";
                var assemblyPath = Path.Combine(assembliesPath, assemblyFileName);

                if (!File.Exists(assemblyPath))
                    return null;

                return Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Загружает сборку Revit API по имени из папки установки Revit.
        /// </summary>
        public Assembly LoadRevitApiAssembly(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return null;

            try
            {
                var revitApiPath = _pathService.GetRevitApiPath();
                var assemblyFileName = assemblyName.EndsWith(".dll") ? assemblyName : $"{assemblyName}.dll";
                var assemblyPath = Path.Combine(revitApiPath, assemblyFileName);

                if (!File.Exists(assemblyPath))
                    return null;

                return Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Assembly Validation

        /// <summary>
        /// Проверяет наличие и корректность всех необходимых сборок.
        /// </summary>
        public bool ValidateAssemblies()
        {
            try
            {
                // Проверяем доступность папки с сборками MagicEntry
                var assembliesPath = _pathService.GetKRGPAssembliesPath();
                if (!Directory.Exists(assembliesPath))
                    return false;

                // Проверяем доступность папки Revit API
                var revitApiPath = _pathService.GetRevitApiPath();
                if (!Directory.Exists(revitApiPath))
                    return false;

                // Проверяем наличие основных сборок Revit API
                var requiredRevitAssemblies = new[] { "RevitAPI.dll", "RevitAPIUI.dll" };
                foreach (var assemblyName in requiredRevitAssemblies)
                {
                    var assemblyPath = Path.Combine(revitApiPath, assemblyName);
                    if (!File.Exists(assemblyPath))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Возвращает список доступных сборок MagicEntry в папке Dependencies.
        /// </summary>
        public string[] GetAvailableKRGPAssemblies()
        {
            try
            {
                var assembliesPath = _pathService.GetKRGPAssembliesPath();
                if (!Directory.Exists(assembliesPath))
                    return new string[0];

                return Directory.GetFiles(assembliesPath, "*.dll")
                    .Select(Path.GetFileNameWithoutExtension)
                    .ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        #endregion

        #endregion
    }
}