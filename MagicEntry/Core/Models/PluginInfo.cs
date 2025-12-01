using System.Collections.Generic;
using System.Xml.Serialization;
using System.ComponentModel; // Для атрибутов

namespace MagicEntry.Core.Models
{
    // Содержит информацию для одного плагина (который может быть PushButton или SplitButton).
    public class PluginInfo
    {
        #region Enums
        /// <summary>
        /// Определяет тип пользовательского интерфейса для кнопки плагина в Revit.
        /// </summary>
        public enum ButtonUIType
        {
            /// <summary>
            /// Обычная нажимаемая кнопка.
            /// </summary>
            [XmlEnum("PushButton")]
            PushButton,
            /// <summary>
            /// Кнопка с выпадающим списком дополнительных команд.
            /// </summary>
            [XmlEnum("SplitButton")]
            SplitButton
        }
        #endregion

        #region Properties

        #region Основные параметры
        [Category("1. Основные параметры")]
        [DisplayName("ID (Имя)")]
        [Description("Уникальное внутреннее имя плагина. Используется для идентификации. Пример: MyUniquePluginID")]
        [XmlElement("Name")]
        public string Name { get; set; }

        [Category("1. Основные параметры")]
        [DisplayName("Активен")]
        [Description("Определяет, будет ли плагин загружен и его UI создан. Если false, плагин игнорируется.")]
        [XmlElement("Enabled")]
        public bool Enabled { get; set; } = true;

        [Category("1. Основные параметры")]
        [DisplayName("Тип UI кнопки")]
        [Description("Определяет, будет ли это обычная кнопка (PushButton) или кнопка с выпадающим списком (SplitButton).")]
        [XmlElement("UIType")]
        public ButtonUIType UIType { get; set; } = ButtonUIType.PushButton;

        [Category("1. Основные параметры")]
        [DisplayName("Версия")]
        [Description("Версия плагина, например, 1.0.0. Отображается для информации.")]
        [XmlElement("Version")]
        public string Version { get; set; }
        #endregion

        #region Сборка и класс
        [Category("2. Сборка и класс")]
        [DisplayName("Путь к сборке")]
        [Description("Относительный путь к файлу .dll плагина от базовой директории MagicEntry. Пример: MagicEntry.Plugins\\MyPlugin\\MyPlugin.dll")]
        [XmlElement("AssemblyPath")]
        public string AssemblyPath { get; set; }

        [Category("2. Сборка и класс")]
        [DisplayName("Имя класса")]
        [Description("Полное имя класса (включая пространство имен), реализующего IExternalCommand. Пример: MyNamespace.MyPluginCommand")]
        [XmlElement("ClassName")]
        public string ClassName { get; set; }
        #endregion

        #region Отображение в Revit UI
        [Category("3. Отображение в Revit UI")]
        [DisplayName("Отображаемое имя")]
        [Description("Текст, который будет виден пользователю на кнопке в ленте Revit.")]
        [XmlElement("DisplayName")]
        public string DisplayName { get; set; }

        [Category("3. Отображение в Revit UI")]
        [DisplayName("Вкладка ленты")]
        [Description("Имя вкладки в ленте Revit, на которой будет размещен плагин. Пример: Моя Вкладка")]
        [XmlElement("RibbonTab")]
        public string RibbonTab { get; set; }

        [Category("3. Отображение в Revit UI")]
        [DisplayName("Панель ленты")]
        [Description("Имя панели на вкладке, где будет размещен плагин. Пример: Инструменты")]
        [XmlElement("RibbonPanel")]
        public string RibbonPanel { get; set; }

        [Category("3. Отображение в Revit UI")]
        [DisplayName("Описание (Tooltip)")]
        [Description("Подробное описание функциональности плагина, отображаемое при наведении курсора на кнопку.")]
        [XmlElement("Description")]
        public string Description { get; set; }

        [Category("3. Отображение в Revit UI")]
        [DisplayName("Иконка (большая)")]
        [Description("Относительный путь к файлу большой иконки (32x32 px) от папки плагина. Пример: Icons\\MyPlugin_32.png")]
        [XmlElement("LargeIcon")]
        public string LargeIcon { get; set; }

        [Category("3. Отображение в Revit UI")]
        [DisplayName("Иконка (маленькая)")]
        [Description("Относительный путь к файлу маленькой иконки (16x16 px) от папки плагина. Пример: Icons\\MyPlugin_16.png. Используется для PulldownButton.")]
        [XmlElement("SmallIcon")]
        public string SmallIcon { get; set; }
        #endregion

        #region Поведение при запуске
        [Category("4. Поведение при запуске")]
        [DisplayName("Загружать при старте")]
        [Description("Определяет, должен ли плагин загружаться и его UI создаваться при старте Revit. Работает только если 'Активен' = true.")]
        [XmlElement("LoadOnStartup")]
        public bool LoadOnStartup { get; set; } = true;
        #endregion

        #region Группировка
        [Category("5. Группировка")]
        [DisplayName("Имя группы PulldownButton")]
        [Description("Имя существующего PulldownButton (из секции PulldownButtonDefinitions), в который будет добавлен этот плагин. Оставьте пустым, если плагин должен быть добавлен напрямую на панель.")]
        [XmlElement("PulldownGroupName")]
        public string PulldownGroupName { get; set; }
        #endregion

        #region Подкоманды (для SplitButton)
        [Category("6. Подкоманды (для SplitButton)")]
        [DisplayName("Список подкоманд")]
        [Description("Список команд, которые будут доступны в выпадающем меню, если 'Тип UI кнопки' = SplitButton.")]
        [XmlArray("SubCommands")]
        [XmlArrayItem("Command")]
        public List<SubCommandInfo> SubCommands { get; set; } = new List<SubCommandInfo>();
        #endregion
        #endregion
    }
}