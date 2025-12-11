using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Windows.Forms;
using System.Windows.Interop;

namespace MagicEntry.SchemaEditor
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SchemaEditorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string revitVersionYear = "2022"; // Получаем версию Revit, например "2022"
            string schemaFileName = "MagicEntry_Schema.xml";
            string determinedSchemaFilePath = "";

            // 1. Пытаемся найти файл через MagicEntryApplication.MagicEntryBasePath (если он установлен)
            //    Это путь к директории, где лежит MagicEntry.dll
            if (!string.IsNullOrEmpty(MagicApplication.MagicEntryBasePath))
            {
                determinedSchemaFilePath = Path.Combine(MagicApplication.MagicEntryBasePath, schemaFileName);
            }

            // 2. Если файл не найден через MagicEntryBasePath или MagicEntryBasePath не установлен,
            //    пытаемся найти по стандартному пути в ProgramData, используя текущую версию Revit.
            if (string.IsNullOrEmpty(determinedSchemaFilePath) || !File.Exists(determinedSchemaFilePath))
            {
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                determinedSchemaFilePath = Path.Combine(programDataPath, "Autodesk", "Revit", "Addins", revitVersionYear, "MagicEntry", schemaFileName);
            }

            string finalSchemaFilePath = determinedSchemaFilePath; // Это путь, который мы пытаемся использовать по умолчанию

            try
            {
                // Если зажата клавиша Shift, принудительно показываем диалог выбора файла
                if (System.Windows.Forms.Control.ModifierKeys == Keys.Shift)
                {
                    using (var openFileDialog = new OpenFileDialog())
                    {
                        string initialDir = Path.GetDirectoryName(finalSchemaFilePath);
                        if (string.IsNullOrEmpty(initialDir) || !Directory.Exists(initialDir))
                        {
                            // Безопасный fallback, если путь по умолчанию некорректен
                            initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        }
                        openFileDialog.InitialDirectory = initialDir;
                        openFileDialog.FileName = Path.GetFileName(finalSchemaFilePath);
                        openFileDialog.Filter = "MagicEntry Schema Files (*.xml)|*.xml|All files (*.*)|*.*";
                        openFileDialog.Title = "Выберите файл схемы MagicEntry (режим Shift)";
                        openFileDialog.CheckFileExists = false; // Позволяем выбрать/ввести несуществующий файл (для создания)

                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            finalSchemaFilePath = openFileDialog.FileName; // Пользователь выбрал новый путь
                        }
                        else
                        {
                            TaskDialog.Show("Редактор Схемы", "Выбор файла отменен (в режиме Shift). Команда не будет выполнена.");
                            return Result.Cancelled; // Если Shift нажат, пользователь должен выбрать файл или отменить команду
                        }
                    }
                }

                // Если файл по итоговому пути (finalSchemaFilePath) не существует
                if (!File.Exists(finalSchemaFilePath))
                {
                    var dialogResult = MessageBox.Show($"Файл схемы '{finalSchemaFilePath}' не найден.\nСоздать новый пустой файл схемы по этому пути?",
                                                       "Файл не найден", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.Yes)
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(finalSchemaFilePath)); // Создаем директорию, если ее нет
                            File.WriteAllText(finalSchemaFilePath,
                                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<MagicEntryConfiguration>\n  <PulldownButtonDefinitions />\n  <Plugins />\n</MagicEntryConfiguration>");
                        }
                        catch (Exception exCreate)
                        {
                            TaskDialog.Show("Ошибка Редактора Схемы", $"Не удалось создать файл схемы: {exCreate.Message}");
                            return Result.Failed;
                        }
                    }
                    else
                    {
                        TaskDialog.Show("Редактор Схемы", "Файл схемы не найден и не был создан. Редактор не будет запущен.");
                        return Result.Failed;
                    }
                }

                // На этом этапе finalSchemaFilePath должен указывать на существующий (возможно, только что созданный) файл
                using (SchemaEditorForm editorForm = new SchemaEditorForm(finalSchemaFilePath))
                {
                    var helper = new WindowInteropHelper(editorForm);
                    helper.Owner = commandData.Application.MainWindowHandle;
                    editorForm.ShowDialog();
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка при запуске редактора схемы: {ex.ToString()}";
                TaskDialog.Show("Критическая Ошибка Редактора", message);
                return Result.Failed;
            }
        }
    }
}
