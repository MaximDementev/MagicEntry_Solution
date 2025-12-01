using MagicEntry.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MagicEntry.SchemaEditor.Services
{
    // Валидатор конфигурации плагинов с проверкой путей, классов и зависимостей
    public class ConfigurationValidator : IConfigurationValidator
    {
        #region IConfigurationValidator Implementation

        // Валидирует всю конфигурацию и возвращает все найденные ошибки
        public List<ValidationError> ValidateConfiguration(PluginConfiguration configuration, string basePath)
        {
            var errors = new List<ValidationError>();

            if (configuration == null)
            {
                errors.Add(new ValidationError("Configuration", "Конфигурация не может быть null", ValidationSeverity.Error));
                return errors;
            }

            // Валидация PulldownButton определений
            foreach (var pulldown in configuration.PulldownButtonDefinitions ?? new List<PulldownButtonDefinitionInfo>())
            {
                errors.AddRange(ValidatePulldownDefinition(pulldown, basePath));
            }

            // Валидация плагинов
            foreach (var plugin in configuration.Plugins ?? new List<PluginInfo>())
            {
                errors.AddRange(ValidatePlugin(plugin, basePath));
            }

            // Проверка уникальности имен
            errors.AddRange(ValidateUniqueNames(configuration));

            return errors;
        }

        // Валидирует отдельный плагин
        public List<ValidationError> ValidatePlugin(PluginInfo plugin, string basePath)
        {
            var errors = new List<ValidationError>();

            if (plugin == null)
                return errors;

            // Проверка обязательных полей
            if (string.IsNullOrWhiteSpace(plugin.Name))
                errors.Add(new ValidationError($"Plugin.{plugin.Name}", "Имя плагина не может быть пустым", ValidationSeverity.Error));

            if (string.IsNullOrWhiteSpace(plugin.DisplayName))
                errors.Add(new ValidationError($"Plugin.{plugin.Name}", "Отображаемое имя не может быть пустым", ValidationSeverity.Warning));

            // Проверка пути к сборке
            if (!string.IsNullOrWhiteSpace(plugin.AssemblyPath))
            {
                var fullPath = Path.Combine(basePath, plugin.AssemblyPath);
                if (!File.Exists(fullPath))
                {
                    errors.Add(new ValidationError($"Plugin.{plugin.Name}", $"Сборка не найдена: {plugin.AssemblyPath}", ValidationSeverity.Error));
                }
                else
                {
                    // Проверка класса команды
                    errors.AddRange(ValidateCommandClass(plugin, fullPath));
                }
            }

            // Проверка иконок
            errors.AddRange(ValidateIcons(plugin, basePath));

            // Проверка подкоманд для SplitButton
            if (plugin.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                errors.AddRange(ValidateSubCommands(plugin, basePath));
            }

            return errors;
        }

        // Валидирует определение PulldownButton
        public List<ValidationError> ValidatePulldownDefinition(PulldownButtonDefinitionInfo pulldown, string basePath)
        {
            var errors = new List<ValidationError>();

            if (pulldown == null)
                return errors;

            if (string.IsNullOrWhiteSpace(pulldown.Name))
                errors.Add(new ValidationError($"Pulldown.{pulldown.Name}", "Имя PulldownButton не может быть пустым", ValidationSeverity.Error));

            if (string.IsNullOrWhiteSpace(pulldown.DisplayName))
                errors.Add(new ValidationError($"Pulldown.{pulldown.Name}", "Отображаемое имя не может быть пустым", ValidationSeverity.Warning));

            // Проверка иконок
            errors.AddRange(ValidatePulldownIcons(pulldown, basePath));

            return errors;
        }

        #endregion

        #region Private Validation Methods

        // Проверяет уникальность имен в конфигурации
        private List<ValidationError> ValidateUniqueNames(PluginConfiguration configuration)
        {
            var errors = new List<ValidationError>();

            // Проверка уникальности имен плагинов
            var pluginNames = configuration.Plugins?.Where(p => !string.IsNullOrWhiteSpace(p.Name)).Select(p => p.Name).ToList() ?? new List<string>();
            var duplicatePlugins = pluginNames.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key);

            foreach (var duplicate in duplicatePlugins)
            {
                errors.Add(new ValidationError("Configuration", $"Дублирующееся имя плагина: {duplicate}", ValidationSeverity.Error));
            }

            // Проверка уникальности имен PulldownButton
            var pulldownNames = configuration.PulldownButtonDefinitions?.Where(p => !string.IsNullOrWhiteSpace(p.Name)).Select(p => p.Name).ToList() ?? new List<string>();
            var duplicatePulldowns = pulldownNames.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key);

            foreach (var duplicate in duplicatePulldowns)
            {
                errors.Add(new ValidationError("Configuration", $"Дублирующееся имя PulldownButton: {duplicate}", ValidationSeverity.Error));
            }

            return errors;
        }

        // Валидирует класс команды в сборке
        private List<ValidationError> ValidateCommandClass(PluginInfo plugin, string assemblyPath)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(plugin.ClassName))
            {
                errors.Add(new ValidationError($"Plugin.{plugin.Name}", "Имя класса команды не может быть пустым", ValidationSeverity.Error));
                return errors;
            }

            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                var commandType = assembly.GetType(plugin.ClassName);

                if (commandType == null)
                {
                    errors.Add(new ValidationError($"Plugin.{plugin.Name}", $"Класс {plugin.ClassName} не найден в сборке", ValidationSeverity.Error));
                }
                else
                {
                    var hasIExternalCommand = commandType.GetInterfaces().Any(i => i.Name == "IExternalCommand");
                    if (!hasIExternalCommand)
                    {
                        errors.Add(new ValidationError($"Plugin.{plugin.Name}", $"Класс {plugin.ClassName} не реализует IExternalCommand", ValidationSeverity.Error));
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError($"Plugin.{plugin.Name}", $"Ошибка загрузки сборки: {ex.Message}", ValidationSeverity.Error));
            }

            return errors;
        }

        // Валидирует иконки плагина
        private List<ValidationError> ValidateIcons(PluginInfo plugin, string basePath)
        {
            var errors = new List<ValidationError>();

            if (!string.IsNullOrWhiteSpace(plugin.LargeIcon))
            {
                var iconPath = Path.Combine(basePath, Path.GetDirectoryName(plugin.AssemblyPath) ?? "", plugin.LargeIcon);
                if (!File.Exists(iconPath))
                {
                    errors.Add(new ValidationError($"Plugin.{plugin.Name}", $"Большая иконка не найдена: {plugin.LargeIcon}", ValidationSeverity.Warning));
                }
            }

            if (!string.IsNullOrWhiteSpace(plugin.SmallIcon))
            {
                var iconPath = Path.Combine(basePath, Path.GetDirectoryName(plugin.AssemblyPath) ?? "", plugin.SmallIcon);
                if (!File.Exists(iconPath))
                {
                    errors.Add(new ValidationError($"Plugin.{plugin.Name}", $"Маленькая иконка не найдена: {plugin.SmallIcon}", ValidationSeverity.Warning));
                }
            }

            return errors;
        }

        // Валидирует иконки PulldownButton
        private List<ValidationError> ValidatePulldownIcons(PulldownButtonDefinitionInfo pulldown, string basePath)
        {
            var errors = new List<ValidationError>();

            if (!string.IsNullOrWhiteSpace(pulldown.LargeIcon))
            {
                var iconPath = Path.Combine(basePath, pulldown.LargeIcon);
                if (!File.Exists(iconPath))
                {
                    errors.Add(new ValidationError($"Pulldown.{pulldown.Name}", $"Большая иконка не найдена: {pulldown.LargeIcon}", ValidationSeverity.Warning));
                }
            }

            if (!string.IsNullOrWhiteSpace(pulldown.SmallIcon))
            {
                var iconPath = Path.Combine(basePath, pulldown.SmallIcon);
                if (!File.Exists(iconPath))
                {
                    errors.Add(new ValidationError($"Pulldown.{pulldown.Name}", $"Маленькая иконка не найдена: {pulldown.SmallIcon}", ValidationSeverity.Warning));
                }
            }

            return errors;
        }

        // Валидирует подкоманды SplitButton
        private List<ValidationError> ValidateSubCommands(PluginInfo plugin, string basePath)
        {
            var errors = new List<ValidationError>();

            if (plugin.SubCommands == null || !plugin.SubCommands.Any())
            {
                errors.Add(new ValidationError($"Plugin.{plugin.Name}", "SplitButton должен содержать хотя бы одну подкоманду", ValidationSeverity.Warning));
                return errors;
            }

            foreach (var subCommand in plugin.SubCommands)
            {
                if (string.IsNullOrWhiteSpace(subCommand.ClassName))
                {
                    errors.Add(new ValidationError($"Plugin.{plugin.Name}.SubCommand.{subCommand.Name}", "Имя класса подкоманды не может быть пустым", ValidationSeverity.Error));
                }
            }

            return errors;
        }

        #endregion
    }

    #region Supporting Classes

    // Ошибка валидации
    public class ValidationError
    {
        public string Context { get; set; }
        public string Message { get; set; }
        public ValidationSeverity Severity { get; set; }

        public ValidationError(string context, string message, ValidationSeverity severity)
        {
            Context = context;
            Message = message;
            Severity = severity;
        }

        public override string ToString()
        {
            var severityText = Severity == ValidationSeverity.Error ? "ОШИБКА" : "ПРЕДУПРЕЖДЕНИЕ";
            return $"[{severityText}] {Context}: {Message}";
        }
    }

    // Уровень серьезности ошибки валидации
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    #endregion
}