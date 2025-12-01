using MagicEntry.Core.Models;
using System.Collections.Generic;

namespace MagicEntry.SchemaEditor.Services
{
    // Интерфейс для сканирования папок и поиска потенциальных плагинов
    public interface IPluginScanner
    {
        #region Methods

        // Сканирует указанную папку и возвращает найденные потенциальные плагины
        List<PluginScanResult> ScanDirectory(string directoryPath);

        // Анализирует сборку и извлекает информацию о командах Revit
        PluginScanResult AnalyzeAssembly(string assemblyPath);

        #endregion
    }
}