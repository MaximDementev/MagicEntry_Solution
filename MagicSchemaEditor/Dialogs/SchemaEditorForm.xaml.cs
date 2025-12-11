using MagicEntry.Core.Models;
using MagicEntry.SchemaEditor.Dialogs;
using MagicEntry.SchemaEditor.Services;
using MagicEntry.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using ValidationError = MagicEntry.SchemaEditor.Services.ValidationError;

namespace MagicEntry.SchemaEditor
{
    public partial class SchemaEditorForm : Window, IDisposable
    {
        #region Поля

        private string _schemaFilePath;
        private PluginConfiguration _currentConfiguration;
        private ObservableCollection<PulldownButtonDefinitionInfo> _pulldownDefinitions;
        private ObservableCollection<PluginInfo> _plugins;
        private ObservableCollection<SubCommandInfo> _subCommands;

        private readonly IConfigurationValidator _validator;
        private readonly IPluginScanner _scanner;
        private DispatcherTimer _validationTimer;
        private List<ValidationError> _currentErrors;
        private string _basePath;

        private bool _isDirty = false;
        private bool _isValidationPanelVisible = false;

        #endregion

        #region Конструктор

        public SchemaEditorForm(string schemaFilePath)
        {
            _schemaFilePath = schemaFilePath ?? throw new ArgumentNullException(nameof(schemaFilePath));
            _validator = new ConfigurationValidator();
            _scanner = new PluginScanner();
            _currentErrors = new List<ValidationError>();
            _basePath = Path.GetDirectoryName(schemaFilePath);

            InitializeComponent();

            // Настройка валидации
            _validationTimer = new DispatcherTimer();
            _validationTimer.Interval = TimeSpan.FromSeconds(1);
            _validationTimer.Tick += ValidationTimer_Tick;

            // Загрузка конфигурации
            LoadConfigurationFromFile(_schemaFilePath);
            UpdateFormTitle();

            // Подписка на события
            this.Closing += SchemaEditorForm_Closing;
        }

        #endregion

        #region Методы загрузки и сохранения

        private void UpdateFormTitle()
        {
            this.Title = $"Редактор схемы MagicEntry - [{Path.GetFileName(_schemaFilePath)}]";
        }

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
                MessageBox.Show($"Ошибка загрузки файла схемы '{filePath}': {ex.Message}",
                    "Ошибка Загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
                _currentConfiguration = new PluginConfiguration();
                _isDirty = false;
            }
            SetupDataBindings();
            TriggerValidation();
        }

        private void SaveConfiguration()
        {
            try
            {
                _currentConfiguration.PulldownButtonDefinitions = _pulldownDefinitions.ToList();
                _currentConfiguration.Plugins = _plugins.ToList();

                var serializer = new XmlSerializer(typeof(PluginConfiguration));
                using (var writer = new StreamWriter(_schemaFilePath, false, System.Text.Encoding.UTF8))
                {
                    var ns = new XmlSerializerNamespaces();
                    ns.Add("", "");
                    serializer.Serialize(writer, _currentConfiguration, ns);
                }
                MessageBox.Show("Схема успешно сохранена.", "Сохранение",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _isDirty = false;

                UpdatePulldownGroupComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения файла схемы: {ex.Message}",
                    "Ошибка Сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Настройка привязки данных

        private void SetupDataBindings()
        {
            // Отписка от событий
            if (dgvPulldownDefinitions != null)
                dgvPulldownDefinitions.SelectionChanged -= DgvPulldownDefinitions_SelectionChanged;
            if (dgvPlugins != null)
                dgvPlugins.SelectionChanged -= DgvPlugins_SelectionChanged;
            if (dgvSubCommands != null)
                dgvSubCommands.SelectionChanged -= DgvSubCommands_SelectionChanged;

            _pulldownDefinitions = new ObservableCollection<PulldownButtonDefinitionInfo>(
                _currentConfiguration.PulldownButtonDefinitions ?? new List<PulldownButtonDefinitionInfo>());
            dgvPulldownDefinitions.ItemsSource = _pulldownDefinitions;
            dgvPulldownDefinitions.SelectionChanged += DgvPulldownDefinitions_SelectionChanged;

            _plugins = new ObservableCollection<PluginInfo>(
                _currentConfiguration.Plugins ?? new List<PluginInfo>());
            dgvPlugins.ItemsSource = _plugins;
            dgvPlugins.SelectionChanged += DgvPlugins_SelectionChanged;

            dgvSubCommands.SelectionChanged += DgvSubCommands_SelectionChanged;

            UpdatePulldownGroupComboBox();
            ClearSubCommandsDisplay();

            btnTogglePulldownEnable.IsEnabled = false;
            btnTogglePluginEnable.IsEnabled = false;
        }

        private void DgvPulldownDefinitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvPulldownDefinitions.SelectedItem is PulldownButtonDefinitionInfo selectedPulldown)
            {
                propertyGridMain.Children.Clear();
                CreatePropertyGrid(selectedPulldown, propertyGridMain);
                btnTogglePulldownEnable.IsEnabled = true;
                btnTogglePulldownEnable.Content = selectedPulldown.Enabled ? "Выключить" : "Включить";
            }
            else
            {
                propertyGridMain.Children.Clear();
                btnTogglePulldownEnable.IsEnabled = false;
                btnTogglePulldownEnable.Content = "Вкл/Выкл";
            }
            ClearSubCommandsDisplay();
        }

        private void DgvPlugins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvPlugins.SelectedItem is PluginInfo selectedPlugin)
            {
                propertyGridPlugins.Children.Clear();
                CreatePropertyGrid(selectedPlugin, propertyGridPlugins);
                DisplaySubCommands(selectedPlugin);
                UpdatePulldownGroupAssignmentControls(selectedPlugin);
                btnTogglePluginEnable.IsEnabled = true;
                btnTogglePluginEnable.Content = selectedPlugin.Enabled ? "Выключить" : "Включить";
            }
            else
            {
                propertyGridPlugins.Children.Clear();
                ClearSubCommandsDisplay();
                UpdatePulldownGroupAssignmentControls(null);
                btnTogglePluginEnable.IsEnabled = false;
                btnTogglePluginEnable.Content = "Вкл/Выкл";
            }
        }

        private void DgvSubCommands_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            propertyGridSubCommands.Children.Clear();
            if (dgvSubCommands.SelectedItem is SubCommandInfo selectedSubCommand)
            {
                CreatePropertyGrid(selectedSubCommand, propertyGridSubCommands);
            }
        }

        private void DisplaySubCommands(PluginInfo plugin)
        {
            if (plugin != null && plugin.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                if (plugin.SubCommands == null) plugin.SubCommands = new List<SubCommandInfo>();
                _subCommands = new ObservableCollection<SubCommandInfo>(plugin.SubCommands);
                dgvSubCommands.ItemsSource = _subCommands;
                dgvSubCommands.IsEnabled = true;
                btnAddSubCommand.IsEnabled = true;
                btnRemoveSubCommand.IsEnabled = true;
            }
            else
            {
                ClearSubCommandsDisplay();
            }
        }

        private void ClearSubCommandsDisplay()
        {
            dgvSubCommands.ItemsSource = null;
            dgvSubCommands.IsEnabled = false;
            btnAddSubCommand.IsEnabled = false;
            btnRemoveSubCommand.IsEnabled = false;
            propertyGridSubCommands.Children.Clear();
        }

        private void UpdatePulldownGroupComboBox()
        {
            string previouslySelected = cmbPulldownGroups.SelectedItem as string;
            cmbPulldownGroups.Items.Clear();
            cmbPulldownGroups.Items.Add("");

            if (_pulldownDefinitions != null)
            {
                foreach (var pbd in _pulldownDefinitions.Where(p => p.Enabled && !string.IsNullOrEmpty(p.Name)))
                {
                    cmbPulldownGroups.Items.Add(pbd.Name);
                }
            }

            if (previouslySelected != null && cmbPulldownGroups.Items.Contains(previouslySelected))
                cmbPulldownGroups.SelectedItem = previouslySelected;
            else if (dgvPlugins.SelectedItem is PluginInfo currentPlugin)
            {
                string groupToSelect = currentPlugin.PulldownGroupName ?? "";
                if (cmbPulldownGroups.Items.Contains(groupToSelect))
                    cmbPulldownGroups.SelectedItem = groupToSelect;
            }
            else if (cmbPulldownGroups.Items.Contains(""))
                cmbPulldownGroups.SelectedItem = "";
            else if (cmbPulldownGroups.Items.Count > 0)
                cmbPulldownGroups.SelectedIndex = 0;
        }

        private void UpdatePulldownGroupAssignmentControls(PluginInfo selectedPlugin)
        {
            if (selectedPlugin != null)
            {
                cmbPulldownGroups.IsEnabled = true;
                btnAssignPulldownGroup.IsEnabled = true;
                cmbPulldownGroups.SelectedItem = selectedPlugin.PulldownGroupName ?? "";
            }
            else
            {
                cmbPulldownGroups.IsEnabled = false;
                btnAssignPulldownGroup.IsEnabled = false;
                cmbPulldownGroups.SelectedItem = null;
            }
        }

        private void CreatePropertyGrid(object obj, StackPanel container)
        {
            if (obj == null) return;

            var properties = TypeDescriptor.GetProperties(obj);
            foreach (PropertyDescriptor prop in properties)
            {
                if (prop.IsBrowsable)
                {
                    var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

                    var label = new TextBlock { Text = prop.DisplayName ?? prop.Name, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(label, 0);
                    grid.Children.Add(label);

                    var value = prop.GetValue(obj);
                    if (prop.PropertyType == typeof(bool))
                    {
                        var checkbox = new CheckBox { IsChecked = (bool?)value };
                        checkbox.Checked += (s, e) => { prop.SetValue(obj, true); _isDirty = true; TriggerValidation(); };
                        checkbox.Unchecked += (s, e) => { prop.SetValue(obj, false); _isDirty = true; TriggerValidation(); };
                        Grid.SetColumn(checkbox, 1);
                        grid.Children.Add(checkbox);
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        var combo = new ComboBox { ItemsSource = Enum.GetValues(prop.PropertyType), SelectedItem = value };
                        combo.SelectionChanged += (s, e) => { prop.SetValue(obj, combo.SelectedItem); _isDirty = true; TriggerValidation(); };
                        Grid.SetColumn(combo, 1);
                        grid.Children.Add(combo);
                    }
                    else
                    {
                        var textbox = new TextBox { Text = value?.ToString() ?? "", IsReadOnly = prop.IsReadOnly };
                        textbox.TextChanged += (s, e) =>
                        {
                            try
                            {
                                var converter = TypeDescriptor.GetConverter(prop.PropertyType);
                                if (converter.CanConvertFrom(typeof(string)))
                                    prop.SetValue(obj, converter.ConvertFromString(textbox.Text));
                                _isDirty = true;
                                TriggerValidation();
                            }
                            catch { }
                        };
                        Grid.SetColumn(textbox, 1);
                        grid.Children.Add(textbox);
                    }

                    container.Children.Add(grid);
                }
            }
        }

        #endregion

        #region Валидация

        private void PerformValidation()
        {
            if (_currentConfiguration == null) return;

            _currentErrors = _validator.ValidateConfiguration(_currentConfiguration, _basePath);
            UpdateValidationDisplay();
            UpdateValidationButtonState();
        }

        private void TriggerValidation()
        {
            _validationTimer.Stop();
            _validationTimer.Start();
        }

        private void ValidationTimer_Tick(object sender, EventArgs e)
        {
            _validationTimer.Stop();
            PerformValidation();
        }

        private void UpdateValidationButtonState()
        {
            bool hasErrors = _currentErrors.Any(e => e.Severity == ValidationSeverity.Error);
            btnToggleValidation.Background = hasErrors ?
                System.Windows.Media.Brushes.Salmon : System.Windows.Media.Brushes.Transparent;
        }

        private void UpdateValidationDisplay()
        {
            if (!_isValidationPanelVisible) return;

            lstValidationErrors.Items.Clear();

            var errorGroups = _currentErrors
                .GroupBy(e => GetGroupKey(e))
                .OrderBy(g => g.Key);

            foreach (var group in errorGroups)
            {
                lstValidationErrors.Items.Add($"--- {group.Key} ---");
                foreach (var error in group.OrderBy(e => e.Severity))
                {
                    var severityText = error.Severity == ValidationSeverity.Error ? "ОШИБКА" : "ПРЕДУПРЕЖДЕНИЕ";
                    lstValidationErrors.Items.Add($"[{severityText}] {error.Message}");
                }
                lstValidationErrors.Items.Add(string.Empty);
            }
        }

        private string GetGroupKey(ValidationError error)
        {
            var contextParts = error.Context.Split('.');
            if (contextParts.Length >= 2)
            {
                string objectType = contextParts[0];
                string objectName = contextParts[1];

                if (objectType == "Plugin")
                {
                    var plugin = _currentConfiguration.Plugins?.FirstOrDefault(p => p.Name == objectName);
                    string displayName = plugin?.DisplayName ?? objectName;
                    return $"Плагин: {displayName} ({objectName})";
                }
                else if (objectType == "Pulldown")
                {
                    var pulldown = _currentConfiguration.PulldownButtonDefinitions?.FirstOrDefault(p => p.Name == objectName);
                    string displayName = pulldown?.DisplayName ?? objectName;
                    return $"PulldownButton: {displayName} ({objectName})";
                }
            }

            return error.Context.Split('.').FirstOrDefault() ?? "Общие";
        }

        #endregion

        #region Обработчики событий

        private void SchemaEditorForm_Closing(object sender, CancelEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("Есть несохраненные изменения. Сохранить их перед закрытием?",
                    "Несохраненные изменения", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    SaveConfiguration();
                    if (_isDirty) e.Cancel = true;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnAddPulldown_Click(object sender, RoutedEventArgs e)
        {
            var newPulldown = new PulldownButtonDefinitionInfo
            {
                Name = "NewPulldown",
                DisplayName = "Новый Pulldown",
                RibbonTab = "MagicEntry",
                RibbonPanel = "Панель",
                Enabled = true
            };
            _pulldownDefinitions.Add(newPulldown);
            _isDirty = true;
            dgvPulldownDefinitions.SelectedItem = newPulldown;
            TriggerValidation();
        }

        private void BtnRemovePulldown_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPulldownDefinitions.SelectedItem is PulldownButtonDefinitionInfo selected)
            {
                if (MessageBox.Show($"Удалить определение Pulldown '{selected.DisplayName}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _pulldownDefinitions.Remove(selected);
                    _isDirty = true;
                    TriggerValidation();
                    UpdatePulldownGroupComboBox();
                }
            }
        }

        private void BtnTogglePulldownEnable_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPulldownDefinitions.SelectedItem is PulldownButtonDefinitionInfo selectedPulldown)
            {
                selectedPulldown.Enabled = !selectedPulldown.Enabled;
                _isDirty = true;
                btnTogglePulldownEnable.Content = selectedPulldown.Enabled ? "Выключить" : "Включить";
                dgvPulldownDefinitions.Items.Refresh();
                UpdatePulldownGroupComboBox();
                TriggerValidation();
            }
        }

        private void BtnAddPlugin_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Выберите тип плагина",
                Width = 250,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(10) };
            var rbPush = new RadioButton { Content = "PushButton", IsChecked = true, Margin = new Thickness(0, 5, 0, 5) };
            var rbSplit = new RadioButton { Content = "SplitButton", Margin = new Thickness(0, 5, 0, 5) };
            var btnOk = new Button { Content = "OK", Width = 100, Height = 30, Margin = new Thickness(0, 10, 0, 0), IsDefault = true };
            btnOk.Click += (s, args) => { dialog.DialogResult = true; dialog.Close(); };

            panel.Children.Add(rbPush);
            panel.Children.Add(rbSplit);
            panel.Children.Add(btnOk);
            dialog.Content = panel;

            if (dialog.ShowDialog() == true)
            {
                var newPlugin = new PluginInfo
                {
                    Name = "NewPlugin",
                    DisplayName = "Новый Плагин",
                    AssemblyPath = "Plugins\\NewPlugin\\NewPlugin.dll",
                    ClassName = "Namespace.NewPluginCommand",
                    RibbonTab = "MagicEntry",
                    RibbonPanel = "Панель",
                    UIType = rbSplit.IsChecked == true ? PluginInfo.ButtonUIType.SplitButton : PluginInfo.ButtonUIType.PushButton,
                    Enabled = true
                };
                if (newPlugin.UIType == PluginInfo.ButtonUIType.SplitButton)
                    newPlugin.SubCommands = new List<SubCommandInfo>();

                _plugins.Add(newPlugin);
                _isDirty = true;
                dgvPlugins.SelectedItem = newPlugin;
                TriggerValidation();
            }
        }

        private void BtnRemovePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPlugins.SelectedItem is PluginInfo selected)
            {
                if (MessageBox.Show($"Удалить плагин '{selected.DisplayName}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _plugins.Remove(selected);
                    _isDirty = true;
                    TriggerValidation();
                }
            }
        }

        private void BtnTogglePluginEnable_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPlugins.SelectedItem is PluginInfo selectedPlugin)
            {
                selectedPlugin.Enabled = !selectedPlugin.Enabled;
                _isDirty = true;
                btnTogglePluginEnable.Content = selectedPlugin.Enabled ? "Выключить" : "Включить";
                dgvPlugins.Items.Refresh();
                TriggerValidation();
            }
        }

        private void BtnAssignPulldownGroup_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPlugins.SelectedItem is PluginInfo selectedPlugin)
            {
                string selectedGroup = cmbPulldownGroups.SelectedItem as string ?? "";
                if (selectedPlugin.PulldownGroupName != selectedGroup)
                {
                    selectedPlugin.PulldownGroupName = selectedGroup;
                    _isDirty = true;
                    dgvPlugins.Items.Refresh();
                    TriggerValidation();
                }
            }
        }

        private void BtnAddSubCommand_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPlugins.SelectedItem is PluginInfo selectedPlugin &&
                selectedPlugin.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                var newSubCommand = new SubCommandInfo
                {
                    Name = "NewSubCmd",
                    DisplayName = "Новая Подкоманда",
                    ClassName = "Namespace.NewSubCommand"
                };
                if (selectedPlugin.SubCommands == null)
                    selectedPlugin.SubCommands = new List<SubCommandInfo>();

                selectedPlugin.SubCommands.Add(newSubCommand);
                _subCommands.Add(newSubCommand);
                _isDirty = true;
                dgvSubCommands.SelectedItem = newSubCommand;
                TriggerValidation();
            }
        }

        private void BtnRemoveSubCommand_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPlugins.SelectedItem is PluginInfo selectedPlugin &&
                dgvSubCommands.SelectedItem is SubCommandInfo selectedSubCommand)
            {
                if (MessageBox.Show($"Удалить подкоманду '{selectedSubCommand.DisplayName}' из плагина '{selectedPlugin.DisplayName}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    selectedPlugin.SubCommands?.Remove(selectedSubCommand);
                    _subCommands.Remove(selectedSubCommand);
                    _isDirty = true;
                    TriggerValidation();
                }
            }
        }

        private void BtnScanPlugins_Click(object sender, RoutedEventArgs e)
        {
            var scanDialog = new PluginScanDialog(_basePath);
            if (scanDialog.ShowDialog() == true)
            {
                foreach (var plugin in scanDialog.SelectedPlugins)
                {
                    _plugins.Add(plugin);
                }
                _isDirty = true;
                TriggerValidation();
            }
        }

        private void BtnToggleValidation_Click(object sender, RoutedEventArgs e)
        {
            _isValidationPanelVisible = !_isValidationPanelVisible;
            panelValidation.Visibility = _isValidationPanelVisible ? Visibility.Visible : Visibility.Collapsed;
            btnToggleValidation.Content = _isValidationPanelVisible ? "Скрыть ошибки" : "Показать ошибки";

            if (_isValidationPanelVisible)
                UpdateValidationDisplay();
        }

        #endregion

        public void Dispose()
        {
            if (_validationTimer != null)
            {
                _validationTimer.Stop();
                _validationTimer = null;
            }
        }
    }
}
