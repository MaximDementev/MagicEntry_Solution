using MagicEntry.Core.Models;
using MagicEntry.SchemaEditor.Dialogs;
using MagicEntry.SchemaEditor.Services;
using MagicEntry.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MagicEntry.SchemaEditor
{
    // Форма для визуального редактирования файла конфигурации MagicEntry_Schema.xml
    public partial class SchemaEditorForm : Form
    {
        #region Поля

        // Базовые поля
        private string _schemaFilePath;
        private PluginConfiguration _currentConfiguration;
        private BindingList<PulldownButtonDefinitionInfo> _pulldownDefinitionsBindingList;
        private BindingList<PluginInfo> _pluginsBindingList;

        private readonly Color _enabledColor = Color.LightGreen;
        private readonly Color _disabledColor = Color.LightCoral;
        private readonly Color _warningColor = Color.LightYellow;
        private readonly Color _errorColor = Color.LightCoral;
        private readonly Color _defaultRowColor = SystemColors.Window;

        private bool _isDirty = false;

        // Поля для улучшенных функций
        private readonly IConfigurationValidator _validator;
        private readonly IPluginScanner _scanner;
        private Timer _validationTimer;
        private List<ValidationError> _currentErrors;
        private string _basePath;
        private Splitter splitter1;
        private SplitContainer splitContainer1;
        private Point? _dragStartPosition;

        // Поля для управления панелью валидации
        private bool _isValidationPanelVisible = false;
        private const int VALIDATION_PANEL_WIDTH = 600;
        private const int VALIDATION_PANEL_MIN_WIDTH = 10;

        #endregion

        #region Конструктор

        // Инициализирует новый экземпляр формы SchemaEditorForm с расширенными возможностями
        public SchemaEditorForm(string schemaFilePath)
        {
            _schemaFilePath = schemaFilePath ?? throw new ArgumentNullException(nameof(schemaFilePath));
            _validator = new ConfigurationValidator();
            _scanner = new PluginScanner();
            _currentErrors = new List<ValidationError>();
            _basePath = Path.GetDirectoryName(schemaFilePath);

            InitializeComponent();

            // Настройка валидации
            _validationTimer = new Timer();
            _validationTimer.Interval = 1000; // 1 секунда задержки
            _validationTimer.Tick += ValidationTimer_Tick;

            // Настройка Drag & Drop
            SetupDragAndDrop();

            // Загрузка конфигурации
            LoadConfigurationFromFile(_schemaFilePath);
            UpdateFormTitle();

            // Подписка на события
            this.FormClosing += SchemaEditorForm_FormClosing;
            propertyGridMain.PropertyValueChanged += propertyGrid_PropertyValueChanged;
            propertyGridSubCommands.PropertyValueChanged += propertyGrid_PropertyValueChanged;

            // Скрытие элементов подкоманд
            propertyGridSubCommands.Visible = false;

            // Настройка начального состояния панели валидации
            InitializeValidationPanelState();
        }

        #endregion

        #region Методы инициализации

        // Настраивает начальное состояние панели валидации
        private void InitializeValidationPanelState()
        {
            panelValidation.Visible = false;
            splitContainer1.Panel2MinSize = VALIDATION_PANEL_MIN_WIDTH;
            splitContainer1.SplitterDistance = splitContainer1.Width - VALIDATION_PANEL_MIN_WIDTH;
            btnToggleValidation.BackColor = SystemColors.Control;
            btnToggleValidation.Text = "Показать ошибки";
        }

        #endregion

        #region Методы загрузки и сохранения конфигурации

        // Обновляет заголовок формы
        private void UpdateFormTitle()
        {
            this.Text = $"Редактор схемы MagicEntry - [{Path.GetFileName(_schemaFilePath)}]";
        }

        // Загружает конфигурацию из XML-файла
        private void LoadConfigurationFromFile(string filePath)
        {
            try
            {
                var reader = new XmlConfigurationReader();
                _currentConfiguration = reader.ReadConfiguration(filePath);
                _schemaFilePath = filePath;
                UpdateFormTitle();
                _isDirty = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла схемы '{filePath}': {ex.Message}", "Ошибка Загрузки", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _currentConfiguration = new PluginConfiguration();
                _isDirty = false;
            }
            SetupDataBindings();
            TriggerValidation();
        }

        // Сохраняет текущую конфигурацию в XML-файл
        private void SaveConfiguration()
        {
            try
            {
                _currentConfiguration.PulldownButtonDefinitions = _pulldownDefinitionsBindingList.ToList();
                _currentConfiguration.Plugins = _pluginsBindingList.ToList();

                var serializer = new XmlSerializer(typeof(PluginConfiguration));
                using (var writer = new StreamWriter(_schemaFilePath, false, System.Text.Encoding.UTF8))
                {
                    var ns = new XmlSerializerNamespaces();
                    ns.Add("", "");
                    serializer.Serialize(writer, _currentConfiguration, ns);
                }
                MessageBox.Show("Схема успешно сохранена.", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isDirty = false;

                RefreshDataGridViewsFormatting();
                UpdatePulldownGroupComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения файла схемы: {ex.Message}", "Ошибка Сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Методы настройки UI и привязки данных

        // Настраивает привязки данных для элементов управления
        private void SetupDataBindings()
        {
            // Отписка от событий перед новой привязкой
            if (dgvPulldownDefinitions != null)
            {
                dgvPulldownDefinitions.SelectionChanged -= DgvPulldownDefinitions_SelectionChanged;
                dgvPulldownDefinitions.RowPrePaint -= DgvPulldownDefinitions_RowPrePaint;
            }
            if (dgvPlugins != null)
            {
                dgvPlugins.SelectionChanged -= DgvPlugins_SelectionChanged;
                dgvPlugins.RowPrePaint -= DgvPlugins_RowPrePaint;
            }
            if (dgvSubCommands != null) dgvSubCommands.SelectionChanged -= DgvSubCommands_SelectionChanged;

            _pulldownDefinitionsBindingList = new BindingList<PulldownButtonDefinitionInfo>(_currentConfiguration.PulldownButtonDefinitions ?? new List<PulldownButtonDefinitionInfo>());
            dgvPulldownDefinitions.AutoGenerateColumns = false;
            dgvPulldownDefinitions.DataSource = _pulldownDefinitionsBindingList;
            dgvPulldownDefinitions.SelectionChanged += DgvPulldownDefinitions_SelectionChanged;
            dgvPulldownDefinitions.RowPrePaint += DgvPulldownDefinitions_RowPrePaint;

            _pluginsBindingList = new BindingList<PluginInfo>(_currentConfiguration.Plugins ?? new List<PluginInfo>());
            dgvPlugins.AutoGenerateColumns = false;
            dgvPlugins.DataSource = _pluginsBindingList;
            dgvPlugins.SelectionChanged += DgvPlugins_SelectionChanged;
            dgvPlugins.RowPrePaint += DgvPlugins_RowPrePaint;

            dgvSubCommands.AutoGenerateColumns = false;
            dgvSubCommands.SelectionChanged += DgvSubCommands_SelectionChanged;

            UpdatePulldownGroupComboBox();
            propertyGridMain.SelectedObject = null;
            propertyGridSubCommands.SelectedObject = null;
            ClearSubCommandsDisplay();
            RefreshDataGridViewsFormatting();

            btnTogglePulldownEnable.Enabled = false;
            btnTogglePulldownEnable.Text = "Вкл/Выкл";
            btnTogglePluginEnable.Enabled = false;
            btnTogglePluginEnable.Text = "Вкл/Выкл";
        }

        // Обрабатывает изменение выбора в таблице определений PulldownButton
        private void DgvPulldownDefinitions_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvPulldownDefinitions.CurrentRow != null && dgvPulldownDefinitions.CurrentRow.DataBoundItem is PulldownButtonDefinitionInfo selectedPulldown)
            {
                propertyGridMain.SelectedObject = selectedPulldown;
                btnTogglePulldownEnable.Enabled = true;
                btnTogglePulldownEnable.Text = selectedPulldown.Enabled ? "Выключить" : "Включить";
            }
            else
            {
                propertyGridMain.SelectedObject = null;
                btnTogglePulldownEnable.Enabled = false;
                btnTogglePulldownEnable.Text = "Вкл/Выкл";
            }
            // При выборе PulldownButton, секция подкоманд всегда скрыта
            ClearSubCommandsDisplay();
            propertyGridSubCommands.SelectedObject = null;
            propertyGridSubCommands.Visible = false;
        }

        // Обрабатывает изменение выбора в таблице плагинов
        private void DgvPlugins_SelectionChanged(object sender, EventArgs e)
        {
            propertyGridSubCommands.SelectedObject = null; // Очищаем перед возможным новым выбором

            if (dgvPlugins.CurrentRow != null && dgvPlugins.CurrentRow.DataBoundItem is PluginInfo selectedPlugin)
            {
                propertyGridMain.SelectedObject = selectedPlugin;
                DisplaySubCommands(selectedPlugin); // Отобразит или скроет dgvSubCommands и propertyGridSubCommands
                UpdatePulldownGroupAssignmentControls(selectedPlugin);
                btnTogglePluginEnable.Enabled = true;
                btnTogglePluginEnable.Text = selectedPlugin.Enabled ? "Выключить" : "Включить";
            }
            else
            {
                propertyGridMain.SelectedObject = null;
                ClearSubCommandsDisplay();
                UpdatePulldownGroupAssignmentControls(null);
                btnTogglePluginEnable.Enabled = false;
                btnTogglePluginEnable.Text = "Вкл/Выкл";
            }
        }

        // Обрабатывает изменение выбора в таблице подкоманд
        private void DgvSubCommands_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvSubCommands.CurrentRow != null && dgvSubCommands.CurrentRow.DataBoundItem is SubCommandInfo selectedSubCommand)
            {
                propertyGridSubCommands.SelectedObject = selectedSubCommand;
                propertyGridSubCommands.Visible = true;
            }
            else
            {
                propertyGridSubCommands.SelectedObject = null;
            }
        }

        // Отображает подкоманды и соответствующий PropertyGrid для выбранного плагина
        private void DisplaySubCommands(PluginInfo plugin)
        {
            if (plugin != null && plugin.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                if (plugin.SubCommands == null) plugin.SubCommands = new List<SubCommandInfo>();
                var subCommandsBindingList = new BindingList<SubCommandInfo>(plugin.SubCommands);
                dgvSubCommands.DataSource = subCommandsBindingList;
                dgvSubCommands.Enabled = true;
                btnAddSubCommand.Enabled = true;
                btnRemoveSubCommand.Enabled = true;

                propertyGridSubCommands.Visible = true;
                // Если есть подкоманды, можно выбрать первую по умолчанию
                if (dgvSubCommands.Rows.Count > 0)
                {
                    dgvSubCommands.ClearSelection(); // Сначала очистить, чтобы событие сработало
                    dgvSubCommands.Rows[0].Selected = true;
                }
                else
                {
                    propertyGridSubCommands.SelectedObject = null; // Если подкоманд нет
                }
            }
            else
            {
                ClearSubCommandsDisplay();
            }
        }

        // Очищает отображение подкоманд и скрывает связанный PropertyGrid
        private void ClearSubCommandsDisplay()
        {
            dgvSubCommands.DataSource = null;
            dgvSubCommands.Enabled = false;
            btnAddSubCommand.Enabled = false;
            btnRemoveSubCommand.Enabled = false;

            propertyGridSubCommands.Visible = false;
            propertyGridSubCommands.SelectedObject = null;
        }

        // Обновляет список доступных групп PulldownButton
        private void UpdatePulldownGroupComboBox()
        {
            string previouslySelected = cmbPulldownGroups.SelectedItem as string;
            cmbPulldownGroups.Items.Clear();
            cmbPulldownGroups.Items.Add(""); // Пустой элемент для "без группы"
            if (_pulldownDefinitionsBindingList != null)
            {
                foreach (var pbd in _pulldownDefinitionsBindingList.Where(p => p.Enabled && !string.IsNullOrEmpty(p.Name)))
                {
                    cmbPulldownGroups.Items.Add(pbd.Name);
                }
            }

            bool selectionRestored = false;
            if (previouslySelected != null && cmbPulldownGroups.Items.Contains(previouslySelected))
            {
                cmbPulldownGroups.SelectedItem = previouslySelected;
                selectionRestored = true;
            }

            if (!selectionRestored && dgvPlugins.CurrentRow?.DataBoundItem is PluginInfo currentPlugin)
            {
                string groupToSelect = currentPlugin.PulldownGroupName ?? "";
                if (cmbPulldownGroups.Items.Contains(groupToSelect))
                {
                    cmbPulldownGroups.SelectedItem = groupToSelect;
                    selectionRestored = true;
                }
            }

            if (!selectionRestored)
            {
                if (cmbPulldownGroups.Items.Contains("")) cmbPulldownGroups.SelectedItem = "";
                else if (cmbPulldownGroups.Items.Count > 0) cmbPulldownGroups.SelectedIndex = 0;
            }
        }

        // Обновляет состояние элементов управления для назначения плагина группе
        private void UpdatePulldownGroupAssignmentControls(PluginInfo selectedPlugin)
        {
            if (selectedPlugin != null)
            {
                cmbPulldownGroups.Enabled = true;
                btnAssignPulldownGroup.Enabled = true;
                cmbPulldownGroups.SelectedItem = selectedPlugin.PulldownGroupName ?? "";
            }
            else
            {
                cmbPulldownGroups.Enabled = false;
                btnAssignPulldownGroup.Enabled = false;
                cmbPulldownGroups.SelectedItem = null;
            }
        }

        // Принудительно обновляет форматирование строк в DataGridViews
        private void RefreshDataGridViewsFormatting()
        {
            dgvPulldownDefinitions.Refresh();
            dgvPlugins.Refresh();
        }

        #endregion

        #region Методы валидации

        // Выполняет валидацию конфигурации
        private void PerformValidation()
        {
            if (_currentConfiguration == null) return;

            _currentErrors = _validator.ValidateConfiguration(_currentConfiguration, _basePath);
            UpdateValidationDisplay();
            UpdateValidationButtonState();
        }

        // Запускает валидацию с задержкой
        private void TriggerValidation()
        {
            _validationTimer.Stop();
            _validationTimer.Start();
        }

        // Обработчик таймера валидации
        private void ValidationTimer_Tick(object sender, EventArgs e)
        {
            _validationTimer.Stop();
            PerformValidation();
        }

        // Обновляет состояние кнопки валидации в зависимости от наличия ошибок
        private void UpdateValidationButtonState()
        {
            bool hasErrors = _currentErrors.Any(e => e.Severity == ValidationSeverity.Error);
            btnToggleValidation.BackColor = hasErrors ? Color.Salmon : SystemColors.Control;
        }

        // Обновляет отображение ошибок валидации
        private void UpdateValidationDisplay()
        {
            if (!_isValidationPanelVisible) return;

            lstValidationErrors.Items.Clear();

            // Группируем ошибки по типу объекта и имени плагина
            var errorGroups = _currentErrors
                .GroupBy(e => GetGroupKey(e))
                .OrderBy(g => g.Key);

            foreach (var group in errorGroups)
            {
                // Добавляем заголовок группы
                lstValidationErrors.Items.Add($"--- {group.Key} ---");

                // Добавляем ошибки группы
                foreach (var error in group.OrderBy(e => e.Severity))
                {
                    var errorItem = new ValidationErrorItem(error);
                    lstValidationErrors.Items.Add(errorItem);
                }

                // Добавляем разделитель
                lstValidationErrors.Items.Add(string.Empty);
            }

            // Подсвечиваем строки с ошибками
            HighlightErrorRows();
        }

        // Получает ключ группировки для ошибки валидации
        private string GetGroupKey(ValidationError error)
        {
            var contextParts = error.Context.Split('.');
            if (contextParts.Length >= 2)
            {
                string objectType = contextParts[0];
                string objectName = contextParts[1];

                // Для плагинов добавляем имя плагина
                if (objectType == "Plugin")
                {
                    var plugin = _currentConfiguration.Plugins?.FirstOrDefault(p => p.Name == objectName);
                    string displayName = plugin?.DisplayName ?? objectName;
                    return $"Плагин: {displayName} ({objectName})";
                }
                // Для PulldownButton
                else if (objectType == "Pulldown")
                {
                    var pulldown = _currentConfiguration.PulldownButtonDefinitions?.FirstOrDefault(p => p.Name == objectName);
                    string displayName = pulldown?.DisplayName ?? objectName;
                    return $"PulldownButton: {displayName} ({objectName})";
                }
            }

            return error.Context.Split('.').FirstOrDefault() ?? "Общие";
        }

        // Подсвечивает строки с ошибками
        private void HighlightErrorRows()
        {
            // Сброс цветов и подсветка ошибок для плагинов
            foreach (DataGridViewRow row in dgvPlugins.Rows)
            {
                if (row.DataBoundItem is PluginInfo plugin)
                {
                    var hasErrors = _currentErrors.Any(e => e.Context.Contains($"Plugin.{plugin.Name}") && e.Severity == ValidationSeverity.Error);
                    var hasWarnings = _currentErrors.Any(e => e.Context.Contains($"Plugin.{plugin.Name}") && e.Severity == ValidationSeverity.Warning);

                    if (hasErrors)
                    {
                        row.DefaultCellStyle.BackColor = _errorColor;
                        row.DefaultCellStyle.SelectionBackColor = Color.IndianRed;
                    }
                    else if (hasWarnings)
                    {
                        row.DefaultCellStyle.BackColor = _warningColor;
                        row.DefaultCellStyle.SelectionBackColor = Color.Khaki;
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = plugin.Enabled ? _enabledColor : _disabledColor;
                        row.DefaultCellStyle.SelectionBackColor = plugin.Enabled ? Color.DarkSeaGreen : Color.IndianRed;
                    }
                }
            }

            // Подсветка ошибок для PulldownButton
            foreach (DataGridViewRow row in dgvPulldownDefinitions.Rows)
            {
                if (row.DataBoundItem is PulldownButtonDefinitionInfo pulldown)
                {
                    var hasErrors = _currentErrors.Any(e => e.Context.Contains($"Pulldown.{pulldown.Name}") && e.Severity == ValidationSeverity.Error);
                    var hasWarnings = _currentErrors.Any(e => e.Context.Contains($"Pulldown.{pulldown.Name}") && e.Severity == ValidationSeverity.Warning);

                    if (hasErrors)
                    {
                        row.DefaultCellStyle.BackColor = _errorColor;
                        row.DefaultCellStyle.SelectionBackColor = Color.IndianRed;
                    }
                    else if (hasWarnings)
                    {
                        row.DefaultCellStyle.BackColor = _warningColor;
                        row.DefaultCellStyle.SelectionBackColor = Color.Khaki;
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = pulldown.Enabled ? _enabledColor : _disabledColor;
                        row.DefaultCellStyle.SelectionBackColor = pulldown.Enabled ? Color.DarkSeaGreen : Color.IndianRed;
                    }
                }
            }
        }

        #endregion

        #region Методы управления панелью валидации

        // Переключает видимость панели валидации
        private void ToggleValidationPanel()
        {
            _isValidationPanelVisible = !_isValidationPanelVisible;

            if (_isValidationPanelVisible)
            {
                ShowValidationPanel();
            }
            else
            {
                HideValidationPanel();
            }

            btnToggleValidation.Text = _isValidationPanelVisible ? "Скрыть ошибки" : "Показать ошибки";
        }

        // Показывает панель валидации
        private void ShowValidationPanel()
        {
            // Увеличиваем общую ширину формы
            this.Width += VALIDATION_PANEL_WIDTH;

            // Устанавливаем ширину второй панели
            splitContainer1.SplitterDistance = splitContainer1.Width - VALIDATION_PANEL_WIDTH;

            // Показываем панель валидации
            panelValidation.Visible = true;

            // Обновляем отображение ошибок
            UpdateValidationDisplay();
        }

        // Скрывает панель валидации
        private void HideValidationPanel()
        {
            // Скрываем панель валидации
            panelValidation.Visible = false;

            // Устанавливаем минимальную ширину второй панели
            splitContainer1.SplitterDistance = splitContainer1.Width - VALIDATION_PANEL_MIN_WIDTH;

            // Уменьшаем общую ширину формы
            this.Width -= VALIDATION_PANEL_WIDTH;
        }

        #endregion

        #region Методы сканирования плагинов

        // Открывает диалог сканирования плагинов
        private void ShowPluginScannerDialog()
        {
            using (var scanDialog = new PluginScanDialog(_basePath))
            {
                if (scanDialog.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (var plugin in scanDialog.SelectedPlugins)
                    {
                        _pluginsBindingList.Add(plugin);
                    }
                    _isDirty = true;
                    TriggerValidation();
                }
            }
        }

        #endregion

        #region Методы дублирования элементов

        // Дублирует выбранный элемент
        private void DuplicateSelectedItem()
        {
            if (tabControlMain.SelectedTab == tabPagePlugins && dgvPlugins.CurrentRow?.DataBoundItem is PluginInfo selectedPlugin)
            {
                DuplicatePlugin(selectedPlugin);
            }
            else if (tabControlMain.SelectedTab == tabPagePulldowns && dgvPulldownDefinitions.CurrentRow?.DataBoundItem is PulldownButtonDefinitionInfo selectedPulldown)
            {
                DuplicatePulldownDefinition(selectedPulldown);
            }
        }

        // Дублирует плагин
        private void DuplicatePlugin(PluginInfo original)
        {
            var duplicate = new PluginInfo
            {
                Name = $"{original.Name}_Copy_{DateTime.Now:HHmmss}",
                DisplayName = $"{original.DisplayName} (Копия)",
                AssemblyPath = original.AssemblyPath,
                ClassName = original.ClassName,
                Description = original.Description,
                RibbonTab = original.RibbonTab,
                RibbonPanel = original.RibbonPanel,
                UIType = original.UIType,
                Enabled = original.Enabled,
                LoadOnStartup = original.LoadOnStartup,
                Version = original.Version,
                LargeIcon = original.LargeIcon,
                SmallIcon = original.SmallIcon,
                PulldownGroupName = original.PulldownGroupName
            };

            if (original.SubCommands != null)
            {
                duplicate.SubCommands = original.SubCommands.Select(sc => new SubCommandInfo
                {
                    Name = sc.Name,
                    DisplayName = sc.DisplayName,
                    ClassName = sc.ClassName,
                    Description = sc.Description,
                    LargeIcon = sc.LargeIcon,
                    SmallIcon = sc.SmallIcon
                }).ToList();
            }

            _pluginsBindingList.Add(duplicate);
            _isDirty = true;
            TriggerValidation();

            // Выбираем новый плагин
            dgvPlugins.ClearSelection();
            dgvPlugins.Rows[_pluginsBindingList.Count - 1].Selected = true;
            dgvPlugins.FirstDisplayedScrollingRowIndex = _pluginsBindingList.Count - 1;
        }

        // Дублирует определение PulldownButton
        private void DuplicatePulldownDefinition(PulldownButtonDefinitionInfo original)
        {
            var duplicate = new PulldownButtonDefinitionInfo
            {
                Name = $"{original.Name}_Copy_{DateTime.Now:HHmmss}",
                DisplayName = $"{original.DisplayName} (Копия)",
                RibbonTab = original.RibbonTab,
                RibbonPanel = original.RibbonPanel,
                Description = original.Description,
                Enabled = original.Enabled,
                LargeIcon = original.LargeIcon,
                SmallIcon = original.SmallIcon
            };

            _pulldownDefinitionsBindingList.Add(duplicate);
            _isDirty = true;
            TriggerValidation();

            // Выбираем новый PulldownButton
            dgvPulldownDefinitions.ClearSelection();
            dgvPulldownDefinitions.Rows[_pulldownDefinitionsBindingList.Count - 1].Selected = true;
            dgvPulldownDefinitions.FirstDisplayedScrollingRowIndex = _pulldownDefinitionsBindingList.Count - 1;

            // Обновляем список доступных групп
            UpdatePulldownGroupComboBox();
        }

        #endregion

        #region Методы Drag & Drop

        // Настраивает Drag & Drop для DataGridView
        private void SetupDragAndDrop()
        {
            dgvPlugins.AllowDrop = true;
            dgvPlugins.DragEnter += DgvPlugins_DragEnter;
            dgvPlugins.DragDrop += DgvPlugins_DragDrop;
            dgvPlugins.MouseDown += DgvPlugins_MouseDown;
            dgvPlugins.MouseMove += DgvPlugins_MouseMove;

            dgvPulldownDefinitions.AllowDrop = true;
            dgvPulldownDefinitions.DragEnter += DgvPulldownDefinitions_DragEnter;
            dgvPulldownDefinitions.DragDrop += DgvPulldownDefinitions_DragDrop;
        }

        // Обрабатывает начало перемещения с мыши
        private void DgvPlugins_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragStartPosition = e.Location;
            }
        }

        // Обрабатывает движение мыши для определения начала перетаскивания
        private void DgvPlugins_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragStartPosition.HasValue)
            {
                // Проверяем, достаточно ли далеко ушли от начальной точки
                var dragSize = SystemInformation.DragSize;
                var dragRect = new Rectangle(
                    _dragStartPosition.Value.X - dragSize.Width / 2,
                    _dragStartPosition.Value.Y - dragSize.Height / 2,
                    dragSize.Width, dragSize.Height);

                if (!dragRect.Contains(e.Location))
                {
                    var hitTest = dgvPlugins.HitTest(_dragStartPosition.Value.X, _dragStartPosition.Value.Y);
                    if (hitTest.RowIndex >= 0 && dgvPlugins.Rows[hitTest.RowIndex].DataBoundItem is PluginInfo plugin)
                    {
                        // Сбрасываем позицию, чтобы предотвратить повторные вызовы DoDragDrop
                        _dragStartPosition = null;
                        dgvPlugins.DoDragDrop(plugin, DragDropEffects.Move | DragDropEffects.Link);
                    }
                }
            }
        }

        // Обрабатывает вход в зону перетаскивания для плагинов
        private void DgvPlugins_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PluginInfo)))
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        // Обрабатывает сброс в dgvPlugins для реорганизации порядка
        private void DgvPlugins_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(PluginInfo)) is PluginInfo draggedPlugin)
            {
                var clientPoint = dgvPlugins.PointToClient(new Point(e.X, e.Y));
                var hitTest = dgvPlugins.HitTest(clientPoint.X, clientPoint.Y);

                if (hitTest.RowIndex >= 0)
                {
                    // Реорганизуем порядок плагинов
                    ReorderPlugin(draggedPlugin, hitTest.RowIndex);
                }
            }
        }

        // Обрабатывает вход в зону перетаскивания для PulldownButton
        private void DgvPulldownDefinitions_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PluginInfo)))
            {
                e.Effect = DragDropEffects.Link;
            }
        }

        // Обрабатывает сброс в dgvPulldownDefinitions для назначения группы
        private void DgvPulldownDefinitions_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(PluginInfo)) is PluginInfo draggedPlugin)
            {
                var clientPoint = dgvPulldownDefinitions.PointToClient(new Point(e.X, e.Y));
                var hitTest = dgvPulldownDefinitions.HitTest(clientPoint.X, clientPoint.Y);

                if (hitTest.RowIndex >= 0 && dgvPulldownDefinitions.Rows[hitTest.RowIndex].DataBoundItem is PulldownButtonDefinitionInfo pulldown)
                {
                    AssignPluginToPulldownGroup(draggedPlugin, pulldown.Name);
                }
            }
        }

        // Изменяет порядок плагинов
        private void ReorderPlugin(PluginInfo plugin, int newIndex)
        {
            var currentIndex = _pluginsBindingList.IndexOf(plugin);
            if (currentIndex >= 0 && currentIndex != newIndex)
            {
                _pluginsBindingList.RemoveAt(currentIndex);
                if (newIndex > currentIndex) newIndex--;
                _pluginsBindingList.Insert(newIndex, plugin);
                _isDirty = true;

                // Выбираем перемещенный плагин
                dgvPlugins.ClearSelection();
                dgvPlugins.Rows[newIndex].Selected = true;

                TriggerValidation();
            }
        }

        // Назначает плагин в группу PulldownButton
        private void AssignPluginToPulldownGroup(PluginInfo plugin, string groupName)
        {
            if (plugin.PulldownGroupName != groupName)
            {
                plugin.PulldownGroupName = groupName;
                _pluginsBindingList.ResetItem(_pluginsBindingList.IndexOf(plugin));
                _isDirty = true;

                // Также обновляем комбобокс, если плагин сейчас выбран
                if (dgvPlugins.CurrentRow?.DataBoundItem == plugin)
                {
                    cmbPulldownGroups.SelectedItem = groupName;
                }

                TriggerValidation();

                // Показываем сообщение об успешной привязке
                var targetPulldown = _pulldownDefinitionsBindingList.FirstOrDefault(p => p.Name == groupName);
                MessageBox.Show($"Плагин '{plugin.DisplayName}' привязан к группе '{targetPulldown?.DisplayName ?? groupName}'",
                    "Привязка успешна", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #endregion

        #region Обработчики событий

        #region Обработчики событий формы

        // Обрабатывает событие закрытия формы
        private void SchemaEditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("Есть несохраненные изменения. Сохранить их перед закрытием?",
                                             "Несохраненные изменения",
                                             MessageBoxButtons.YesNoCancel,
                                             MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    SaveConfiguration();
                    if (_isDirty) e.Cancel = true; // Если сохранение не удалось, отменяем закрытие
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        #endregion

        #region Обработчики событий PropertyGrid

        // Обрабатывает изменение значения свойства в любом из PropertyGrid
        private void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            _isDirty = true;
            var selectedObject = (s as PropertyGrid)?.SelectedObject;

            if (e.ChangedItem.PropertyDescriptor.Name == "Enabled")
            {
                if (selectedObject is PulldownButtonDefinitionInfo pbd)
                {
                    btnTogglePulldownEnable.Text = pbd.Enabled ? "Выключить" : "Включить";
                    RefreshDataGridViewsFormatting();
                    UpdatePulldownGroupComboBox(); // Обновляем список доступных групп
                }
                else if (selectedObject is PluginInfo plugin)
                {
                    btnTogglePluginEnable.Text = plugin.Enabled ? "Выключить" : "Включить";
                    RefreshDataGridViewsFormatting();
                }
            }
            else if (e.ChangedItem.PropertyDescriptor.Name == "UIType" && selectedObject is PluginInfo plugin)
            {
                DisplaySubCommands(plugin); // Обновляем видимость dgvSubCommands и propertyGridSubCommands
                _pluginsBindingList.ResetItem(_pluginsBindingList.IndexOf(plugin)); // Обновляем строку в гриде плагинов
            }
            else if (e.ChangedItem.PropertyDescriptor.Name == "Name" && selectedObject is PulldownButtonDefinitionInfo)
            {
                UpdatePulldownGroupComboBox(); // Имя группы изменилось, обновляем комбобокс
            }

            // Обновляем соответствующий DataGridView, если изменилось имя или отображаемое имя
            if (e.ChangedItem.PropertyDescriptor.Name == "Name" || e.ChangedItem.PropertyDescriptor.Name == "DisplayName")
            {
                if (selectedObject is PulldownButtonDefinitionInfo pbdInfo)
                    _pulldownDefinitionsBindingList.ResetItem(_pulldownDefinitionsBindingList.IndexOf(pbdInfo));
                else if (selectedObject is PluginInfo pluginInfo)
                    _pluginsBindingList.ResetItem(_pluginsBindingList.IndexOf(pluginInfo));
                else if (selectedObject is SubCommandInfo subCmdInfo && dgvPlugins.CurrentRow?.DataBoundItem is PluginInfo parentPlugin)
                {
                    var subCmdList = dgvSubCommands.DataSource as BindingList<SubCommandInfo>;
                    if (subCmdList != null)
                    {
                        int index = subCmdList.IndexOf(subCmdInfo);
                        if (index >= 0) subCmdList.ResetItem(index);
                    }
                }
            }

            // Запускаем валидацию с задержкой после изменения свойства
            TriggerValidation();
        }

        #endregion

        #region Обработчики кнопок

        // Обрабатывает нажатие кнопки "Открыть файл..."
        private void btnOpenFile_Click(object sender, EventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("Есть несохраненные изменения. Сохранить их перед открытием нового файла?",
                                             "Несохраненные изменения",
                                             MessageBoxButtons.YesNoCancel,
                                             MessageBoxIcon.Warning);
                if (result == DialogResult.Yes) SaveConfiguration();
                else if (result == DialogResult.Cancel) return;
            }

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(_schemaFilePath);
                openFileDialog.FileName = Path.GetFileName(_schemaFilePath);
                openFileDialog.Filter = "MagicEntry Schema Files (*.xml)|*.xml|All files (*.*)|*.*";
                openFileDialog.Title = "Выберите файл схемы MagicEntry";
                openFileDialog.CheckFileExists = true;

                if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadConfigurationFromFile(openFileDialog.FileName);
                }
            }
        }

        // Обрабатывает нажатие кнопки "Сохранить"
        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveConfiguration();
        }

        // Обрабатывает нажатие кнопки "Закрыть"
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Обрабатывает нажатие кнопки "Добавить" для определений PulldownButton
        private void btnAddPulldown_Click(object sender, EventArgs e)
        {
            var newPulldown = new PulldownButtonDefinitionInfo { Name = "NewPulldown", DisplayName = "Новый Pulldown", RibbonTab = "MagicEntry", RibbonPanel = "Панель", Enabled = true };
            _pulldownDefinitionsBindingList.Add(newPulldown);
            _isDirty = true;
            if (dgvPulldownDefinitions.Rows.Count > 0)
            {
                dgvPulldownDefinitions.ClearSelection();
                dgvPulldownDefinitions.Rows[dgvPulldownDefinitions.Rows.Count - 1].Selected = true;
            }
            TriggerValidation();
        }

        // Обрабатывает нажатие кнопки "Удалить" для определений PulldownButton
        private void btnRemovePulldown_Click(object sender, EventArgs e)
        {
            if (dgvPulldownDefinitions.CurrentRow != null && dgvPulldownDefinitions.CurrentRow.DataBoundItem is PulldownButtonDefinitionInfo selected)
            {
                if (MessageBox.Show($"Удалить определение Pulldown '{selected.DisplayName}'?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _pulldownDefinitionsBindingList.Remove(selected);
                    _isDirty = true;
                    TriggerValidation();
                    UpdatePulldownGroupComboBox();
                }
            }
        }

        // Обрабатывает нажатие кнопки "Включить/Выключить" для определений PulldownButton
        private void btnTogglePulldownEnable_Click(object sender, EventArgs e)
        {
            if (dgvPulldownDefinitions.CurrentRow != null && dgvPulldownDefinitions.CurrentRow.DataBoundItem is PulldownButtonDefinitionInfo selectedPulldown)
            {
                selectedPulldown.Enabled = !selectedPulldown.Enabled;
                _isDirty = true;
                btnTogglePulldownEnable.Text = selectedPulldown.Enabled ? "Выключить" : "Включить";
                _pulldownDefinitionsBindingList.ResetItem(_pulldownDefinitionsBindingList.IndexOf(selectedPulldown));
                RefreshDataGridViewsFormatting();
                UpdatePulldownGroupComboBox();
                propertyGridMain.Refresh();
                TriggerValidation();
            }
        }

        // Обрабатывает нажатие кнопки "Добавить" для плагинов
        private void btnAddPlugin_Click(object sender, EventArgs e)
        {
            using (var typeDialog = new Form())
            {
                typeDialog.Text = "Выберите тип плагина";
                typeDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                typeDialog.StartPosition = FormStartPosition.CenterParent;
                typeDialog.ClientSize = new System.Drawing.Size(200, 100);

                var rbPush = new RadioButton { Text = "PushButton", Location = new System.Drawing.Point(10, 10), Checked = true, AutoSize = true };
                var rbSplit = new RadioButton { Text = "SplitButton", Location = new System.Drawing.Point(10, 35), AutoSize = true };
                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(110, 65) };

                typeDialog.Controls.AddRange(new Control[] { rbPush, rbSplit, btnOk });
                typeDialog.AcceptButton = btnOk;

                if (typeDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var newPlugin = new PluginInfo
                    {
                        Name = "NewPlugin",
                        DisplayName = "Новый Плагин",
                        AssemblyPath = "Plugins\\NewPlugin\\NewPlugin.dll",
                        ClassName = "Namespace.NewPluginCommand",
                        RibbonTab = "MagicEntry",
                        RibbonPanel = "Панель",
                        UIType = rbSplit.Checked ? PluginInfo.ButtonUIType.SplitButton : PluginInfo.ButtonUIType.PushButton,
                        Enabled = true
                    };
                    if (newPlugin.UIType == PluginInfo.ButtonUIType.SplitButton)
                    {
                        newPlugin.SubCommands = new List<SubCommandInfo>();
                    }
                    _pluginsBindingList.Add(newPlugin);
                    _isDirty = true;
                    if (dgvPlugins.Rows.Count > 0)
                    {
                        dgvPlugins.ClearSelection();
                        dgvPlugins.Rows[dgvPlugins.Rows.Count - 1].Selected = true;
                    }
                    TriggerValidation();
                }
            }
        }

        // Обрабатывает нажатие кнопки "Удалить" для плагинов
        private void btnRemovePlugin_Click(object sender, EventArgs e)
        {
            if (dgvPlugins.CurrentRow != null && dgvPlugins.CurrentRow.DataBoundItem is PluginInfo selected)
            {
                if (MessageBox.Show($"Удалить плагин '{selected.DisplayName}'?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _pluginsBindingList.Remove(selected);
                    _isDirty = true;
                    TriggerValidation();
                }
            }
        }

        // Обрабатывает нажатие кнопки "Включить/Выключить" для плагинов
        private void btnTogglePluginEnable_Click(object sender, EventArgs e)
        {
            if (dgvPlugins.CurrentRow != null && dgvPlugins.CurrentRow.DataBoundItem is PluginInfo selectedPlugin)
            {
                selectedPlugin.Enabled = !selectedPlugin.Enabled;
                _isDirty = true;
                btnTogglePluginEnable.Text = selectedPlugin.Enabled ? "Выключить" : "Включить";
                _pluginsBindingList.ResetItem(_pluginsBindingList.IndexOf(selectedPlugin));
                RefreshDataGridViewsFormatting();
                propertyGridMain.Refresh();
                TriggerValidation();
            }
        }

        // Обрабатывает нажатие кнопки "Назначить" для привязки плагина к группе
        private void btnAssignPulldownGroup_Click(object sender, EventArgs e)
        {
            if (dgvPlugins.CurrentRow != null && dgvPlugins.CurrentRow.DataBoundItem is PluginInfo selectedPlugin)
            {
                string selectedGroup = cmbPulldownGroups.SelectedItem as string ?? "";
                if (selectedPlugin.PulldownGroupName != selectedGroup)
                {
                    selectedPlugin.PulldownGroupName = selectedGroup;
                    _isDirty = true;
                    _pluginsBindingList.ResetItem(_pluginsBindingList.IndexOf(selectedPlugin));
                    propertyGridMain.Refresh(); // Обновляем основной PropertyGrid, т.к. изменилось свойство плагина
                    TriggerValidation();
                }
            }
        }

        // Обрабатывает нажатие кнопки "Добавить" для подкоманд
        private void btnAddSubCommand_Click(object sender, EventArgs e)
        {
            if (dgvPlugins.CurrentRow != null && dgvPlugins.CurrentRow.DataBoundItem is PluginInfo selectedPlugin)
            {
                if (selectedPlugin.UIType == PluginInfo.ButtonUIType.SplitButton)
                {
                    var newSubCommand = new SubCommandInfo { Name = "NewSubCmd", DisplayName = "Новая Подкоманда", ClassName = "Namespace.NewSubCommand" };
                    if (selectedPlugin.SubCommands == null) selectedPlugin.SubCommands = new List<SubCommandInfo>();

                    selectedPlugin.SubCommands.Add(newSubCommand);
                    _isDirty = true;
                    // Обновляем DataSource для dgvSubCommands, чтобы он перерисовался
                    var subCommandsBindingList = new BindingList<SubCommandInfo>(selectedPlugin.SubCommands);
                    dgvSubCommands.DataSource = subCommandsBindingList;


                    if (dgvSubCommands.Rows.Count > 0)
                    {
                        dgvSubCommands.ClearSelection();
                        dgvSubCommands.Rows[dgvSubCommands.Rows.Count - 1].Selected = true;
                    }
                    TriggerValidation();
                }
            }
        }

        // Обрабатывает нажатие кнопки "Удалить" для подкоманд
        private void btnRemoveSubCommand_Click(object sender, EventArgs e)
        {
            if (dgvPlugins.CurrentRow != null && dgvPlugins.CurrentRow.DataBoundItem is PluginInfo selectedPlugin &&
                dgvSubCommands.CurrentRow != null && dgvSubCommands.CurrentRow.DataBoundItem is SubCommandInfo selectedSubCommand)
            {
                if (MessageBox.Show($"Удалить подкоманду '{selectedSubCommand.DisplayName}' из плагина '{selectedPlugin.DisplayName}'?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (selectedPlugin.SubCommands != null)
                    {
                        selectedPlugin.SubCommands.Remove(selectedSubCommand);
                        _isDirty = true;
                        // Обновляем DataSource для dgvSubCommands
                        var subCommandsBindingList = new BindingList<SubCommandInfo>(selectedPlugin.SubCommands);
                        dgvSubCommands.DataSource = subCommandsBindingList;

                        if (dgvSubCommands.Rows.Count == 0)
                        {
                            propertyGridSubCommands.SelectedObject = null;
                        }
                        else
                        {
                            // Можно выбрать предыдущую или первую, если есть
                            dgvSubCommands.ClearSelection();
                            if (dgvSubCommands.Rows.Count > 0)
                                dgvSubCommands.Rows[0].Selected = true;
                        }
                        TriggerValidation();
                    }
                }
            }
        }

        // Обрабатывает нажатие кнопки "Сканировать"
        private void btnScanPlugins_Click(object sender, EventArgs e)
        {
            ShowPluginScannerDialog();
        }

        // Обрабатывает нажатие кнопки "Дублировать"
        private void btnDuplicate_Click(object sender, EventArgs e)
        {
            DuplicateSelectedItem();
        }

        // Обрабатывает нажатие кнопки "Скрыть/Показать" для панели валидации
        private void btnToggleValidation_Click(object sender, EventArgs e)
        {
            ToggleValidationPanel();
        }

        #endregion

        #region Обработчики ListBox

        // Обрабатывает отрисовку элементов в списке ошибок валидации
        private void lstValidationErrors_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            var item = lstValidationErrors.Items[e.Index];
            Color textColor = SystemColors.ControlText;

            // Подсвечиваем только ошибки
            if (item is ValidationErrorItem errorItem && errorItem.Error.Severity == ValidationSeverity.Error)
            {
                using (var brush = new SolidBrush(Color.Salmon))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
            }

            using (var brush = new SolidBrush(textColor))
            {
                string text = item?.ToString() ?? string.Empty;
                e.Graphics.DrawString(text, e.Font, brush, e.Bounds, StringFormat.GenericDefault);
            }

            e.DrawFocusRectangle();
        }

        #endregion

        #region Обработчики DataGridView

        // Осуществляет кастомную отрисовку строк в таблице определений PulldownButton
        private void DgvPulldownDefinitions_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvPulldownDefinitions.Rows.Count) return;
            var row = dgvPulldownDefinitions.Rows[e.RowIndex];
            if (row.DataBoundItem is PulldownButtonDefinitionInfo pbd)
            {
                // Проверяем наличие ошибок валидации
                var hasErrors = _currentErrors.Any(er => er.Context.Contains($"Pulldown.{pbd.Name}") && er.Severity == ValidationSeverity.Error);
                var hasWarnings = _currentErrors.Any(er => er.Context.Contains($"Pulldown.{pbd.Name}") && er.Severity == ValidationSeverity.Warning);

                if (hasErrors)
                {
                    row.DefaultCellStyle.BackColor = _errorColor;
                    row.DefaultCellStyle.SelectionBackColor = Color.IndianRed;
                }
                else if (hasWarnings)
                {
                    row.DefaultCellStyle.BackColor = _warningColor;
                    row.DefaultCellStyle.SelectionBackColor = Color.Khaki;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = pbd.Enabled ? _enabledColor : _disabledColor;
                    row.DefaultCellStyle.SelectionBackColor = pbd.Enabled ? Color.DarkSeaGreen : Color.IndianRed;
                }
            }
            else
            {
                row.DefaultCellStyle.BackColor = _defaultRowColor;
                row.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            }
        }

        // Осуществляет кастомную отрисовку строк в таблице плагинов
        private void DgvPlugins_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvPlugins.Rows.Count) return;
            var row = dgvPlugins.Rows[e.RowIndex];
            if (row.DataBoundItem is PluginInfo plugin)
            {
                // Проверяем наличие ошибок валидации
                var hasErrors = _currentErrors.Any(er => er.Context.Contains($"Plugin.{plugin.Name}") && er.Severity == ValidationSeverity.Error);
                var hasWarnings = _currentErrors.Any(er => er.Context.Contains($"Plugin.{plugin.Name}") && er.Severity == ValidationSeverity.Warning);

                if (hasErrors)
                {
                    row.DefaultCellStyle.BackColor = _errorColor;
                    row.DefaultCellStyle.SelectionBackColor = Color.IndianRed;
                }
                else if (hasWarnings)
                {
                    row.DefaultCellStyle.BackColor = _warningColor;
                    row.DefaultCellStyle.SelectionBackColor = Color.Khaki;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = plugin.Enabled ? _enabledColor : _disabledColor;
                    row.DefaultCellStyle.SelectionBackColor = plugin.Enabled ? Color.DarkSeaGreen : Color.IndianRed;
                }
            }
            else
            {
                row.DefaultCellStyle.BackColor = _defaultRowColor;
                row.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            }
        }

        #endregion

        #endregion

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPagePulldowns = new System.Windows.Forms.TabPage();
            this.dgvPulldownDefinitions = new System.Windows.Forms.DataGridView();
            this.colPulldownName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPulldownDisplayName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPulldownTab = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPulldownPanel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPulldownEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.panelPulldownButtons = new System.Windows.Forms.Panel();
            this.btnTogglePulldownEnable = new System.Windows.Forms.Button();
            this.btnRemovePulldown = new System.Windows.Forms.Button();
            this.btnAddPulldown = new System.Windows.Forms.Button();
            this.tabPagePlugins = new System.Windows.Forms.TabPage();
            this.splitContainerPlugins = new System.Windows.Forms.SplitContainer();
            this.dgvPlugins = new System.Windows.Forms.DataGridView();
            this.colPluginName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPluginDisplayName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPluginUIType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPluginPulldownGroup = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelPluginActions = new System.Windows.Forms.Panel();
            this.btnTogglePluginEnable = new System.Windows.Forms.Button();
            this.btnAssignPulldownGroup = new System.Windows.Forms.Button();
            this.cmbPulldownGroups = new System.Windows.Forms.ComboBox();
            this.btnDuplicate = new System.Windows.Forms.Button();
            this.btnRemovePlugin = new System.Windows.Forms.Button();
            this.btnAddPlugin = new System.Windows.Forms.Button();
            this.groupBoxSubCommands = new System.Windows.Forms.GroupBox();
            this.dgvSubCommands = new System.Windows.Forms.DataGridView();
            this.colSubCmdName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSubCmdDisplayName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelSubCommandButtons = new System.Windows.Forms.Panel();
            this.btnRemoveSubCommand = new System.Windows.Forms.Button();
            this.btnAddSubCommand = new System.Windows.Forms.Button();
            this.btnScanPlugins = new System.Windows.Forms.Button();
            this.btnOpenFile = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.splitContainerProperties = new System.Windows.Forms.SplitContainer();
            this.propertyGridMain = new System.Windows.Forms.PropertyGrid();
            this.btnToggleValidation = new System.Windows.Forms.Button();
            this.propertyGridSubCommands = new System.Windows.Forms.PropertyGrid();
            this.panelValidation = new System.Windows.Forms.Panel();
            this.lstValidationErrors = new System.Windows.Forms.ListBox();
            this.lblValidationTitle = new System.Windows.Forms.Label();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControlMain.SuspendLayout();
            this.tabPagePulldowns.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPulldownDefinitions)).BeginInit();
            this.panelPulldownButtons.SuspendLayout();
            this.tabPagePlugins.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerPlugins)).BeginInit();
            this.splitContainerPlugins.Panel1.SuspendLayout();
            this.splitContainerPlugins.Panel2.SuspendLayout();
            this.splitContainerPlugins.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPlugins)).BeginInit();
            this.panelPluginActions.SuspendLayout();
            this.groupBoxSubCommands.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSubCommands)).BeginInit();
            this.panelSubCommandButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerProperties)).BeginInit();
            this.splitContainerProperties.Panel1.SuspendLayout();
            this.splitContainerProperties.Panel2.SuspendLayout();
            this.splitContainerProperties.SuspendLayout();
            this.panelValidation.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabPagePulldowns);
            this.tabControlMain.Controls.Add(this.tabPagePlugins);
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Left;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(637, 896);
            this.tabControlMain.TabIndex = 0;
            // 
            // tabPagePulldowns
            // 
            this.tabPagePulldowns.Controls.Add(this.dgvPulldownDefinitions);
            this.tabPagePulldowns.Controls.Add(this.panelPulldownButtons);
            this.tabPagePulldowns.Location = new System.Drawing.Point(4, 22);
            this.tabPagePulldowns.Name = "tabPagePulldowns";
            this.tabPagePulldowns.Padding = new System.Windows.Forms.Padding(3);
            this.tabPagePulldowns.Size = new System.Drawing.Size(629, 870);
            this.tabPagePulldowns.TabIndex = 0;
            this.tabPagePulldowns.Text = "Pulldown Buttons";
            this.tabPagePulldowns.UseVisualStyleBackColor = true;
            // 
            // dgvPulldownDefinitions
            // 
            this.dgvPulldownDefinitions.AllowUserToAddRows = false;
            this.dgvPulldownDefinitions.AllowUserToDeleteRows = false;
            this.dgvPulldownDefinitions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPulldownDefinitions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPulldownName,
            this.colPulldownDisplayName,
            this.colPulldownTab,
            this.colPulldownPanel,
            this.colPulldownEnabled});
            this.dgvPulldownDefinitions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPulldownDefinitions.Location = new System.Drawing.Point(3, 33);
            this.dgvPulldownDefinitions.MultiSelect = false;
            this.dgvPulldownDefinitions.Name = "dgvPulldownDefinitions";
            this.dgvPulldownDefinitions.ReadOnly = true;
            this.dgvPulldownDefinitions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvPulldownDefinitions.Size = new System.Drawing.Size(623, 834);
            this.dgvPulldownDefinitions.TabIndex = 1;
            // 
            // colPulldownName
            // 
            this.colPulldownName.DataPropertyName = "Name";
            this.colPulldownName.HeaderText = "ID";
            this.colPulldownName.Name = "colPulldownName";
            this.colPulldownName.ReadOnly = true;
            // 
            // colPulldownDisplayName
            // 
            this.colPulldownDisplayName.DataPropertyName = "DisplayName";
            this.colPulldownDisplayName.HeaderText = "Отображаемое имя";
            this.colPulldownDisplayName.Name = "colPulldownDisplayName";
            this.colPulldownDisplayName.ReadOnly = true;
            this.colPulldownDisplayName.Width = 150;
            // 
            // colPulldownTab
            // 
            this.colPulldownTab.DataPropertyName = "RibbonTab";
            this.colPulldownTab.HeaderText = "Вкладка";
            this.colPulldownTab.Name = "colPulldownTab";
            this.colPulldownTab.ReadOnly = true;
            // 
            // colPulldownPanel
            // 
            this.colPulldownPanel.DataPropertyName = "RibbonPanel";
            this.colPulldownPanel.HeaderText = "Панель";
            this.colPulldownPanel.Name = "colPulldownPanel";
            this.colPulldownPanel.ReadOnly = true;
            // 
            // colPulldownEnabled
            // 
            this.colPulldownEnabled.DataPropertyName = "Enabled";
            this.colPulldownEnabled.HeaderText = "Активен";
            this.colPulldownEnabled.Name = "colPulldownEnabled";
            this.colPulldownEnabled.ReadOnly = true;
            this.colPulldownEnabled.Width = 60;
            // 
            // panelPulldownButtons
            // 
            this.panelPulldownButtons.Controls.Add(this.btnTogglePulldownEnable);
            this.panelPulldownButtons.Controls.Add(this.btnRemovePulldown);
            this.panelPulldownButtons.Controls.Add(this.btnAddPulldown);
            this.panelPulldownButtons.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPulldownButtons.Location = new System.Drawing.Point(3, 3);
            this.panelPulldownButtons.Name = "panelPulldownButtons";
            this.panelPulldownButtons.Size = new System.Drawing.Size(623, 30);
            this.panelPulldownButtons.TabIndex = 0;
            // 
            // btnTogglePulldownEnable
            // 
            this.btnTogglePulldownEnable.Enabled = false;
            this.btnTogglePulldownEnable.Location = new System.Drawing.Point(165, 3);
            this.btnTogglePulldownEnable.Name = "btnTogglePulldownEnable";
            this.btnTogglePulldownEnable.Size = new System.Drawing.Size(120, 23);
            this.btnTogglePulldownEnable.TabIndex = 2;
            this.btnTogglePulldownEnable.Text = "Вкл/Выкл";
            this.btnTogglePulldownEnable.UseVisualStyleBackColor = true;
            this.btnTogglePulldownEnable.Click += new System.EventHandler(this.btnTogglePulldownEnable_Click);
            // 
            // btnRemovePulldown
            // 
            this.btnRemovePulldown.Location = new System.Drawing.Point(84, 3);
            this.btnRemovePulldown.Name = "btnRemovePulldown";
            this.btnRemovePulldown.Size = new System.Drawing.Size(75, 23);
            this.btnRemovePulldown.TabIndex = 1;
            this.btnRemovePulldown.Text = "Удалить";
            this.btnRemovePulldown.UseVisualStyleBackColor = true;
            this.btnRemovePulldown.Click += new System.EventHandler(this.btnRemovePulldown_Click);
            // 
            // btnAddPulldown
            // 
            this.btnAddPulldown.Location = new System.Drawing.Point(3, 3);
            this.btnAddPulldown.Name = "btnAddPulldown";
            this.btnAddPulldown.Size = new System.Drawing.Size(75, 23);
            this.btnAddPulldown.TabIndex = 0;
            this.btnAddPulldown.Text = "Добавить";
            this.btnAddPulldown.UseVisualStyleBackColor = true;
            this.btnAddPulldown.Click += new System.EventHandler(this.btnAddPulldown_Click);
            // 
            // tabPagePlugins
            // 
            this.tabPagePlugins.Controls.Add(this.splitContainerPlugins);
            this.tabPagePlugins.Location = new System.Drawing.Point(4, 22);
            this.tabPagePlugins.Name = "tabPagePlugins";
            this.tabPagePlugins.Padding = new System.Windows.Forms.Padding(3);
            this.tabPagePlugins.Size = new System.Drawing.Size(629, 870);
            this.tabPagePlugins.TabIndex = 1;
            this.tabPagePlugins.Text = "Плагины";
            this.tabPagePlugins.UseVisualStyleBackColor = true;
            // 
            // splitContainerPlugins
            // 
            this.splitContainerPlugins.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerPlugins.Location = new System.Drawing.Point(3, 3);
            this.splitContainerPlugins.Name = "splitContainerPlugins";
            this.splitContainerPlugins.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerPlugins.Panel1
            // 
            this.splitContainerPlugins.Panel1.Controls.Add(this.dgvPlugins);
            this.splitContainerPlugins.Panel1.Controls.Add(this.panelPluginActions);
            // 
            // splitContainerPlugins.Panel2
            // 
            this.splitContainerPlugins.Panel2.Controls.Add(this.groupBoxSubCommands);
            this.splitContainerPlugins.Size = new System.Drawing.Size(623, 864);
            this.splitContainerPlugins.SplitterDistance = 415;
            this.splitContainerPlugins.TabIndex = 0;
            // 
            // dgvPlugins
            // 
            this.dgvPlugins.AllowUserToAddRows = false;
            this.dgvPlugins.AllowUserToDeleteRows = false;
            this.dgvPlugins.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPlugins.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPluginName,
            this.colPluginDisplayName,
            this.colPluginUIType,
            this.colPluginPulldownGroup});
            this.dgvPlugins.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPlugins.Location = new System.Drawing.Point(0, 30);
            this.dgvPlugins.MultiSelect = false;
            this.dgvPlugins.Name = "dgvPlugins";
            this.dgvPlugins.ReadOnly = true;
            this.dgvPlugins.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvPlugins.Size = new System.Drawing.Size(623, 385);
            this.dgvPlugins.TabIndex = 1;
            // 
            // colPluginName
            // 
            this.colPluginName.DataPropertyName = "Name";
            this.colPluginName.HeaderText = "ID";
            this.colPluginName.Name = "colPluginName";
            this.colPluginName.ReadOnly = true;
            // 
            // colPluginDisplayName
            // 
            this.colPluginDisplayName.DataPropertyName = "DisplayName";
            this.colPluginDisplayName.HeaderText = "Отображаемое имя";
            this.colPluginDisplayName.Name = "colPluginDisplayName";
            this.colPluginDisplayName.ReadOnly = true;
            this.colPluginDisplayName.Width = 150;
            // 
            // colPluginUIType
            // 
            this.colPluginUIType.DataPropertyName = "UIType";
            this.colPluginUIType.HeaderText = "Тип UI";
            this.colPluginUIType.Name = "colPluginUIType";
            this.colPluginUIType.ReadOnly = true;
            // 
            // colPluginPulldownGroup
            // 
            this.colPluginPulldownGroup.DataPropertyName = "PulldownGroupName";
            this.colPluginPulldownGroup.HeaderText = "Группа Pulldown";
            this.colPluginPulldownGroup.Name = "colPluginPulldownGroup";
            this.colPluginPulldownGroup.ReadOnly = true;
            this.colPluginPulldownGroup.Width = 120;
            // 
            // panelPluginActions
            // 
            this.panelPluginActions.Controls.Add(this.btnTogglePluginEnable);
            this.panelPluginActions.Controls.Add(this.btnAssignPulldownGroup);
            this.panelPluginActions.Controls.Add(this.cmbPulldownGroups);
            this.panelPluginActions.Controls.Add(this.btnDuplicate);
            this.panelPluginActions.Controls.Add(this.btnRemovePlugin);
            this.panelPluginActions.Controls.Add(this.btnAddPlugin);
            this.panelPluginActions.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelPluginActions.Location = new System.Drawing.Point(0, 0);
            this.panelPluginActions.Name = "panelPluginActions";
            this.panelPluginActions.Size = new System.Drawing.Size(623, 30);
            this.panelPluginActions.TabIndex = 0;
            // 
            // btnTogglePluginEnable
            // 
            this.btnTogglePluginEnable.Enabled = false;
            this.btnTogglePluginEnable.Location = new System.Drawing.Point(285, 2);
            this.btnTogglePluginEnable.Name = "btnTogglePluginEnable";
            this.btnTogglePluginEnable.Size = new System.Drawing.Size(92, 23);
            this.btnTogglePluginEnable.TabIndex = 4;
            this.btnTogglePluginEnable.Text = "Вкл/Выкл";
            this.btnTogglePluginEnable.UseVisualStyleBackColor = true;
            this.btnTogglePluginEnable.Click += new System.EventHandler(this.btnTogglePluginEnable_Click);
            // 
            // btnAssignPulldownGroup
            // 
            this.btnAssignPulldownGroup.Enabled = false;
            this.btnAssignPulldownGroup.Location = new System.Drawing.Point(535, 4);
            this.btnAssignPulldownGroup.Name = "btnAssignPulldownGroup";
            this.btnAssignPulldownGroup.Size = new System.Drawing.Size(85, 23);
            this.btnAssignPulldownGroup.TabIndex = 3;
            this.btnAssignPulldownGroup.Text = "Назначить";
            this.btnAssignPulldownGroup.UseVisualStyleBackColor = true;
            this.btnAssignPulldownGroup.Click += new System.EventHandler(this.btnAssignPulldownGroup_Click);
            // 
            // cmbPulldownGroups
            // 
            this.cmbPulldownGroups.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPulldownGroups.Enabled = false;
            this.cmbPulldownGroups.FormattingEnabled = true;
            this.cmbPulldownGroups.Location = new System.Drawing.Point(383, 4);
            this.cmbPulldownGroups.Name = "cmbPulldownGroups";
            this.cmbPulldownGroups.Size = new System.Drawing.Size(146, 21);
            this.cmbPulldownGroups.TabIndex = 2;
            // 
            // btnDuplicate
            // 
            this.btnDuplicate.Location = new System.Drawing.Point(165, 2);
            this.btnDuplicate.Name = "btnDuplicate";
            this.btnDuplicate.Size = new System.Drawing.Size(101, 23);
            this.btnDuplicate.TabIndex = 4;
            this.btnDuplicate.Text = "Дублировать";
            this.btnDuplicate.UseVisualStyleBackColor = true;
            this.btnDuplicate.Click += new System.EventHandler(this.btnDuplicate_Click);
            // 
            // btnRemovePlugin
            // 
            this.btnRemovePlugin.Location = new System.Drawing.Point(84, 3);
            this.btnRemovePlugin.Name = "btnRemovePlugin";
            this.btnRemovePlugin.Size = new System.Drawing.Size(75, 23);
            this.btnRemovePlugin.TabIndex = 1;
            this.btnRemovePlugin.Text = "Удалить";
            this.btnRemovePlugin.UseVisualStyleBackColor = true;
            this.btnRemovePlugin.Click += new System.EventHandler(this.btnRemovePlugin_Click);
            // 
            // btnAddPlugin
            // 
            this.btnAddPlugin.Location = new System.Drawing.Point(3, 3);
            this.btnAddPlugin.Name = "btnAddPlugin";
            this.btnAddPlugin.Size = new System.Drawing.Size(75, 23);
            this.btnAddPlugin.TabIndex = 0;
            this.btnAddPlugin.Text = "Добавить";
            this.btnAddPlugin.UseVisualStyleBackColor = true;
            this.btnAddPlugin.Click += new System.EventHandler(this.btnAddPlugin_Click);
            // 
            // groupBoxSubCommands
            // 
            this.groupBoxSubCommands.Controls.Add(this.dgvSubCommands);
            this.groupBoxSubCommands.Controls.Add(this.panelSubCommandButtons);
            this.groupBoxSubCommands.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSubCommands.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSubCommands.Name = "groupBoxSubCommands";
            this.groupBoxSubCommands.Size = new System.Drawing.Size(623, 445);
            this.groupBoxSubCommands.TabIndex = 0;
            this.groupBoxSubCommands.TabStop = false;
            this.groupBoxSubCommands.Text = "Подкоманды (для SplitButton)";
            // 
            // dgvSubCommands
            // 
            this.dgvSubCommands.AllowUserToAddRows = false;
            this.dgvSubCommands.AllowUserToDeleteRows = false;
            this.dgvSubCommands.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSubCommands.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSubCmdName,
            this.colSubCmdDisplayName});
            this.dgvSubCommands.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvSubCommands.Enabled = false;
            this.dgvSubCommands.Location = new System.Drawing.Point(3, 46);
            this.dgvSubCommands.MultiSelect = false;
            this.dgvSubCommands.Name = "dgvSubCommands";
            this.dgvSubCommands.ReadOnly = true;
            this.dgvSubCommands.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvSubCommands.Size = new System.Drawing.Size(617, 396);
            this.dgvSubCommands.TabIndex = 1;
            // 
            // colSubCmdName
            // 
            this.colSubCmdName.DataPropertyName = "Name";
            this.colSubCmdName.HeaderText = "ID";
            this.colSubCmdName.Name = "colSubCmdName";
            this.colSubCmdName.ReadOnly = true;
            // 
            // colSubCmdDisplayName
            // 
            this.colSubCmdDisplayName.DataPropertyName = "DisplayName";
            this.colSubCmdDisplayName.HeaderText = "Отображаемое имя";
            this.colSubCmdDisplayName.Name = "colSubCmdDisplayName";
            this.colSubCmdDisplayName.ReadOnly = true;
            this.colSubCmdDisplayName.Width = 200;
            // 
            // panelSubCommandButtons
            // 
            this.panelSubCommandButtons.Controls.Add(this.btnRemoveSubCommand);
            this.panelSubCommandButtons.Controls.Add(this.btnAddSubCommand);
            this.panelSubCommandButtons.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelSubCommandButtons.Location = new System.Drawing.Point(3, 16);
            this.panelSubCommandButtons.Name = "panelSubCommandButtons";
            this.panelSubCommandButtons.Size = new System.Drawing.Size(617, 30);
            this.panelSubCommandButtons.TabIndex = 0;
            // 
            // btnRemoveSubCommand
            // 
            this.btnRemoveSubCommand.Enabled = false;
            this.btnRemoveSubCommand.Location = new System.Drawing.Point(84, 3);
            this.btnRemoveSubCommand.Name = "btnRemoveSubCommand";
            this.btnRemoveSubCommand.Size = new System.Drawing.Size(75, 23);
            this.btnRemoveSubCommand.TabIndex = 1;
            this.btnRemoveSubCommand.Text = "Удалить";
            this.btnRemoveSubCommand.UseVisualStyleBackColor = true;
            this.btnRemoveSubCommand.Click += new System.EventHandler(this.btnRemoveSubCommand_Click);
            // 
            // btnAddSubCommand
            // 
            this.btnAddSubCommand.Enabled = false;
            this.btnAddSubCommand.Location = new System.Drawing.Point(3, 3);
            this.btnAddSubCommand.Name = "btnAddSubCommand";
            this.btnAddSubCommand.Size = new System.Drawing.Size(75, 23);
            this.btnAddSubCommand.TabIndex = 0;
            this.btnAddSubCommand.Text = "Добавить";
            this.btnAddSubCommand.UseVisualStyleBackColor = true;
            this.btnAddSubCommand.Click += new System.EventHandler(this.btnAddSubCommand_Click);
            // 
            // btnScanPlugins
            // 
            this.btnScanPlugins.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnScanPlugins.Location = new System.Drawing.Point(372, 112);
            this.btnScanPlugins.Name = "btnScanPlugins";
            this.btnScanPlugins.Size = new System.Drawing.Size(139, 23);
            this.btnScanPlugins.TabIndex = 3;
            this.btnScanPlugins.Text = "Сканировать...";
            this.btnScanPlugins.UseVisualStyleBackColor = true;
            this.btnScanPlugins.Click += new System.EventHandler(this.btnScanPlugins_Click);
            // 
            // btnOpenFile
            // 
            this.btnOpenFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenFile.Location = new System.Drawing.Point(372, 83);
            this.btnOpenFile.Name = "btnOpenFile";
            this.btnOpenFile.Size = new System.Drawing.Size(140, 23);
            this.btnOpenFile.TabIndex = 2;
            this.btnOpenFile.Text = "Открыть файл...";
            this.btnOpenFile.UseVisualStyleBackColor = true;
            this.btnOpenFile.Click += new System.EventHandler(this.btnOpenFile_Click);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Location = new System.Drawing.Point(376, 237);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(136, 40);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Закрыть редактирование";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(376, 187);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(136, 44);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "Сохранить схему";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // splitContainerProperties
            // 
            this.splitContainerProperties.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerProperties.Location = new System.Drawing.Point(0, 0);
            this.splitContainerProperties.Name = "splitContainerProperties";
            this.splitContainerProperties.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerProperties.Panel1
            // 
            this.splitContainerProperties.Panel1.Controls.Add(this.propertyGridMain);
            // 
            // splitContainerProperties.Panel2
            // 
            this.splitContainerProperties.Panel2.Controls.Add(this.btnToggleValidation);
            this.splitContainerProperties.Panel2.Controls.Add(this.btnOpenFile);
            this.splitContainerProperties.Panel2.Controls.Add(this.btnScanPlugins);
            this.splitContainerProperties.Panel2.Controls.Add(this.btnClose);
            this.splitContainerProperties.Panel2.Controls.Add(this.btnSave);
            this.splitContainerProperties.Panel2.Controls.Add(this.propertyGridSubCommands);
            this.splitContainerProperties.Size = new System.Drawing.Size(515, 896);
            this.splitContainerProperties.SplitterDistance = 600;
            this.splitContainerProperties.TabIndex = 3;
            // 
            // propertyGridMain
            // 
            this.propertyGridMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGridMain.HelpForeColor = System.Drawing.SystemColors.ControlDark;
            this.propertyGridMain.Location = new System.Drawing.Point(0, 0);
            this.propertyGridMain.Name = "propertyGridMain";
            this.propertyGridMain.Size = new System.Drawing.Size(515, 600);
            this.propertyGridMain.TabIndex = 0;
            // 
            // btnToggleValidation
            // 
            this.btnToggleValidation.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnToggleValidation.BackColor = System.Drawing.SystemColors.Control;
            this.btnToggleValidation.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleValidation.Location = new System.Drawing.Point(376, 36);
            this.btnToggleValidation.Name = "btnToggleValidation";
            this.btnToggleValidation.Size = new System.Drawing.Size(136, 23);
            this.btnToggleValidation.TabIndex = 1;
            this.btnToggleValidation.Text = "Показать ошибки";
            this.btnToggleValidation.UseVisualStyleBackColor = false;
            this.btnToggleValidation.Click += new System.EventHandler(this.btnToggleValidation_Click);
            // 
            // propertyGridSubCommands
            // 
            this.propertyGridSubCommands.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyGridSubCommands.HelpForeColor = System.Drawing.SystemColors.ControlDark;
            this.propertyGridSubCommands.Location = new System.Drawing.Point(0, 3);
            this.propertyGridSubCommands.Name = "propertyGridSubCommands";
            this.propertyGridSubCommands.Size = new System.Drawing.Size(370, 279);
            this.propertyGridSubCommands.TabIndex = 1;
            this.propertyGridSubCommands.Visible = false;
            // 
            // panelValidation
            // 
            this.panelValidation.BackColor = System.Drawing.Color.LightYellow;
            this.panelValidation.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelValidation.Controls.Add(this.lstValidationErrors);
            this.panelValidation.Controls.Add(this.lblValidationTitle);
            this.panelValidation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelValidation.Location = new System.Drawing.Point(0, 0);
            this.panelValidation.Name = "panelValidation";
            this.panelValidation.Size = new System.Drawing.Size(77, 896);
            this.panelValidation.TabIndex = 4;
            this.panelValidation.Visible = false;
            // 
            // lstValidationErrors
            // 
            this.lstValidationErrors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstValidationErrors.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.lstValidationErrors.FormattingEnabled = true;
            this.lstValidationErrors.HorizontalScrollbar = true;
            this.lstValidationErrors.Location = new System.Drawing.Point(0, 23);
            this.lstValidationErrors.Name = "lstValidationErrors";
            this.lstValidationErrors.Size = new System.Drawing.Size(75, 871);
            this.lstValidationErrors.TabIndex = 2;
            this.lstValidationErrors.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.lstValidationErrors_DrawItem);
            // 
            // lblValidationTitle
            // 
            this.lblValidationTitle.AutoSize = true;
            this.lblValidationTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblValidationTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.lblValidationTitle.Location = new System.Drawing.Point(0, 0);
            this.lblValidationTitle.Name = "lblValidationTitle";
            this.lblValidationTitle.Padding = new System.Windows.Forms.Padding(5);
            this.lblValidationTitle.Size = new System.Drawing.Size(134, 23);
            this.lblValidationTitle.TabIndex = 0;
            this.lblValidationTitle.Text = "Ошибки валидации:";
            // 
            // splitter1
            // 
            this.splitter1.Location = new System.Drawing.Point(637, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(3, 896);
            this.splitter1.TabIndex = 5;
            this.splitter1.TabStop = false;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(639, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainerProperties);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.panelValidation);
            this.splitContainer1.Size = new System.Drawing.Size(596, 896);
            this.splitContainer1.SplitterDistance = 515;
            this.splitContainer1.TabIndex = 6;
            // 
            // SchemaEditorForm
            // 
            this.AcceptButton = this.btnSave;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(1233, 896);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.tabControlMain);
            this.Controls.Add(this.splitContainer1);
            this.MinimumSize = new System.Drawing.Size(900, 500);
            this.Name = "SchemaEditorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Редактор схемы MagicEntry";
            this.Load += new System.EventHandler(this.SchemaEditorForm_Load);
            this.tabControlMain.ResumeLayout(false);
            this.tabPagePulldowns.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPulldownDefinitions)).EndInit();
            this.panelPulldownButtons.ResumeLayout(false);
            this.tabPagePlugins.ResumeLayout(false);
            this.splitContainerPlugins.Panel1.ResumeLayout(false);
            this.splitContainerPlugins.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerPlugins)).EndInit();
            this.splitContainerPlugins.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPlugins)).EndInit();
            this.panelPluginActions.ResumeLayout(false);
            this.groupBoxSubCommands.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSubCommands)).EndInit();
            this.panelSubCommandButtons.ResumeLayout(false);
            this.splitContainerProperties.Panel1.ResumeLayout(false);
            this.splitContainerProperties.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerProperties)).EndInit();
            this.splitContainerProperties.ResumeLayout(false);
            this.panelValidation.ResumeLayout(false);
            this.panelValidation.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPagePulldowns;
        private System.Windows.Forms.DataGridView dgvPulldownDefinitions;
        private System.Windows.Forms.Panel panelPulldownButtons;
        private System.Windows.Forms.Button btnRemovePulldown;
        private System.Windows.Forms.Button btnAddPulldown;
        private System.Windows.Forms.TabPage tabPagePlugins;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.SplitContainer splitContainerPlugins;
        private System.Windows.Forms.DataGridView dgvPlugins;
        private System.Windows.Forms.Panel panelPluginActions;
        private System.Windows.Forms.Button btnRemovePlugin;
        private System.Windows.Forms.Button btnAddPlugin;
        private System.Windows.Forms.GroupBox groupBoxSubCommands;
        private System.Windows.Forms.DataGridView dgvSubCommands;
        private System.Windows.Forms.Panel panelSubCommandButtons;
        private System.Windows.Forms.Button btnRemoveSubCommand;
        private System.Windows.Forms.Button btnAddSubCommand;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPulldownName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPulldownDisplayName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPulldownTab;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPulldownPanel;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colPulldownEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPluginName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPluginDisplayName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPluginUIType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPluginPulldownGroup;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSubCmdName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSubCmdDisplayName;
        private System.Windows.Forms.Button btnOpenFile;
        private System.Windows.Forms.ComboBox cmbPulldownGroups;
        private System.Windows.Forms.Button btnAssignPulldownGroup;
        private System.Windows.Forms.Button btnTogglePulldownEnable;
        private System.Windows.Forms.Button btnTogglePluginEnable;
        private System.Windows.Forms.SplitContainer splitContainerProperties;
        private System.Windows.Forms.PropertyGrid propertyGridMain;
        private System.Windows.Forms.PropertyGrid propertyGridSubCommands;
        private System.Windows.Forms.Button btnScanPlugins;
        private System.Windows.Forms.Button btnDuplicate;
        private System.Windows.Forms.Panel panelValidation;
        private System.Windows.Forms.ListBox lstValidationErrors;
        private System.Windows.Forms.Label lblValidationTitle;
        private System.Windows.Forms.Button btnToggleValidation;

        private void SchemaEditorForm_Load(object sender, EventArgs e)
        {

        }
    }

    #region Вспомогательные классы

    // Обертка для элемента ошибки валидации с поддержкой отображения
    public class ValidationErrorItem
    {
        public ValidationError Error { get; }

        public ValidationErrorItem(ValidationError error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public override string ToString()
        {
            var severityText = Error.Severity == ValidationSeverity.Error ? "ОШИБКА" : "ПРЕДУПРЕЖДЕНИЕ";
            return $"[{severityText}] {Error.Message}";
        }
    }

    #endregion
}