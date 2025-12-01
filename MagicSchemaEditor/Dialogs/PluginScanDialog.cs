using MagicEntry.Core.Models;
using MagicEntry.SchemaEditor.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MagicEntry.SchemaEditor.Dialogs
{
    // Диалог для сканирования папок и автоматического поиска плагинов
    public partial class PluginScanDialog : Form
    {
        #region Поля

        private readonly IPluginScanner _scanner;
        private List<PluginScanResult> _scanResults;
        private BindingList<PluginImportItem> _importItems;

        #endregion

        #region Свойства

        // Возвращает список плагинов, выбранных для импорта
        public List<PluginInfo> SelectedPlugins { get; private set; }

        #endregion

        #region Конструктор

        // Инициализирует диалог сканирования плагинов с базовым путем
        public PluginScanDialog(string basePath = null)
        {
            _scanner = new PluginScanner();
            SelectedPlugins = new List<PluginInfo>();
            InitializeComponent();
            SetupDataGridView();

            // Устанавливаем базовый путь по умолчанию
            if (!string.IsNullOrEmpty(basePath))
            {
                txtPath.Text = basePath;
            }
        }

        #endregion

        #region Приватные методы

        // Настраивает DataGridView для отображения найденных плагинов
        private void SetupDataGridView()
        {
            dgvPlugins.AutoGenerateColumns = false;
            dgvPlugins.AllowUserToAddRows = false;
            dgvPlugins.AllowUserToDeleteRows = false;
            dgvPlugins.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPlugins.MultiSelect = true;

            _importItems = new BindingList<PluginImportItem>();
            dgvPlugins.DataSource = _importItems;
        }

        // Выполняет сканирование выбранной папки
        private void ScanDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("Указанная папка не существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnScan.Enabled = false;
            progressBar.Visible = true;
            lblStatus.Text = "Сканирование...";

            try
            {
                _scanResults = _scanner.ScanDirectory(directoryPath);
                PopulateResults();
                lblStatus.Text = $"Найдено {_importItems.Count} плагинов";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сканировании: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Ошибка сканирования";
            }
            finally
            {
                btnScan.Enabled = true;
                progressBar.Visible = false;
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

            btnImport.Enabled = _importItems.Any();
            btnSelectAll.Enabled = _importItems.Any();
            btnSelectNone.Enabled = _importItems.Any();
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

        // Обрабатывает нажатие кнопки выбора папки
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Выберите папку для сканирования плагинов";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = folderDialog.SelectedPath;
                }
            }
        }

        // Обрабатывает нажатие кнопки сканирования
        private void btnScan_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPath.Text))
            {
                MessageBox.Show("Выберите папку для сканирования.", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ScanDirectory(txtPath.Text);
        }

        // Обрабатывает нажатие кнопки "Выбрать все"
        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            foreach (var item in _importItems)
            {
                item.IsSelected = true;
            }
            dgvPlugins.Refresh();
        }

        // Обрабатывает нажатие кнопки "Снять выбор"
        private void btnSelectNone_Click(object sender, EventArgs e)
        {
            foreach (var item in _importItems)
            {
                item.IsSelected = false;
            }
            dgvPlugins.Refresh();
        }

        // Обрабатывает нажатие кнопки импорта
        private void btnImport_Click(object sender, EventArgs e)
        {
            var selectedCount = _importItems.Count(i => i.IsSelected);
            if (selectedCount == 0)
            {
                MessageBox.Show("Выберите хотя бы один плагин для импорта.", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CreateSelectedPlugins();
            DialogResult = DialogResult.OK;
            Close();
        }

        // Обрабатывает нажатие кнопки отмены
        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        #endregion

        #region Designer Code

        private void InitializeComponent()
        {
            this.lblPath = new Label();
            this.txtPath = new TextBox();
            this.btnBrowse = new Button();
            this.btnScan = new Button();
            this.dgvPlugins = new DataGridView();
            this.colSelected = new DataGridViewCheckBoxColumn();
            this.colAssembly = new DataGridViewTextBoxColumn();
            this.colClassName = new DataGridViewTextBoxColumn();
            this.colDisplayName = new DataGridViewTextBoxColumn();
            this.colDescription = new DataGridViewTextBoxColumn();
            this.btnSelectAll = new Button();
            this.btnSelectNone = new Button();
            this.btnImport = new Button();
            this.btnCancel = new Button();
            this.progressBar = new ProgressBar();
            this.lblStatus = new Label();

            ((ISupportInitialize)(this.dgvPlugins)).BeginInit();
            this.SuspendLayout();

            // lblPath
            this.lblPath.AutoSize = true;
            this.lblPath.Location = new Point(12, 15);
            this.lblPath.Text = "Папка для сканирования:";

            // txtPath
            this.txtPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.txtPath.Location = new Point(12, 35);
            this.txtPath.Size = new Size(450, 20);

            // btnBrowse
            this.btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnBrowse.Location = new Point(470, 33);
            this.btnBrowse.Size = new Size(75, 23);
            this.btnBrowse.Text = "Обзор...";
            this.btnBrowse.Click += btnBrowse_Click;

            // btnScan
            this.btnScan.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.btnScan.Location = new Point(555, 33);
            this.btnScan.Size = new Size(75, 23);
            this.btnScan.Text = "Сканировать";
            this.btnScan.Click += btnScan_Click;

            // dgvPlugins
            this.dgvPlugins.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.dgvPlugins.Location = new Point(12, 70);
            this.dgvPlugins.Size = new Size(618, 300);
            this.dgvPlugins.Columns.AddRange(new DataGridViewColumn[] {
                this.colSelected, this.colAssembly, this.colClassName, this.colDisplayName, this.colDescription });

            // colSelected
            this.colSelected.DataPropertyName = "IsSelected";
            this.colSelected.HeaderText = "";
            this.colSelected.Width = 30;

            // colAssembly
            this.colAssembly.DataPropertyName = "AssemblyName";
            this.colAssembly.HeaderText = "Сборка";
            this.colAssembly.Width = 120;

            // colClassName
            this.colClassName.DataPropertyName = "ClassName";
            this.colClassName.HeaderText = "Класс";
            this.colClassName.Width = 150;

            // colDisplayName
            this.colDisplayName.DataPropertyName = "DisplayName";
            this.colDisplayName.HeaderText = "Имя";
            this.colDisplayName.Width = 120;

            // colDescription
            this.colDescription.DataPropertyName = "Description";
            this.colDescription.HeaderText = "Описание";
            this.colDescription.Width = 180;

            // btnSelectAll
            this.btnSelectAll.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnSelectAll.Location = new Point(12, 380);
            this.btnSelectAll.Size = new Size(100, 23);
            this.btnSelectAll.Text = "Выбрать все";
            this.btnSelectAll.Enabled = false;
            this.btnSelectAll.Click += btnSelectAll_Click;

            // btnSelectNone
            this.btnSelectNone.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.btnSelectNone.Location = new Point(120, 380);
            this.btnSelectNone.Size = new Size(100, 23);
            this.btnSelectNone.Text = "Снять выбор";
            this.btnSelectNone.Enabled = false;
            this.btnSelectNone.Click += btnSelectNone_Click;

            // progressBar
            this.progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.progressBar.Location = new Point(230, 380);
            this.progressBar.Size = new Size(200, 23);
            this.progressBar.Style = ProgressBarStyle.Marquee;
            this.progressBar.Visible = false;

            // lblStatus
            this.lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new Point(12, 415);
            this.lblStatus.Text = "Готов к сканированию";

            // btnImport
            this.btnImport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnImport.Location = new Point(470, 380);
            this.btnImport.Size = new Size(75, 23);
            this.btnImport.Text = "Импорт";
            this.btnImport.Enabled = false;
            this.btnImport.Click += btnImport_Click;

            // btnCancel
            this.btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnCancel.Location = new Point(555, 380);
            this.btnCancel.Size = new Size(75, 23);
            this.btnCancel.Text = "Отмена";
            this.btnCancel.Click += btnCancel_Click;

            // PluginScanDialog
            this.ClientSize = new Size(642, 450);
            this.Controls.Add(this.lblPath);
            this.Controls.Add(this.txtPath);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.dgvPlugins);
            this.Controls.Add(this.btnSelectAll);
            this.Controls.Add(this.btnSelectNone);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnImport);
            this.Controls.Add(this.btnCancel);
            this.Text = "Сканирование плагинов";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(650, 480);

            ((ISupportInitialize)(this.dgvPlugins)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private Label lblPath;
        private TextBox txtPath;
        private Button btnBrowse;
        private Button btnScan;
        private DataGridView dgvPlugins;
        private DataGridViewCheckBoxColumn colSelected;
        private DataGridViewTextBoxColumn colAssembly;
        private DataGridViewTextBoxColumn colClassName;
        private DataGridViewTextBoxColumn colDisplayName;
        private DataGridViewTextBoxColumn colDescription;
        private Button btnSelectAll;
        private Button btnSelectNone;
        private Button btnImport;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;

        #endregion
    }

    #region Вспомогательные классы

    // Элемент для импорта плагина
    public class PluginImportItem
    {
        public bool IsSelected { get; set; }
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string AssemblyPath { get; set; }
        public string FullAssemblyPath { get; set; }
    }

    #endregion
}