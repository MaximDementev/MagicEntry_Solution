using Autodesk.Revit.UI;
using MagicEntry.Core.Interfaces;
using MagicEntry.Core.Models;
using MagicEntry.Core.Services;
using MagicEntry.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MagicEntry.Services
{
    // Управляет загрузкой, инициализацией плагинов и созданием их UI.
    public class PluginManager : IPluginManager
    {
        #region Fields

        private readonly IConfigurationReader _configurationReader;
        private readonly IPluginLoader _pluginLoader;
        private PluginConfiguration _pluginConfiguration;
        private readonly List<IPlugin> _loadedPlugins;
        private string _MagicEntryBasePath;
        private readonly Dictionary<string, PulldownButton> _createdPulldownButtons;
        private IUserAccessService _userAccessService;

        #endregion

        #region Constructor

        public PluginManager(IConfigurationReader configurationReader, IPluginLoader pluginLoader)
        {
            _configurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
            _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));
            _loadedPlugins = new List<IPlugin>();
            _createdPulldownButtons = new Dictionary<string, PulldownButton>();
        }

        #endregion

        #region IPluginManager Implementation

        public IReadOnlyCollection<IPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

        // Загружает конфигурацию и экземпляры плагинов.
        public void LoadPlugins(string configurationPath, string MagicEntryBasePath)
        {
            if (string.IsNullOrWhiteSpace(configurationPath))
                throw new ArgumentException("Путь к файлу конфигурации не может быть пустым.", nameof(configurationPath));
            if (string.IsNullOrWhiteSpace(MagicEntryBasePath))
                throw new ArgumentException("Базовый путь MagicEntry не может быть пустым.", nameof(MagicEntryBasePath));

            _MagicEntryBasePath = MagicEntryBasePath;

            _userAccessService = ServiceProvider.GetService<IUserAccessService>();
            if (_userAccessService == null)
            {
                _userAccessService = new UserAccessService();
                ServiceProvider.RegisterService<IUserAccessService>(_userAccessService);
            }

            try
            {
                _pluginConfiguration = _configurationReader.ReadConfiguration(configurationPath);

                _loadedPlugins.Clear();
                foreach (var pluginInfo in _pluginConfiguration.Plugins.Where(pi => pi.Enabled && pi.LoadOnStartup))
                {
                    try
                    {
                        IPlugin pluginInstance = _pluginLoader.LoadPlugin(pluginInfo, _MagicEntryBasePath);
                        if (pluginInstance != null)
                        {
                            _loadedPlugins.Add(pluginInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Plugin Load Error", $"Ошибка при загрузке экземпляра плагина '{pluginInfo.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Критическая ошибка при загрузке конфигурации плагинов из '{configurationPath}': {ex.Message}", ex);
            }
        }

        // Инициализирует плагины и создает UI.
        public void InitializePluginsAndCreateUI(UIControlledApplication application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (_pluginConfiguration == null)
            {
                TaskDialog.Show("Plugin UI Error", "Конфигурация плагинов не загружена.");
                return;
            }

            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.Initialize();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Plugin Initialization Error", $"Ошибка при внутренней инициализации плагина '{plugin.Info?.Name}': {ex.Message}");
                }
            }

            var uiElementsByPanel = _pluginConfiguration.PulldownButtonDefinitions
                .Where(pbd => pbd.Enabled)
                .Cast<object>()
                .Concat(_pluginConfiguration.Plugins.Cast<object>())
                .Where(item =>
                {
                    if (item is PluginInfo pi)
                    {
                        if (!pi.Enabled || !pi.LoadOnStartup)
                            return false;

                        return _userAccessService.HasAccess(pi.AllowedDepartments, pi.AllowedUsers);
                    }
                    return true;
                })
                .GroupBy(item =>
                {
                    if (item is PulldownButtonDefinitionInfo pbd) return new { Tab = pbd.RibbonTab, Panel = pbd.RibbonPanel };
                    if (item is PluginInfo pi) return new { Tab = pi.RibbonTab, Panel = pi.RibbonPanel };
                    return null;
                })
                .Where(g => g.Key != null);

            foreach (var panelGroup in uiElementsByPanel)
            {
                var tabName = panelGroup.Key.Tab;
                var panelName = panelGroup.Key.Panel;
                RibbonPanel ribbonPanel = GetOrCreateRibbonPanel(application, tabName, panelName);

                foreach (var pbdInfo in panelGroup.OfType<PulldownButtonDefinitionInfo>().Where(pbd => pbd.Enabled))
                {
                    CreateActualPulldownButton(ribbonPanel, pbdInfo);
                }

                foreach (var pluginInfo in panelGroup.OfType<PluginInfo>())
                {
                    string pluginAssemblyFullPath = Path.Combine(_MagicEntryBasePath, pluginInfo.AssemblyPath);
                    string pluginAssemblyDir = Path.GetDirectoryName(pluginAssemblyFullPath);

                    if (!File.Exists(pluginAssemblyFullPath))
                    {
                        TaskDialog.Show("Plugin UI Error", $"Сборка не найдена для плагина '{pluginInfo.Name}': {pluginAssemblyFullPath}");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(pluginInfo.PulldownGroupName))
                    {
                        var targetPulldownDef = _pluginConfiguration.PulldownButtonDefinitions
                            .FirstOrDefault(pbd => pbd.Name == pluginInfo.PulldownGroupName && pbd.RibbonTab == tabName && pbd.RibbonPanel == panelName);

                        if (targetPulldownDef != null && targetPulldownDef.Enabled)
                        {
                            string pulldownKey = GeneratePulldownKey(tabName, panelName, pluginInfo.PulldownGroupName);
                            if (_createdPulldownButtons.TryGetValue(pulldownKey, out PulldownButton pulldownButton))
                            {
                                AddItemToPulldownButton(pulldownButton, pluginInfo, pluginAssemblyFullPath, pluginAssemblyDir);
                            }
                            else
                            {
                                TaskDialog.Show("Plugin UI Warning", $"PulldownButton '{pluginInfo.PulldownGroupName}' определен, но не был создан (возможно, из-за ошибки). Плагин '{pluginInfo.Name}' не будет добавлен в него.");
                            }
                        }
                        else if (targetPulldownDef == null)
                        {
                            TaskDialog.Show("Plugin UI Warning", $"PulldownButton '{pluginInfo.PulldownGroupName}' не определен для панели '{panelName}'. Плагин '{pluginInfo.Name}' не будет добавлен.");
                        }
                    }
                    else
                    {
                        AddItemToPanel(ribbonPanel, pluginInfo, pluginAssemblyFullPath, pluginAssemblyDir);
                    }
                }
            }
        }

        // Завершение работы плагинов.
        public void ShutdownPlugins()
        {
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.Shutdown();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Plugin Shutdown Error", $"Ошибка при завершении работы плагина '{plugin.Info?.Name}': {ex.Message}");
                }
            }
            _loadedPlugins.Clear();
            _createdPulldownButtons.Clear();
            _pluginConfiguration = null;
        }

        #endregion

        #region Private UI Creation Methods

        // Генерирует ключ для словаря _createdPulldownButtons.
        private string GeneratePulldownKey(string tabName, string panelName, string pulldownName)
        {
            return $"{tabName}_{panelName}_{pulldownName}";
        }

        // Создает и регистрирует PulldownButton, если он активен.
        private void CreateActualPulldownButton(RibbonPanel ribbonPanel, PulldownButtonDefinitionInfo pbdInfo)
        {
            if (!pbdInfo.Enabled) return; // Не создаем, если отключен

            string pulldownKey = GeneratePulldownKey(pbdInfo.RibbonTab, pbdInfo.RibbonPanel, pbdInfo.Name);
            if (_createdPulldownButtons.ContainsKey(pulldownKey)) return;

            var pulldownButtonData = new PulldownButtonData(
                name: $"cmd_pulldown_{pbdInfo.Name.Replace(" ", "_")}_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                text: pbdInfo.DisplayName
            );

            if (!string.IsNullOrEmpty(pbdInfo.Description))
            {
                pulldownButtonData.ToolTip = pbdInfo.Description;
            }

            string largeIconPath = string.IsNullOrEmpty(pbdInfo.LargeIcon) ? null : Path.Combine(_MagicEntryBasePath, pbdInfo.LargeIcon);
            string smallIconPath = string.IsNullOrEmpty(pbdInfo.SmallIcon) ? null : Path.Combine(_MagicEntryBasePath, pbdInfo.SmallIcon);

            pulldownButtonData.LargeImage = LoadBitmapImage(largeIconPath);
            pulldownButtonData.Image = LoadBitmapImage(smallIconPath);

            var pulldownButton = ribbonPanel.AddItem(pulldownButtonData) as PulldownButton;
            if (pulldownButton != null)
            {
                _createdPulldownButtons[pulldownKey] = pulldownButton;
            }
            else
            {
                TaskDialog.Show("Plugin UI Error", $"Не удалось создать PulldownButton '{pbdInfo.DisplayName}'.");
            }
        }

        // Добавляет элемент (PushButton или SplitButton) на панель.
        private void AddItemToPanel(RibbonPanel ribbonPanel, PluginInfo pluginInfo, string pluginAssemblyFullPath, string pluginAssemblyDir)
        {
            if (pluginInfo.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                CreateSplitButtonOnPanel(ribbonPanel, pluginInfo, pluginAssemblyFullPath, pluginAssemblyDir);
            }
            else
            {
                CreatePushButtonOnPanel(ribbonPanel, pluginInfo, pluginAssemblyFullPath, pluginAssemblyDir);
            }
        }

        // Добавляет элемент (PushButton или SplitButton) в PulldownButton.
        private void AddItemToPulldownButton(PulldownButton pulldownButton, PluginInfo pluginInfo, string pluginAssemblyFullPath, string pluginAssemblyDir)
        {
            if (pluginInfo.UIType == PluginInfo.ButtonUIType.SplitButton)
            {
                if (!string.IsNullOrEmpty(pluginInfo.ClassName))
                {
                    var mainPushButtonData = CreatePushButton(pluginInfo);
                    SetContextualHelp(mainPushButtonData, pluginInfo.HelpUrl);
                    pulldownButton.AddPushButton(mainPushButtonData);

                    if (pluginInfo.SubCommands != null && pluginInfo.SubCommands.Any())
                        pulldownButton.AddSeparator();
                }

                foreach (var subCommandInfo in pluginInfo.SubCommands ?? Enumerable.Empty<SubCommandInfo>())
                {
                    var subPushButtonData = new PushButtonData(
                        name: $"cmd_sub_pd_{subCommandInfo.Name.Replace(" ", "_")}_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                        text: subCommandInfo.DisplayName,
                        assemblyName: pluginAssemblyFullPath,
                        className: subCommandInfo.ClassName
                    );
                    if (!string.IsNullOrEmpty(subCommandInfo.Description)) subPushButtonData.ToolTip = subCommandInfo.Description;

                    string subLargeIconPath = ResolveIconPath(subCommandInfo.LargeIcon, pluginAssemblyDir);
                    string subSmallIconPath = ResolveIconPath(subCommandInfo.SmallIcon, pluginAssemblyDir);
                    subPushButtonData.LargeImage = LoadBitmapImage(subLargeIconPath);
                    subPushButtonData.Image = LoadBitmapImage(subSmallIconPath);

                    SetContextualHelp(subPushButtonData, pluginInfo.HelpUrl);

                    pulldownButton.AddPushButton(subPushButtonData);
                }
            }
            else
            {
                var pushButtonData = CreatePushButton(pluginInfo);
                SetContextualHelp(pushButtonData, pluginInfo.HelpUrl);
                pulldownButton.AddPushButton(pushButtonData);
            }
        }

        private void SetContextualHelp(ButtonData buttonData, string helpUrl)
        {
            if (!string.IsNullOrWhiteSpace(helpUrl))
            {
                try
                {
                    buttonData.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, helpUrl));
                }
                catch (Exception ex)
                {
                    // Игнорируем ошибки установки ContextualHelp
                    System.Diagnostics.Debug.WriteLine($"Ошибка установки ContextualHelp: {ex.Message}");
                }
            }
        }

        private PushButtonData CreatePushButton(
            string name,
            string text,
            string assemblyName,
            string className,
            string largeIconPath,
            string smallIconPath,
            string description = null,
            double scaleDown = 0.95
            )
        {
            var pushButtonData = new PushButtonData(
                name,
                text,
                assemblyName,
                className
            );

            if (!string.IsNullOrEmpty(description))
                pushButtonData.ToolTip = description;

            var largeImg = LoadBitmapImage(largeIconPath);
            if (largeImg != null)
                largeImg = ScaleDown(largeImg, scaleDown);

            pushButtonData.LargeImage = largeImg;
            pushButtonData.Image = LoadBitmapImage(smallIconPath);

            return pushButtonData;
        }

        public PushButtonData CreatePushButton(PluginInfo pluginInfo,
            double scaleDown = 0.9)
        {
            string pluginAssemblyFullPath = pluginInfo.AssemblyPath;
            string pluginAssemblyDir = Path.GetDirectoryName(pluginAssemblyFullPath);

            string name =
                $"cmd_pb_{pluginInfo.Name.Replace(" ", "_")}_{Guid.NewGuid():N}".Substring(0, 8);

            string largeIcon = string.IsNullOrEmpty(pluginInfo.LargeIcon)
                ? null
                : Path.Combine(pluginAssemblyDir, pluginInfo.LargeIcon);

            string smallIcon = string.IsNullOrEmpty(pluginInfo.SmallIcon)
                ? null
                : Path.Combine(pluginAssemblyDir, pluginInfo.SmallIcon);

            return CreatePushButton(
                name,
                pluginInfo.DisplayName,
                pluginAssemblyFullPath,
                pluginInfo.ClassName,
                largeIcon,
                smallIcon, null, scaleDown
            );
        }

        // Создает PushButton на панели.
        private void CreatePushButtonOnPanel(RibbonPanel ribbonPanel, PluginInfo pluginInfo, string pluginAssemblyFullPath, string pluginAssemblyDir)
        {
            var pushButtonData = CreatePushButton(pluginInfo, 1);

            if (!string.IsNullOrEmpty(pluginInfo.Description))
                pushButtonData.ToolTip = pluginInfo.Description;

            SetContextualHelp(pushButtonData, pluginInfo.HelpUrl);

            ribbonPanel.AddItem(pushButtonData);
        }

        // Подготавливает SplitButtonData.
        private SplitButtonData PrepareSplitButtonData(PluginInfo pluginInfo, string pluginAssemblyFullPath, string pluginAssemblyDir)
        {
            var splitButtonData = new SplitButtonData(
                name: $"cmd_split_{pluginInfo.Name.Replace(" ", "_")}_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                text: pluginInfo.DisplayName
            );

            if (!string.IsNullOrEmpty(pluginInfo.Description))
                splitButtonData.ToolTip = pluginInfo.Description;

            string largeIconPath = string.IsNullOrEmpty(pluginInfo.LargeIcon) ? null : Path.Combine(pluginAssemblyDir, pluginInfo.LargeIcon);
            splitButtonData.LargeImage = LoadBitmapImage(largeIconPath);

            return splitButtonData;
        }

        // Создает SplitButton на панели и наполняет его подкомандами.
        private void CreateSplitButtonOnPanel(RibbonPanel ribbonPanel, PluginInfo pluginInfo, string pluginAssemblyFullPath, string pluginAssemblyDir)
        {
            var splitButtonData = PrepareSplitButtonData(pluginInfo, pluginAssemblyFullPath, pluginAssemblyDir);
            

            var splitButton = ribbonPanel.AddItem(splitButtonData) as SplitButton;

            if (!string.IsNullOrEmpty(pluginInfo.HelpUrl))
                splitButton.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, pluginInfo.HelpUrl));

            if (splitButton == null)
            {
                TaskDialog.Show("Plugin UI Error", $"Не удалось создать SplitButton для плагина '{pluginInfo.DisplayName}'.");
                return;
            }


            if (pluginInfo.SubCommands != null)
            {
                foreach (var subCommandInfo in pluginInfo.SubCommands)
                {
                    var subPushButtonData = new PushButtonData(
                        name: $"cmd_sub_split_{subCommandInfo.Name.Replace(" ", "_")}_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                        text: subCommandInfo.DisplayName,
                        assemblyName: pluginAssemblyFullPath,
                        className: subCommandInfo.ClassName
                    );

                    if (!string.IsNullOrEmpty(subCommandInfo.Description))
                        subPushButtonData.ToolTip = subCommandInfo.Description;

                    string subLargeIconPath = ResolveIconPath(subCommandInfo.LargeIcon, pluginAssemblyDir);
                    string subSmallIconPath = ResolveIconPath(subCommandInfo.SmallIcon, pluginAssemblyDir);
                    subPushButtonData.LargeImage = ScaleDown(LoadBitmapImage(subLargeIconPath),0.85);
                    subPushButtonData.Image = LoadBitmapImage(subSmallIconPath);

                    SetContextualHelp(subPushButtonData, pluginInfo.HelpUrl);

                    splitButton.AddPushButton(subPushButtonData);
                }
            }

            splitButton.IsSynchronizedWithCurrentItem = false;
        }


        #endregion

        #region Private Helper Methods

        private BitmapImage LoadBitmapImage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(path, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Icon Load Error", $"Ошибка при загрузке иконки '{path}': {ex.Message}");
                return null;
            }
        }

        private BitmapImage ScaleDown(BitmapImage original, double scaleFactor)
        {
            if (original == null || scaleFactor >= 1.0)
                return original;

            try
            {
                int newWidth = (int)(original.PixelWidth * scaleFactor);
                int newHeight = (int)(original.PixelHeight * scaleFactor);

                var scaled = new TransformedBitmap(original, new ScaleTransform(scaleFactor, scaleFactor));
                var bitmapImage = new BitmapImage();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(scaled));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }

                return bitmapImage;
            }
            catch
            {
                return original;
            }
        }

        private RibbonPanel GetOrCreateRibbonPanel(UIControlledApplication app, string tabName, string panelName)
        {
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch { }

            RibbonPanel panel = null;
            try
            {
                panel = app.GetRibbonPanels(tabName).FirstOrDefault(p => p.Name == panelName);
            }
            catch { }

            if (panel == null)
            {
                try
                {
                    panel = app.CreateRibbonPanel(tabName, panelName);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Panel Creation Error", $"Ошибка при создании панели '{panelName}' на вкладке '{tabName}': {ex.Message}");
                    return null;
                }
            }

            return panel;
        }

        string ResolveIconPath(string iconValue, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(iconValue))
                return null;

            // 1) Если путь абсолютный — используем напрямую
            if (Path.IsPathRooted(iconValue))
            {
                return File.Exists(iconValue) ? iconValue : null;
            }

            // 2) Путь относительный — ищем рядом со сборкой
            string combined = Path.Combine(baseDir, iconValue);

            return File.Exists(combined) ? combined : null;
        }

        #endregion
    }
}
