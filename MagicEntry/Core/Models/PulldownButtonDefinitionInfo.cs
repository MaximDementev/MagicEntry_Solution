using System.Xml.Serialization;
using System.ComponentModel;

namespace MagicEntry.Core.Models
{
    // Содержит информацию для определения PulldownButton (выпадающей кнопки-контейнера).
    public class PulldownButtonDefinitionInfo
    {
        #region Properties

        #region Основные параметры
        [Category("1. Основные параметры")]
        [DisplayName("ID (Имя)")]
        [Description("Уникальное внутреннее имя для PulldownButton. Используется для связи с плагинами. Пример: CommonUtilsGroup")]
        [XmlElement("Name")]
        public string Name { get; set; }

        [Category("1. Основные параметры")]
        [DisplayName("Активен")]
        [Description("Определяет, будет ли этот PulldownButton создан в интерфейсе Revit. Если false, кнопка и все плагины, предназначенные для нее, не будут отображены в этой группе.")]
        [XmlElement("Enabled")]
        public bool Enabled { get; set; } = true;
        #endregion

        #region Отображение в Revit UI
        [Category("2. Отображение в Revit UI")]
        [DisplayName("Отображаемое имя")]
        [Description("Текст, который будет виден пользователю на самой PulldownButton в ленте Revit.")]
        [XmlElement("DisplayName")]
        public string DisplayName { get; set; }

        [Category("2. Отображение в Revit UI")]
        [DisplayName("Вкладка ленты")]
        [Description("Имя вкладки в ленте Revit, на которой будет размещен PulldownButton. Должно совпадать с вкладкой плагинов этой группы.")]
        [XmlElement("RibbonTab")]
        public string RibbonTab { get; set; }

        [Category("2. Отображение в Revit UI")]
        [DisplayName("Панель ленты")]
        [Description("Имя панели на вкладке, где будет размещен PulldownButton. Должно совпадать с панелью плагинов этой группы.")]
        [XmlElement("RibbonPanel")]
        public string RibbonPanel { get; set; }

        [Category("2. Отображение в Revit UI")]
        [DisplayName("Описание (Tooltip)")]
        [Description("Подробное описание, отображаемое при наведении курсора на PulldownButton.")]
        [XmlElement("Description")]
        public string Description { get; set; }

        [Category("2. Отображение в Revit UI")]
        [DisplayName("Иконка (большая)")]
        [Description("Относительный путь к файлу большой иконки (32x32 px) от базовой директории MagicEntry. Пример: Icons\\Pulldowns\\Utils_32.png")]
        [XmlElement("LargeIcon")]
        public string LargeIcon { get; set; }

        [Category("2. Отображение в Revit UI")]
        [DisplayName("Иконка (маленькая)")]
        [Description("Относительный путь к файлу маленькой иконки (16x16 px) от базовой директории MagicEntry. Пример: Icons\\Pulldowns\\Utils_16.png")]
        [XmlElement("SmallIcon")]
        public string SmallIcon { get; set; }
        #endregion

        #endregion
    }
}