using System.Reflection;

namespace MagicEntry.Core.Services
{
    /// <summary>
    /// Сервис для управления загрузкой и валидацией сборок в системе MagicEntry.
    /// Обеспечивает доступ к сборкам MagicEntry и Revit API.
    /// </summary>
    public interface IAssemblyService
    {
        #region Assembly Loading

        /// <summary>
        /// Загружает сборку MagicEntry по имени из папки Dependencies.
        /// </summary>
        /// <param name="assemblyName">Имя сборки без расширения .dll</param>
        /// <returns>Загруженная сборка или null, если загрузка не удалась</returns>
        Assembly LoadKRGPAssembly(string assemblyName);

        /// <summary>
        /// Загружает сборку Revit API по имени из папки установки Revit.
        /// </summary>
        /// <param name="assemblyName">Имя сборки без расширения .dll</param>
        /// <returns>Загруженная сборка или null, если загрузка не удалась</returns>
        Assembly LoadRevitApiAssembly(string assemblyName);

        #endregion

        #region Assembly Validation

        /// <summary>
        /// Проверяет наличие и корректность всех необходимых сборок.
        /// </summary>
        /// <returns>True, если все сборки доступны и корректны, иначе false</returns>
        bool ValidateAssemblies();

        /// <summary>
        /// Возвращает список доступных сборок MagicEntry в папке Dependencies.
        /// </summary>
        /// <returns>Массив имен доступных сборок</returns>
        string[] GetAvailableKRGPAssemblies();

        #endregion
    }
}