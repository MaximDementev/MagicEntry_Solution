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
using System.Windows.Input;
using System.Windows.Media;
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

        private readonly IConfigurationValidator _validator;
        private readonly IPluginScanner _scanner;
        private DispatcherTimer _validationTimer;
        private List<ValidationError> _currentErrors;
        private string _basePath;

        private bool _isDirty = false;
        private bool _isValidationPanelVisible = false;

        private PluginInfo _selectedPlugin = null;
        private SubCommandInfo _selectedSubCommand = null;
        private PulldownButtonDefinitionInfo _selectedPulldown = null;

        private Border _draggedPanel = null;
        private PluginInfo _draggedPlugin = null;

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

            this.PreviewKeyDown += SchemaEditorForm_PreviewKeyDown;

            _validationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _validationTimer.Tick += ValidationTimer_Tick;

            LoadSchema();
            PopulateFilterComboBox();
            RebuildPluginCards();
            RebuildPulldownCards();
            UpdateFormTitle();

            // Подписка на события
            this.Closing += SchemaEditorForm_Closing;
        }

        #endregion

        #region Горячие клавиши

        private void SchemaEditorForm_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S - Сохранить
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveConfiguration();
                e.Handled = true;
            }
            // Del - Удалить выбранный элемент
            else if (e.Key == Key.Delete)
            {
                DeleteSelectedItem();
                e.Handled = true;
            }
            // F2 - Редактировать DisplayName
            else if (e.Key == Key.F2)
            {
                EditDisplayName();
                e.Handled = true;
            }
        }

        private void DeleteSelectedItem()
        {
            if (mainTabControl.SelectedIndex == 0 && _selectedPlugin != null)
            {
                if (MessageBox.Show($"Удалить плагин '{_selectedPlugin.DisplayName}'?",
                    "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _plugins.Remove(_selectedPlugin);
                    _selectedPlugin = null;
                    _isDirty = true;
                    RebuildPluginCards();
                    TriggerValidation();
                }
            }
            else if (mainTabControl.SelectedIndex == 0 && _selectedSubCommand != null && _selectedPlugin != null)
            {
                if (MessageBox.Show($"Удалить подкоманду '{_selectedSubCommand.DisplayName}'?",
                    "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _selectedPlugin.SubCommands?.Remove(_selectedSubCommand);
                    _selectedSubCommand = null;
                    _isDirty = true;
                    //RebuildSubCommandCards();
                    TriggerValidation();
                }
            }
            else if (mainTabControl.SelectedIndex == 1 && _selectedPulldown != null)
            {
                if (MessageBox.Show($"Удалить PulldownButton '{_selectedPulldown.DisplayName}'?",
                    "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _pulldownDefinitions.Remove(_selectedPulldown);
                    _selectedPulldown = null;
                    _isDirty = true;
                    RebuildPulldownCards();
                    TriggerValidation();
                }
            }
        }

        private void EditDisplayName()
        {
            string currentName = null;
            string title = "Редактировать DisplayName";

            if (mainTabControl.SelectedIndex == 0 && _selectedPlugin != null)
            {
                currentName = _selectedPlugin.DisplayName;
            }
            else if (mainTabControl.SelectedIndex == 0 && _selectedSubCommand != null)
            {
                currentName = _selectedSubCommand.DisplayName;
            }
            else if (mainTabControl.SelectedIndex == 1 && _selectedPulldown != null)
            {
                currentName = _selectedPulldown.DisplayName;
            }

            if (currentName != null)
            {
                string newName = ShowInputDialog(title, "Новое отображаемое имя:", currentName);
                if (newName != null)
                {
                    if (mainTabControl.SelectedIndex == 0 && _selectedPlugin != null)
                    {
                        _selectedPlugin.DisplayName = newName;
                        RebuildPluginCards();
                    }
                    else if (mainTabControl.SelectedIndex == 0 && _selectedSubCommand != null)
                    {
                        _selectedSubCommand.DisplayName = newName;
                        RebuildSubCommandCards();
                    }
                    else if (mainTabControl.SelectedIndex == 1 && _selectedPulldown != null)
                    {
                        _selectedPulldown.DisplayName = newName;
                        RebuildPulldownCards();
                    }
                    _isDirty = true;
                    TriggerValidation();
                }
            }
        }

        private string ShowInputDialog(string title, string prompt, string defaultValue)
        {
            Window dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            Grid grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            TextBox textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button okButton = new Button { Content = "OK", Width = 80, Height = 25, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            Button cancelButton = new Button { Content = "Отмена", Width = 80, Height = 25, IsCancel = true };

            okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            dialog.Content = grid;
            textBox.Focus();
            textBox.SelectAll();

            return dialog.ShowDialog() == true ? textBox.Text : null;
        }

        #endregion

        #region Методы загрузки и сохранения

        private void UpdateFormTitle()
        {
            this.Title = $"Редактор схемы MagicEntry - [{Path.GetFileName(_schemaFilePath)}]";
        }

        private void LoadSchema()
        {
            try
            {
                var reader = new XmlConfigurationReader();
                _currentConfiguration = reader.ReadConfiguration(_schemaFilePath);

                // Initialize ObservableCollections from loaded configuration
                _plugins = _currentConfiguration.Plugins != null
                    ? new ObservableCollection<PluginInfo>(_currentConfiguration.Plugins)
                    : new ObservableCollection<PluginInfo>();

                _pulldownDefinitions = _currentConfiguration.PulldownButtonDefinitions != null
                    ? new ObservableCollection<PulldownButtonDefinitionInfo>(_currentConfiguration.PulldownButtonDefinitions)
                    : new ObservableCollection<PulldownButtonDefinitionInfo>();

                _isDirty = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла схемы '{_schemaFilePath}': {ex.Message}",
                    "Ошибка Загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
                _currentConfiguration = new PluginConfiguration();
                _plugins = new ObservableCollection<PluginInfo>();
                _pulldownDefinitions = new ObservableCollection<PulldownButtonDefinitionInfo>();
                _isDirty = false;
            }
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

                txtStatus.Text = "✓ Успешно сохранено";
                txtStatus.Foreground = Brushes.Green;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, e) => { txtStatus.Text = ""; timer.Stop(); };
                timer.Start();

                _isDirty = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения файла схемы: {ex.Message}",
                    "Ошибка Сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Настройка привязки данных

        private void PopulateFilterComboBox()
        {
            cmbFilterTab.Items.Clear();
            cmbFilterTab.Items.Add("(Все вкладки)");

            if (_plugins != null && _plugins.Any())
            {
                var tabs = _plugins.Select(p => p.RibbonTab).Distinct().OrderBy(t => t).ToList();
                foreach (var tab in tabs)
                {
                    if (!string.IsNullOrEmpty(tab))
                        cmbFilterTab.Items.Add(tab);
                }
            }

            cmbFilterTab.SelectedIndex = 0;
        }

        private void RebuildPluginCards()
        {
            pluginGroupsContainer.Children.Clear();

            var filteredPlugins = ApplyFilters(_plugins);

            // Группировка по RibbonTab и RibbonPanel
            var groups = filteredPlugins
                .GroupBy(p => new { p.RibbonTab, p.RibbonPanel })
                .OrderBy(g => g.Key.RibbonTab)
                .ThenBy(g => g.Key.RibbonPanel);

            foreach (var group in groups)
            {
                var panelBorder = new Border
                {
                    Style = (Style)FindResource("PanelGroupStyle"),
                    AllowDrop = true,
                    Tag = new { group.Key.RibbonTab, group.Key.RibbonPanel }
                };

                panelBorder.Background = GetTabColorBrush(group.Key.RibbonTab);

                panelBorder.MouseLeftButtonDown += PanelBorder_MouseLeftButtonDown;
                panelBorder.DragEnter += PanelBorder_DragEnter;
                panelBorder.DragLeave += PanelBorder_DragLeave;
                panelBorder.Drop += PanelBorder_Drop;

                var panelStack = new StackPanel { Margin = new Thickness(5) };

                // Заголовок панели
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var headerText = new TextBlock
                {
                    Text = $"Панель «{group.Key.RibbonPanel}» (Tab={group.Key.RibbonTab})",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(headerText, 0);
                headerGrid.Children.Add(headerText);

                var dragHint = new TextBlock
                {
                    Text = "☰",
                    FontSize = 16,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "Перетащите панель для изменения порядка"
                };
                Grid.SetColumn(dragHint, 1);
                headerGrid.Children.Add(dragHint);

                panelStack.Children.Add(headerGrid);

                // Контейнер для карточек плагинов
                var wrapPanel = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    AllowDrop = true
                };

                wrapPanel.Drop += (s, e) => PluginWrapPanel_Drop(s, e, group.Key.RibbonTab, group.Key.RibbonPanel);
                wrapPanel.DragEnter += (s, e) =>
                {
                    if (e.Data.GetDataPresent(typeof(PluginInfo)))
                    {
                        e.Effects = DragDropEffects.Move;
                    }
                };

                foreach (var plugin in group.OrderBy(p => p.DisplayOrder).ThenBy(p => p.DisplayName))
                {
                    var card = CreatePluginCard(plugin);
                    wrapPanel.Children.Add(card);
                }

                panelStack.Children.Add(wrapPanel);
                panelBorder.Child = panelStack;
                pluginGroupsContainer.Children.Add(panelBorder);
            }

            var addButton = new Button
            {
                Content = "+ Добавить новый плагин",
                Height = 40,
                Margin = new Thickness(10),
                FontWeight = FontWeights.Bold
            };
            addButton.Click += BtnAddPlugin_Click;
            pluginGroupsContainer.Children.Add(addButton);
        }

        private Brush GetTabColorBrush(string tabName)
        {
            if (string.IsNullOrEmpty(tabName))
                return Brushes.Transparent;

            // Генерируем хеш из имени вкладки
            int hash = tabName.GetHashCode();
            byte r = (byte)((hash & 0xFF0000) >> 16);
            byte g = (byte)((hash & 0x00FF00) >> 8);
            byte b = (byte)(hash & 0x0000FF);

            // Делаем цвет очень прозрачным (alpha = 15 из 255)
            return new SolidColorBrush(Color.FromArgb(15, r, g, b));
        }

        private void PanelBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && e.OriginalSource is TextBlock textBlock && textBlock.Text == "☰")
            {
                _draggedPanel = border;
                DragDrop.DoDragDrop(border, border.Tag, DragDropEffects.Move);
                _draggedPanel = null;
            }
        }

        private void PanelBorder_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border border && border != _draggedPanel && !e.Data.GetDataPresent(typeof(PluginInfo)))
            {
                border.Tag = "DragOver";
                border.Style = (Style)FindResource("PanelGroupStyle");
            }
        }

        private void PanelBorder_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                var tagData = border.Tag;
                if (tagData is string && (string)tagData == "DragOver")
                {
                    // Восстанавливаем исходный Tag
                    var groups = _plugins.GroupBy(p => new { p.RibbonTab, p.RibbonPanel });
                    foreach (var group in groups)
                    {
                        border.Tag = new { group.Key.RibbonTab, group.Key.RibbonPanel };
                        break;
                    }
                }
                border.Style = (Style)FindResource("PanelGroupStyle");
            }
        }

        private void PanelBorder_Drop(object sender, DragEventArgs e)
        {
            if (!(sender is Border targetBorder) || _draggedPanel == null || targetBorder == _draggedPanel)
                return;

            // Меняем местами панели в контейнере
            int sourceIndex = pluginGroupsContainer.Children.IndexOf(_draggedPanel);
            int targetIndex = pluginGroupsContainer.Children.IndexOf(targetBorder);

            if (sourceIndex >= 0 && targetIndex >= 0)
            {
                pluginGroupsContainer.Children.RemoveAt(sourceIndex);
                pluginGroupsContainer.Children.Insert(targetIndex, _draggedPanel);
                _isDirty = true;
            }

            // Восстанавливаем стиль без обращения к Tag
            targetBorder.Style = (Style)FindResource("PanelGroupStyle");
            _draggedPanel = null;
        }

        private void PluginWrapPanel_Drop(object sender, DragEventArgs e, string targetTab, string targetPanel)
        {
            if (e.Data.GetDataPresent(typeof(PluginInfo)) && sender is WrapPanel wrapPanel)
            {
                var plugin = e.Data.GetData(typeof(PluginInfo)) as PluginInfo;
                if (plugin != null)
                {
                    if (plugin.RibbonTab != targetTab || plugin.RibbonPanel != targetPanel)
                    {
                        plugin.RibbonTab = targetTab;
                        plugin.RibbonPanel = targetPanel;
                        _isDirty = true;
                        RebuildPluginCards();
                    }
                }
            }
        }


        private IEnumerable<PluginInfo> ApplyFilters(IEnumerable<PluginInfo> plugins)
        {
            var result = plugins.AsEnumerable();

            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string searchText = txtSearch.Text.ToLower();
                result = result.Where(p =>
                    p.Name?.ToLower().Contains(searchText) == true ||
                    p.DisplayName?.ToLower().Contains(searchText) == true ||
                    p.ClassName?.ToLower().Contains(searchText) == true);
            }

            // Фильтр по вкладке
            if (cmbFilterTab.SelectedIndex > 0)
            {
                string selectedTab = cmbFilterTab.SelectedItem as string;
                result = result.Where(p => p.RibbonTab == selectedTab);
            }

            // Фильтр по статусу
            if (chkShowDisabled.IsChecked == false)
            {
                result = result.Where(p => p.Enabled);
            }

            return result;
        }

        private Border CreatePluginCard(PluginInfo plugin)
        {
            var card = new Border
            {
                Width = 200,
                Height = 140,
                AllowDrop = true,
                Tag = plugin,
                BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Cursor = Cursors.Hand
            };

            card.Background = plugin.Enabled ?
                new SolidColorBrush(Color.FromRgb(232, 245, 233)) :
                Brushes.White;

            card.MouseLeftButtonDown += (s, e) =>
            {
                SelectPluginAndShowProperties(plugin);
                e.Handled = true;
            };
            card.DragEnter += PluginCard_DragEnter;
            card.Drop += PluginCard_Drop;
            card.MouseEnter += (s, e) =>
            {
                if (!plugin.Enabled)
                    card.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = plugin.Enabled ?
                    new SolidColorBrush(Color.FromRgb(232, 245, 233)) :
                    Brushes.White;
            };

            var stack = new StackPanel();

            // DisplayName
            var nameText = new TextBlock
            {
                Text = plugin.DisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(nameText);

            // Name (ID)
            var idText = new TextBlock
            {
                Text = $"ID: {plugin.Name}",
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(idText);

            // UIType
            var typeText = new TextBlock
            {
                Text = $"Тип: {plugin.UIType}",
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(typeText);

            // Enabled toggle
            var enabledCheck = new CheckBox
            {
                Content = "Включен",
                IsChecked = plugin.Enabled,
                Margin = new Thickness(0, 5, 0, 5)
            };
            enabledCheck.Checked += (s, e) =>
            {
                plugin.Enabled = true;
                _isDirty = true;
                TriggerValidation();
                RebuildPluginCards(); // Перестроение для обновления цвета
            };
            enabledCheck.Unchecked += (s, e) =>
            {
                plugin.Enabled = false;
                _isDirty = true;
                TriggerValidation();
                RebuildPluginCards(); // Перестроение для обновления цвета
            };
            stack.Children.Add(enabledCheck);

            // Кнопка редактирования
            var editButton = new Button
            {
                Content = "Редактировать",
                Height = 25,
                Margin = new Thickness(0, 5, 0, 0)
            };
            editButton.Click += (s, e) => SelectPluginAndShowProperties(plugin);
            stack.Children.Add(editButton);

            card.Child = stack;

            if (_selectedPlugin == plugin)
            {
                card.BorderBrush = Brushes.Blue;
                card.BorderThickness = new Thickness(3);
            }

            return card;
        }

        private void PluginCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card && card.Tag is PluginInfo plugin)
            {
                _draggedPlugin = plugin;
                DragDrop.DoDragDrop(card, plugin, DragDropEffects.Move);
                _draggedPlugin = null;
            }
        }

        private void PluginCard_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PluginInfo)))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void PluginCard_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PluginInfo)) && sender is Border targetCard && targetCard.Tag is PluginInfo targetPlugin)
            {
                var sourcePlugin = e.Data.GetData(typeof(PluginInfo)) as PluginInfo;
                if (sourcePlugin != null && sourcePlugin != targetPlugin)
                {
                    int sourceOrder = sourcePlugin.DisplayOrder;
                    sourcePlugin.DisplayOrder = targetPlugin.DisplayOrder;
                    targetPlugin.DisplayOrder = sourceOrder;

                    _isDirty = true;
                    RebuildPluginCards();
                }
            }
        }

        private void SelectPluginAndShowProperties(PluginInfo plugin)
        {
            _selectedPlugin = plugin;
            _selectedPulldown = null;
            _selectedSubCommand = null;

            CreatePropertyGrid(plugin, propertyGridPlugins);

            if (plugin != null && plugin.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                subCommandsSplitter.Visibility = Visibility.Visible;
                subCommandsPanel.Visibility = Visibility.Visible;
                subCommandsSplitterColumn.Width = new GridLength(5);
                subCommandsColumn.Width = new GridLength(300);
                RebuildSubCommandCards();
            }
            else
            {
                subCommandsSplitter.Visibility = Visibility.Collapsed;
                subCommandsPanel.Visibility = Visibility.Collapsed;
                subCommandsSplitterColumn.Width = new GridLength(0);
                subCommandsColumn.Width = new GridLength(0);
                subCommandsContainer.Children.Clear();
                propertyGridSubCommands.Children.Clear();
            }

            RebuildPluginCards(); // Rebuild to update selection highlighting
        }

        private void SelectPlugin(PluginInfo plugin)
        {
            SelectPluginAndShowProperties(plugin);
        }

        private void PluginCard_Click(object sender, RoutedEventArgs e)
        {
            var card = sender as Border;
            var plugin = card?.Tag as PluginInfo;

            // Replaced with SelectPluginAndShowProperties for unified logic
            SelectPluginAndShowProperties(plugin);

            // Original logic removed as it's now handled by SelectPluginAndShowProperties
            // if (plugin != null && plugin.UIType == PluginInfo.ButtonUIType.SplitButton)
            // {
            //     subCommandsSplitter.Visibility = Visibility.Visible;
            //     subCommandsPanel.Visibility = Visibility.Visible;
            //     subCommandsSplitterColumn.Width = new GridLength(5);
            //     subCommandsColumn.Width = new GridLength(300);
            //     RebuildSubCommandCards();
            // }
            // else
            // {
            //     subCommandsSplitter.Visibility = Visibility.Collapsed;
            //     subCommandsPanel.Visibility = Visibility.Collapsed;
            //     subCommandsSplitterColumn.Width = new GridLength(0);
            //     subCommandsColumn.Width = new GridLength(0);
            //     subCommandsContainer.Children.Clear(); // Clear subcommands if not split button
            //     propertyGridSubCommands.Children.Clear(); // Clear subcommands property grid
            // }
        }

        private void SelectSubCommand(SubCommandInfo subCmd)
        {
            _selectedSubCommand = subCmd;
            propertyGridSubCommands.Children.Clear();
            CreatePropertyGrid(subCmd, propertyGridSubCommands);
            RebuildSubCommandCards();
        }

        private void AddSubCommand(PluginInfo plugin)
        {
            var newSubCommand = new SubCommandInfo
            {
                Name = "NewSubCmd",
                DisplayName = "Новая Подкоманда",
                ClassName = "Namespace.NewSubCommand"
            };

            if (plugin.SubCommands == null)
                plugin.SubCommands = new List<SubCommandInfo>();

            plugin.SubCommands.Add(newSubCommand);
            _isDirty = true;
            RebuildSubCommandCards();
            SelectSubCommand(newSubCommand);
            TriggerValidation();
        }

        private void RebuildSubCommandCards()
        {
            subCommandsContainer.Children.Clear();

            if (_selectedPlugin == null || _selectedPlugin.SubCommands == null || _selectedPlugin.UIType != PluginInfo.ButtonUIType.SplitButton)
                return;

            foreach (var subCmd in _selectedPlugin.SubCommands)
            {
                var card = CreateSubCommandCard(subCmd);
                subCommandsContainer.Children.Add(card);
            }

            // Кнопка добавления новой подкоманды
            var addButton = new Button
            {
                Content = "+ Добавить подкоманду",
                Height = 40,
                Margin = new Thickness(10),
                FontWeight = FontWeights.Bold
            };
            addButton.Click += (s, e) => AddSubCommand(_selectedPlugin);
            subCommandsContainer.Children.Add(addButton);
        }

        private Border CreateSubCommandCard(SubCommandInfo subCmd)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Tag = subCmd
            };

            var stack = new StackPanel();

            // Название подкоманды
            var nameText = new TextBlock
            {
                Text = subCmd.DisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stack.Children.Add(nameText);

            // Кнопки редактирования и удаления под названием
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var editBtn = new Button
            {
                Content = "Редактировать",
                Width = 100,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0)
            };
            editBtn.Click += (s, e) => SelectSubCommand(subCmd);
            buttonsPanel.Children.Add(editBtn);

            var deleteBtn = new Button
            {
                Content = "✕",
                Width = 25,
                Height = 25,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Red
            };
            deleteBtn.Click += (s, e) =>
            {
                if (MessageBox.Show($"Удалить подкоманду '{subCmd.DisplayName}'?",
                    "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _selectedPlugin.SubCommands?.Remove(subCmd);
                    if (_selectedSubCommand == subCmd)
                    {
                        _selectedSubCommand = null;
                        propertyGridSubCommands.Children.Clear();
                    }
                    _isDirty = true;
                    RebuildSubCommandCards();
                    TriggerValidation();
                }
            };
            buttonsPanel.Children.Add(deleteBtn);

            stack.Children.Add(buttonsPanel);

            // Информация
            var infoText = new TextBlock
            {
                Text = $"ID: {subCmd.Name} | Класс: {subCmd.ClassName ?? "не указан"}",
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(infoText);

            card.Child = stack;

            // Выделение выбранной подкоманды
            if (_selectedSubCommand == subCmd)
            {
                card.BorderBrush = Brushes.Blue;
                card.BorderThickness = new Thickness(3);
            }

            return card;
        }


        private void RebuildPulldownCards()
        {
            pulldownCardsContainer.Children.Clear();

            foreach (var pulldown in _pulldownDefinitions.OrderBy(p => p.RibbonTab).ThenBy(p => p.DisplayName))
            {
                var card = CreatePulldownCard(pulldown);
                pulldownCardsContainer.Children.Add(card);
            }

            // Кнопка добавления
            var addButton = new Button
            {
                Content = "+ Добавить новый PulldownButton",
                Height = 40,
                Margin = new Thickness(10),
                FontWeight = FontWeights.Bold
            };
            addButton.Click += BtnAddPulldown_Click;
            pulldownCardsContainer.Children.Add(addButton);
        }

        private Border CreatePulldownCard(PulldownButtonDefinitionInfo pulldown)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                AllowDrop = true,
                Tag = pulldown
            };

            card.MouseLeftButtonDown += (s, e) =>
            {
                if (card.Tag is PulldownButtonDefinitionInfo pb)
                    DragDrop.DoDragDrop(card, pb, DragDropEffects.Move);
            };

            card.Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(typeof(PulldownButtonDefinitionInfo)) && card.Tag is PulldownButtonDefinitionInfo targetPb)
                {
                    var sourcePb = e.Data.GetData(typeof(PulldownButtonDefinitionInfo)) as PulldownButtonDefinitionInfo;
                    if (sourcePb != null && sourcePb != targetPb)
                    {
                        int sourceIndex = _pulldownDefinitions.IndexOf(sourcePb);
                        int targetIndex = _pulldownDefinitions.IndexOf(targetPb);

                        _pulldownDefinitions.Move(sourceIndex, targetIndex);
                        _isDirty = true;
                        RebuildPulldownCards();
                    }
                }
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Added for edit button

            var nameText = new TextBlock { Text = pulldown.DisplayName, FontWeight = FontWeights.Bold, FontSize = 14 };
            Grid.SetColumn(nameText, 0);
            headerGrid.Children.Add(nameText);

            var enabledCheck = new CheckBox { IsChecked = pulldown.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            enabledCheck.Checked += (s, e) => { pulldown.Enabled = true; _isDirty = true; TriggerValidation(); };
            enabledCheck.Unchecked += (s, e) => { pulldown.Enabled = false; _isDirty = true; TriggerValidation(); };
            Grid.SetColumn(enabledCheck, 1);
            headerGrid.Children.Add(enabledCheck);

            var editBtn = new Button { Content = "Редактировать", Width = 100, Height = 25, Margin = new Thickness(10, 0, 0, 0) };
            editBtn.Click += (s, e) => SelectPulldown(pulldown);
            Grid.SetColumn(editBtn, 2);
            headerGrid.Children.Add(editBtn);

            Grid.SetRow(headerGrid, 0);
            grid.Children.Add(headerGrid);

            var infoText = new TextBlock
            {
                Text = $"ID: {pulldown.Name} | Tab: {pulldown.RibbonTab} | Panel: {pulldown.RibbonPanel}",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(infoText, 1);
            grid.Children.Add(infoText);

            card.Child = grid;

            if (_selectedPulldown == pulldown)
            {
                card.BorderBrush = Brushes.Blue;
                card.BorderThickness = new Thickness(3);
            }

            return card;
        }

        private void SelectPulldown(PulldownButtonDefinitionInfo pulldown)
        {
            _selectedPulldown = pulldown;
            propertyGridPulldown.Children.Clear();
            CreatePropertyGrid(pulldown, propertyGridPulldown);
            RebuildPulldownCards();
        }

        private void CreatePropertyGrid(object obj, StackPanel container)
        {
            container.Children.Clear();

            if (obj == null) return;

            var properties = TypeDescriptor.GetProperties(obj)
                .Cast<PropertyDescriptor>()
                .Where(p => p.IsBrowsable)
                .GroupBy(p => p.Category)
                .OrderBy(g => g.Key);

            var enabledAndLoadOnStartupProps = new List<PropertyDescriptor>();
            var otherProps = new List<IGrouping<string, PropertyDescriptor>>();

            foreach (var categoryGroup in properties)
            {
                var groupList = categoryGroup.ToList();
                var specialProps = groupList.Where(p => p.Name == "Enabled" || p.Name == "LoadOnStartup").ToList();
                var regularProps = groupList.Except(specialProps).ToList();

                enabledAndLoadOnStartupProps.AddRange(specialProps);

                if (regularProps.Any())
                {
                    otherProps.Add(regularProps.GroupBy(p => categoryGroup.Key).First());
                }
            }

            // Render "Активен" and "Загружать при старте" first
            if (enabledAndLoadOnStartupProps.Any())
            {
                var topHeader = new TextBlock
                {
                    Text = "Основные настройки",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204))
                };
                container.Children.Add(topHeader);

                foreach (var prop in enabledAndLoadOnStartupProps.OrderBy(p => p.Name == "Enabled" ? 0 : 1))
                {
                    CreatePropertyControl(obj, prop, container);
                }
            }

            // Render other categories
            foreach (var categoryGroup in otherProps)
            {
                var categoryHeader = new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, 10, 0, 5),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204))
                };
                container.Children.Add(categoryHeader);

                foreach (var prop in categoryGroup)
                {
                    CreatePropertyControl(obj, prop, container);
                }
            }
        }

        private void CreatePropertyControl(object obj, PropertyDescriptor prop, StackPanel container)
        {
            var propertyContainer = new StackPanel { Margin = new Thickness(0, 5, 0, 10) };

            // Label
            var label = new TextBlock
            {
                Text = prop.DisplayName ?? prop.Name,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 3)
            };
            propertyContainer.Children.Add(label);

            var value = prop.GetValue(obj);

            if (prop.Name == "AllowedDepartments")
            {
                CreateDepartmentsControl(obj, prop, propertyContainer, value);
            }
            else if (prop.Name == "AllowedUsers")
            {
                CreateAllowedUsersControl(obj, prop, propertyContainer, value);
            }
            // Special handling for Description property
            else if (prop.Name == "Description")
            {
                CreateDescriptionControl(obj, prop, propertyContainer, value);
            }
            // Special handling for RibbonTab and RibbonPanel
            else if (prop.Name == "RibbonTab" || prop.Name == "RibbonPanel")
            {
                CreateComboBoxWithTextInput(obj, prop, propertyContainer, value);
            }
            // Special handling for AssemblyPath and icon properties
            else if (prop.Name == "AssemblyPath" || prop.Name == "LargeIcon" || prop.Name == "SmallIcon")
            {
                CreateFilePathControl(obj, prop, propertyContainer, value);
            }
            // Boolean properties
            else if (prop.PropertyType == typeof(bool))
            {
                var checkbox = new CheckBox
                {
                    IsChecked = (bool?)value,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                checkbox.Checked += (s, e) => { prop.SetValue(obj, true); _isDirty = true; TriggerValidation(); };
                checkbox.Unchecked += (s, e) => { prop.SetValue(obj, false); _isDirty = true; TriggerValidation(); };
                propertyContainer.Children.Add(checkbox);
            }
            // Enum properties
            else if (prop.PropertyType.IsEnum)
            {
                var combo = new ComboBox
                {
                    ItemsSource = Enum.GetValues(prop.PropertyType),
                    SelectedItem = value,
                    Height = 25
                };
                combo.SelectionChanged += (s, e) => { prop.SetValue(obj, combo.SelectedItem); _isDirty = true; TriggerValidation(); };
                propertyContainer.Children.Add(combo);
            }
            // Default text properties
            else
            {
                var textbox = new TextBox
                {
                    Text = value?.ToString() ?? "",
                    IsReadOnly = prop.IsReadOnly,
                    Height = 25,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(5, 0, 5, 0)
                };
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
                propertyContainer.Children.Add(textbox);
            }

            container.Children.Add(propertyContainer);
        }

        private void CreateDepartmentsControl(object obj, PropertyDescriptor prop, StackPanel container, object value)
        {
            var departments = new[] { "АР", "КР", "АД", "ГИП", "ЭЛ", "ТХ", "ПБ", "ВК", "СС", "ОВ", "ОИМ" };
            var currentDepartments = value as List<string> ?? new List<string>();

            var wrapPanel = new WrapPanel { Margin = new Thickness(0, 5, 0, 5) };

            foreach (var dept in departments)
            {
                var checkbox = new CheckBox
                {
                    Content = dept,
                    IsChecked = currentDepartments.Contains(dept),
                    Margin = new Thickness(0, 2, 10, 2),
                    MinWidth = 50
                };

                checkbox.Checked += (s, e) =>
                {
                    if (!currentDepartments.Contains(dept))
                    {
                        currentDepartments.Add(dept);
                        prop.SetValue(obj, currentDepartments);
                        _isDirty = true;
                        TriggerValidation();
                    }
                };

                checkbox.Unchecked += (s, e) =>
                {
                    if (currentDepartments.Contains(dept))
                    {
                        currentDepartments.Remove(dept);
                        prop.SetValue(obj, currentDepartments);
                        _isDirty = true;
                        TriggerValidation();
                    }
                };

                wrapPanel.Children.Add(checkbox);
            }

            container.Children.Add(wrapPanel);
        }

        private void CreateAllowedUsersControl(object obj, PropertyDescriptor prop, StackPanel container, object value)
        {
            var currentUsers = value as List<string> ?? new List<string>();
            var usersText = string.Join(", ", currentUsers);

            // Container grid with toggle button
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var readOnlyTextBox = new TextBox
            {
                Text = usersText,
                Height = 25,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };
            Grid.SetColumn(readOnlyTextBox, 0);
            grid.Children.Add(readOnlyTextBox);

            var toggleButton = new Button
            {
                Content = "✎",
                Width = 30,
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Редактировать список пользователей"
            };
            Grid.SetColumn(toggleButton, 1);
            grid.Children.Add(toggleButton);

            container.Children.Add(grid);

            // Editable TextBox (initially hidden)
            var editableTextBox = new TextBox
            {
                Text = usersText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 80,
                MaxHeight = 150,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(5),
                Visibility = Visibility.Collapsed
            };

            var instructionText = new TextBlock
            {
                Text = "Введите sAMAccountName пользователей через запятую (например: ivanov, petrov, sidorov)",
                FontSize = 11,
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap
            };

            editableTextBox.TextChanged += (s, e) =>
            {
                var users = editableTextBox.Text
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim())
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .ToList();

                prop.SetValue(obj, users);
                readOnlyTextBox.Text = string.Join(", ", users);
                _isDirty = true;
                TriggerValidation();
            };

            container.Children.Add(instructionText);
            container.Children.Add(editableTextBox);

            toggleButton.Click += (s, e) =>
            {
                if (editableTextBox.Visibility == Visibility.Collapsed)
                {
                    editableTextBox.Visibility = Visibility.Visible;
                    instructionText.Visibility = Visibility.Visible;
                    toggleButton.Content = "▲";
                    toggleButton.ToolTip = "Свернуть";
                }
                else
                {
                    editableTextBox.Visibility = Visibility.Collapsed;
                    instructionText.Visibility = Visibility.Collapsed;
                    toggleButton.Content = "✎";
                    toggleButton.ToolTip = "Редактировать список пользователей";
                }
            };
        }


        private void CreateDescriptionControl(object obj, PropertyDescriptor prop, StackPanel container, object value)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBox = new TextBox
            {
                Text = value?.ToString() ?? "",
                Height = 25,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                IsReadOnly = true
            };
            Grid.SetColumn(textBox, 0);
            grid.Children.Add(textBox);

            var toggleButton = new Button
            {
                Content = "...",
                Width = 30,
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0)
            };
            Grid.SetColumn(toggleButton, 1);
            grid.Children.Add(toggleButton);

            container.Children.Add(grid);

            // MultiLine TextBox (initially hidden)
            var multiLineTextBox = new TextBox
            {
                Text = value?.ToString() ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 100,
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(5),
                Visibility = Visibility.Collapsed
            };
            multiLineTextBox.TextChanged += (s, e) =>
            {
                prop.SetValue(obj, multiLineTextBox.Text);
                textBox.Text = multiLineTextBox.Text;
                _isDirty = true;
                TriggerValidation();
            };
            container.Children.Add(multiLineTextBox);

            toggleButton.Click += (s, e) =>
            {
                if (multiLineTextBox.Visibility == Visibility.Collapsed)
                {
                    multiLineTextBox.Visibility = Visibility.Visible;
                    toggleButton.Content = "▲";
                }
                else
                {
                    multiLineTextBox.Visibility = Visibility.Collapsed;
                    toggleButton.Content = "...";
                }
            };
        }

        private void CreateComboBoxWithTextInput(object obj, PropertyDescriptor prop, StackPanel container, object value)
        {
            var existingValues = new List<string>();

            if (prop.Name == "RibbonTab")
            {
                existingValues = _currentConfiguration.Plugins
                    .Select(p => p.RibbonTab)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();
            }
            else if (prop.Name == "RibbonPanel")
            {
                existingValues = _currentConfiguration.Plugins
                    .Select(p => p.RibbonPanel)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .OrderBy(p => p)
                    .ToList();
            }

            var comboBox = new ComboBox
            {
                IsEditable = true,
                ItemsSource = existingValues,
                Text = value?.ToString() ?? "",
                Height = 25
            };

            comboBox.LostFocus += (s, e) =>
            {
                prop.SetValue(obj, comboBox.Text);
                _isDirty = true;
                TriggerValidation();
            };

            container.Children.Add(comboBox);
        }

        private void CreateFilePathControl(object obj, PropertyDescriptor prop, StackPanel container, object value)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBox = new TextBox
            {
                Text = value?.ToString() ?? "",
                Height = 25,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0)
            };
            textBox.TextChanged += (s, e) =>
            {
                prop.SetValue(obj, textBox.Text);
                _isDirty = true;
                TriggerValidation();
            };
            Grid.SetColumn(textBox, 0);
            grid.Children.Add(textBox);

            var browseButton = new Button
            {
                Content = "📁",
                Width = 30,
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0)
            };
            browseButton.Click += (s, e) => BrowseFile(obj, prop, textBox);
            Grid.SetColumn(browseButton, 1);
            grid.Children.Add(browseButton);

            container.Children.Add(grid);
        }

        private void BrowseFile(object obj, PropertyDescriptor prop, TextBox textBox)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();

            if (prop.Name == "AssemblyPath")
            {
                dialog.Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*";
                dialog.Title = "Выберите файл сборки";

                // Open in schema directory
                var schemaDir = System.IO.Path.GetDirectoryName(_schemaFilePath);
                if (!string.IsNullOrEmpty(schemaDir) && System.IO.Directory.Exists(schemaDir))
                {
                    dialog.InitialDirectory = schemaDir;
                }
            }
            else // Icon files
            {
                dialog.Filter = "Image Files (*.png;*.jpg;*.ico)|*.png;*.jpg;*.ico|All Files (*.*)|*.*";
                dialog.Title = "Выберите файл иконки";

                string initialDir = null;

                if (obj is PluginInfo plugin && !string.IsNullOrWhiteSpace(plugin.AssemblyPath))
                {
                    var assemblyPath = System.IO.Path.Combine(_basePath, plugin.AssemblyPath);
                    var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);

                    // Try Icons folder first
                    if (!string.IsNullOrEmpty(assemblyDir))
                    {
                        var iconsDir = System.IO.Path.Combine(assemblyDir, "Icons");
                        if (System.IO.Directory.Exists(iconsDir))
                        {
                            initialDir = iconsDir;
                        }
                        else if (System.IO.Directory.Exists(assemblyDir))
                        {
                            initialDir = assemblyDir;
                        }
                    }
                }
                else if (obj is SubCommandInfo)
                {
                    // For SubCommand, try to get assembly path from parent plugin
                    if (_selectedPlugin != null && !string.IsNullOrWhiteSpace(_selectedPlugin.AssemblyPath))
                    {
                        var assemblyPath = System.IO.Path.Combine(_basePath, _selectedPlugin.AssemblyPath);
                        var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);

                        if (!string.IsNullOrEmpty(assemblyDir))
                        {
                            var iconsDir = System.IO.Path.Combine(assemblyDir, "Icons");
                            if (System.IO.Directory.Exists(iconsDir))
                            {
                                initialDir = iconsDir;
                            }
                            else if (System.IO.Directory.Exists(assemblyDir))
                            {
                                initialDir = assemblyDir;
                            }
                        }
                    }
                }

                // Final fallback to default MagicEntry folder
                if (string.IsNullOrEmpty(initialDir))
                {
                    var defaultDir = @"C:\ProgramData\Autodesk\Revit\Addins\2022\MagicEntry";
                    if (System.IO.Directory.Exists(defaultDir))
                    {
                        initialDir = defaultDir;
                    }
                    else
                    {
                        // Last resort: schema directory
                        var schemaDir = System.IO.Path.GetDirectoryName(_schemaFilePath);
                        if (!string.IsNullOrEmpty(schemaDir) && System.IO.Directory.Exists(schemaDir))
                        {
                            initialDir = schemaDir;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(initialDir))
                {
                    dialog.InitialDirectory = initialDir;
                }
            }

            if (dialog.ShowDialog() == true)
            {
                string selectedFile = dialog.FileName;

                // Находим директорию сборки
                string assemblyPath = Path.Combine(_basePath, _selectedPlugin.AssemblyPath);
                string assemblyDir = Path.GetDirectoryName(assemblyPath);

                // Относительный путь от assemblyDir → selectedFile
                string relativePath = MakeRelativePath(assemblyDir, selectedFile);

                textBox.Text = relativePath;
                prop.SetValue(obj, relativePath);

                _isDirty = true;
                TriggerValidation();
            }

        }

        private string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) return toPath;
            if (string.IsNullOrEmpty(toPath)) return toPath;

            var fromUri = new Uri(fromPath.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) return toPath;

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
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
            if (_validationTimer != null)
            {
                _validationTimer.Stop();
                _validationTimer.Start();
            }
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
            RebuildPulldownCards();
            SelectPulldown(newPulldown);
            TriggerValidation();
        }

        private void BtnRemovePulldown_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPulldown != null)
            {
                if (MessageBox.Show($"Удалить определение Pulldown '{_selectedPulldown.DisplayName}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _pulldownDefinitions.Remove(_selectedPulldown);
                    _selectedPulldown = null;
                    _isDirty = true;
                    RebuildPulldownCards();
                    TriggerValidation();
                }
            }
        }

        private void BtnTogglePulldownEnable_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPulldown != null)
            {
                _selectedPulldown.Enabled = !_selectedPulldown.Enabled;
                _isDirty = true;
                RebuildPulldownCards();
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
            var btnOk = new Button { Content = "OK", Width = 100, Height = 30, Margin = new Thickness(0, 10, 0, 0) };
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
                RebuildPluginCards();
                PopulateFilterComboBox();
                SelectPlugin(newPlugin);
                TriggerValidation();
            }
        }

        private void BtnRemovePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin != null)
            {
                if (MessageBox.Show($"Удалить плагин '{_selectedPlugin.DisplayName}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _plugins.Remove(_selectedPlugin);
                    _selectedPlugin = null;
                    _isDirty = true;
                    RebuildPluginCards();
                    TriggerValidation();
                }
            }
        }

        private void BtnTogglePluginEnable_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin != null)
            {
                _selectedPlugin.Enabled = !_selectedPlugin.Enabled;
                _isDirty = true;
                RebuildPluginCards();
                TriggerValidation();
            }
        }

        private void BtnAssignPulldownGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin != null)
            {
                var availableGroups = _pulldownDefinitions
                    .Where(p => p.Enabled)
                    .Select(p => p.Name)
                    .Distinct()
                    .OrderBy(g => g)
                    .ToList();

                if (availableGroups.Any())
                {
                    var dialog = new Window
                    {
                        Title = "Выберите группу Pulldown",
                        Width = 300,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this
                    };

                    var panel = new StackPanel { Margin = new Thickness(10) };
                    var combo = new ComboBox { Margin = new Thickness(0, 10, 0, 10) };
                    combo.Items.Add("");
                    foreach (var group in availableGroups)
                        combo.Items.Add(group);
                    combo.SelectedItem = _selectedPlugin.PulldownGroupName ?? "";

                    var btnOk = new Button { Content = "OK", Width = 100, Height = 30, IsDefault = true };
                    btnOk.Click += (s, args) => { dialog.DialogResult = true; dialog.Close(); };

                    panel.Children.Add(new TextBlock { Text = "Группа Pulldown:" });
                    panel.Children.Add(combo);
                    panel.Children.Add(btnOk);
                    dialog.Content = panel;

                    if (dialog.ShowDialog() == true)
                    {
                        string selectedGroup = combo.SelectedItem as string ?? "";
                        if (_selectedPlugin.PulldownGroupName != selectedGroup)
                        {
                            _selectedPlugin.PulldownGroupName = selectedGroup;
                            _isDirty = true;
                            SelectPlugin(_selectedPlugin);
                            TriggerValidation();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Нет доступных групп Pulldown. Сначала создайте PulldownButton Definition.",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnAddSubCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin != null &&
                _selectedPlugin.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                var newSubCommand = new SubCommandInfo
                {
                    Name = "NewSubCmd",
                    DisplayName = "Новая Подкоманда",
                    ClassName = "Namespace.NewSubCommand"
                };
                if (_selectedPlugin.SubCommands == null)
                    _selectedPlugin.SubCommands = new List<SubCommandInfo>();

                _selectedPlugin.SubCommands.Add(newSubCommand);
                _isDirty = true;
                RebuildSubCommandCards();
                SelectSubCommand(newSubCommand);
                TriggerValidation();
            }
        }

        private void BtnRemoveSubCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin != null && _selectedSubCommand != null)
            {
                if (MessageBox.Show($"Удалить подкоманду '{_selectedSubCommand.DisplayName}' из плагина '{_selectedPlugin.DisplayName}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _selectedPlugin.SubCommands?.Remove(_selectedSubCommand);
                    _selectedSubCommand = null;
                    _isDirty = true;
                    RebuildSubCommandCards();
                    TriggerValidation();
                }
            }
        }

        private void BtnScanPlugins_Click(object sender, RoutedEventArgs e)
        {
            var scanDialog = new PluginScanDialog(_basePath);
            scanDialog.Owner = this;
            if (scanDialog.ShowDialog() == true)
            {
                foreach (var plugin in scanDialog.SelectedPlugins)
                {
                    _plugins.Add(plugin);
                }
                _isDirty = true;
                RebuildPluginCards();
                PopulateFilterComboBox();
                TriggerValidation();
            }
        }

        private void BtnToggleValidation_Click(object sender, RoutedEventArgs e)
        {
            _isValidationPanelVisible = !_isValidationPanelVisible;
            validationPanel.Visibility = _isValidationPanelVisible ? Visibility.Visible : Visibility.Collapsed;
            btnToggleValidation.Content = _isValidationPanelVisible ? "Скрыть ошибки" : "Показать ошибки";

            if (_isValidationPanelVisible)
            {
                PerformValidation();
                UpdateValidationDisplay();
            }
        }

        private void BtnCollapseValidation_Click(object sender, RoutedEventArgs e)
        {
            _isValidationPanelVisible = false;
            validationPanel.Visibility = Visibility.Collapsed;
            btnToggleValidation.Content = "Показать ошибки";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (mainTabControl.SelectedIndex == 0)
                RebuildPluginCards();
        }

        private void CmbFilterTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mainTabControl.SelectedIndex == 0)
                RebuildPluginCards();
        }

        private void ChkShowDisabled_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (mainTabControl == null) return;

            if (mainTabControl.SelectedIndex == 0)
            {
                RebuildPluginCards();
            }
            else if (mainTabControl.SelectedIndex == 1)
            {
                RebuildPulldownCards();
            }
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
