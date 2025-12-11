using MagicEntry.Core.Models;
using MagicEntry.SchemaEditor.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace MagicEntry.SchemaEditor.Dialogs
{
    public partial class PluginScanDialog : Window
    {
        #region Поля

        private readonly IPluginScanner _scanner;
        private List<PluginScanResult> _scanResults;
        private ObservableCollection<PluginImportItem> _importItems;

        #endregion

        #region Свойства

        // Возвращает список плагинов, выбранных для импорта
        public List<PluginInfo> SelectedPlugins { get; private set; }

        #endregion

        #region Конструктор

        public PluginScanDialog(string basePath = null)
        {
            _scanner = new PluginScanner();
            SelectedPlugins = new List<PluginInfo>();
            InitializeComponent();

            // Настройка привязки данных
            _importItems = new ObservableCollection<PluginImportItem>();
            dgvPlugins.ItemsSource = _importItems;

            // Устанавливаем базовый путь по умолчанию
            if (!string.IsNullOrEmpty(basePath))
            {
                txtPath.Text = basePath;
            }
        }

        #endregion

        #region Приватные методы

        // Выполняет сканирование выбранной папки
        private void ScanDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("Указанная папка не существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnScan.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            lblStatus.Text = "Сканирование...";

            try
            {
                _scanResults = _scanner.ScanDirectory(directoryPath);
                PopulateResults();
                lblStatus.Text = $"Найдено {_importItems.Count} плагинов";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сканировании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                lblStatus.Text = "Ошибка сканирования";
            }
            finally
            {
                btnScan.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        // Заполняет список результатами сканирования
        private void PopulateResults()
        {
            _importItems.Clear();

            foreach (var scanResult in _scanResults)
            {
                foreach (var command in scanResult.Commands)
                {
                    var importItem = new PluginImportItem
                    {
                        IsSelected = true,
                        AssemblyName = scanResult.AssemblyName,
                        ClassName = command.ClassName,
                        DisplayName = command.DisplayName,
                        Description = command.Description,
                        AssemblyPath = command.RelativeAssemblyPath,
                        FullAssemblyPath = scanResult.AssemblyPath
                    };
                    _importItems.Add(importItem);
                }
            }

            btnImport.IsEnabled = _importItems.Any();
            btnSelectAll.IsEnabled = _importItems.Any();
            btnSelectNone.IsEnabled = _importItems.Any();
        }

        // Создает PluginInfo из выбранных элементов
        private void CreateSelectedPlugins()
        {
            SelectedPlugins.Clear();

            foreach (var item in _importItems.Where(i => i.IsSelected))
            {
                var plugin = new PluginInfo
                {
                    Name = GeneratePluginName(item.ClassName),
                    DisplayName = item.DisplayName,
                    AssemblyPath = item.AssemblyPath,
                    ClassName = item.ClassName,
                    Description = item.Description,
                    RibbonTab = "MagicEntry",
                    RibbonPanel = "Импортированные",
                    UIType = PluginInfo.ButtonUIType.PushButton,
                    Enabled = true,
                    LoadOnStartup = true,
                    Version = "1.0.0"
                };

                SelectedPlugins.Add(plugin);
            }
        }

        // Генерирует уникальное имя плагина
        private string GeneratePluginName(string className)
        {
            var baseName = className.Split('.').Last();
            if (baseName.EndsWith("Command"))
                baseName = baseName.Substring(0, baseName.Length - 7);

            return $"{baseName}_{DateTime.Now:yyyyMMdd}";
        }

        #endregion

        #region Обработчики событий

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Выберите папку для сканирования плагинов";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtPath.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPath.Text))
            {
                MessageBox.Show("Выберите папку для сканирования.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ScanDirectory(txtPath.Text);
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _importItems)
            {
                item.IsSelected = true;
            }
            dgvPlugins.Items.Refresh();
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _importItems)
            {
                item.IsSelected = false;
            }
            dgvPlugins.Items.Refresh();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var selectedCount = _importItems.Count(i => i.IsSelected);
            if (selectedCount == 0)
            {
                MessageBox.Show("Выберите хотя бы один плагин для импорта.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreateSelectedPlugins();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }

    #region Вспомогательные классы

    public class PluginImportItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string AssemblyName { get; set; }
        public string ClassName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string AssemblyPath { get; set; }
        public string FullAssemblyPath { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}
