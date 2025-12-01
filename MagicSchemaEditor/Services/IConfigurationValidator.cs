using MagicEntry.Core.Models;
using System.Collections.Generic;

namespace MagicEntry.SchemaEditor.Services
{
    // Интерфейс для валидации конфигурации плагинов
    public interface IConfigurationValidator
    {
        #region Methods

        // Валидирует всю конфигурацию и возвращает список ошибок
        List<ValidationError> ValidateConfiguration(PluginConfiguration configuration, string basePath);

        // Валидирует отдельный плагин
        List<ValidationError> ValidatePlugin(PluginInfo plugin, string basePath);

        // Валидирует определение PulldownButton
        List<ValidationError> ValidatePulldownDefinition(PulldownButtonDefinitionInfo pulldown, string basePath);

        #endregion
    }
}