using MagicEntry.Core.Models; // Для PluginInfo

namespace MagicEntry.Core.Interfaces
{
    /// <summary>
    /// Определяет базовый контракт для плагинов в системе MagicEntry.
    /// Плагины могут реализовывать этот интерфейс для предоставления метаданных
    /// и выполнения специфической логики инициализации/завершения, не связанной с UI.
    /// Основная логика команды Revit должна быть в классе, реализующем IExternalCommand.
    /// </summary>
    public interface IPlugin
    {
        #region Properties

        /// <summary>
        /// Получает или задает конфигурационную информацию о плагине.
        /// Заполняется системой при загрузке плагина.
        /// </summary>
        PluginInfo Info { get; set; }

        /// <summary>
        /// Получает или задает значение, указывающее, активен ли плагин.
        /// </summary>
        bool IsEnabled { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Выполняет инициализацию плагина, если это необходимо для его внутренней логики.
        /// Этот метод не должен создавать элементы UI.
        /// </summary>
        /// <returns>True, если инициализация прошла успешно, иначе false.</returns>
        bool Initialize();

        /// <summary>
        /// Освобождает ресурсы, используемые плагином.
        /// </summary>
        void Shutdown();

        #endregion
    }
}