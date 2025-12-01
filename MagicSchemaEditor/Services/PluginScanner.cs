using MagicEntry.Core.Models;
using MagicEntry.SchemaEditor.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MagicEntry.SchemaEditor.Services
{
    // Сканер для автоматического поиска и анализа плагинов в папках
    public class PluginScanner : IPluginScanner
    {
        #region IPluginScanner Implementation

        // Сканирует папку и возвращает список найденных потенциальных плагинов
        public List<PluginScanResult> ScanDirectory(string directoryPath)
        {
            var results = new List<PluginScanResult>();

            if (!Directory.Exists(directoryPath))
                return results;

            try
            {
                var dllFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

                foreach (var dllFile in dllFiles)
                {
                    try
                    {
                        var scanResult = AnalyzeAssembly(dllFile);
                        if (scanResult != null && scanResult.Commands.Any())
                        {
                            results.Add(scanResult);
                        }
                    }
                    catch
                    {
                        // Игнорируем сборки, которые не удается загрузить
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки доступа к папкам
            }

            return results;
        }

        // Анализирует сборку и извлекает команды, реализующие IExternalCommand
        public PluginScanResult AnalyzeAssembly(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                return null;

            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                var commands = new List<CommandInfo>();

                foreach (var type in assembly.GetTypes())
                {
                    if (IsExternalCommand(type))
                    {
                        var commandInfo = CreateCommandInfo(type, assemblyPath);
                        commands.Add(commandInfo);
                    }
                }

                if (commands.Any())
                {
                    return new PluginScanResult
                    {
                        AssemblyPath = assemblyPath,
                        AssemblyName = Path.GetFileNameWithoutExtension(assemblyPath),
                        Commands = commands
                    };
                }
            }
            catch
            {
                // Не удалось загрузить или проанализировать сборку
            }

            return null;
        }

        #endregion

        #region Private Methods

        // Проверяет, реализует ли тип интерфейс IExternalCommand
        private bool IsExternalCommand(Type type)
        {
            if (type.IsAbstract || type.IsInterface)
                return false;

            return type.GetInterfaces().Any(i => i.Name == "IExternalCommand");
        }

        // Создает информацию о команде на основе типа
        private CommandInfo CreateCommandInfo(Type type, string assemblyPath)
        {
            return new CommandInfo
            {
                ClassName = type.FullName,
                DisplayName = ExtractDisplayName(type),
                Description = ExtractDescription(type),
                RelativeAssemblyPath = MakeRelativePath(assemblyPath)
            };
        }

        // Извлекает отображаемое имя из атрибутов или имени класса
        private string ExtractDisplayName(Type type)
        {
            // Можно добавить логику извлечения из атрибутов
            var name = type.Name;
            if (name.EndsWith("Command"))
                name = name.Substring(0, name.Length - 7);

            return name;
        }

        // Извлекает описание из атрибутов
        private string ExtractDescription(Type type)
        {
            // Можно добавить логику извлечения из атрибутов
            return $"Команда {type.Name}";
        }

        // Создает относительный путь для сборки
        private string MakeRelativePath(string assemblyPath)
        {
            var fileName = Path.GetFileName(assemblyPath);
            var directory = Path.GetFileName(Path.GetDirectoryName(assemblyPath));
            return Path.Combine("MagicEntry.Plugins", directory, fileName);
        }

        #endregion
    }

    #region Supporting Classes

    // Результат сканирования сборки
    public class PluginScanResult
    {
        public string AssemblyPath { get; set; }
        public string AssemblyName { get; set; }
        public List<CommandInfo> Commands { get; set; } = new List<CommandInfo>();
    }

    // Информация о найденной команде
    public class CommandInfo
    {
        public string ClassName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string RelativeAssemblyPath { get; set; }
    }

    #endregion
}