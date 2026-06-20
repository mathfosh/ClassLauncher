using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassLauncher.Models;

namespace ClassLauncher.Views.SettingsPages;

[SettingsPageInfo("classlauncher.settings", "ClassLauncher")]
public class SettingsPage : SettingsPageBase
{
    public Plugin Plugin { get; }
    private readonly StackPanel _appsPanel;
    private readonly TextBlock _availableCoursesLabel;

    public SettingsPage(Plugin plugin)
    {
        Plugin = plugin;
        DataContext = this;

        var scrollViewer = new ScrollViewer();
        var mainPanel = new StackPanel { MaxWidth = 750, Spacing = 6 };

        // 说明卡片
        var infoCard = new Border
        {
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "ClassLauncher", FontWeight = FontWeight.Bold, FontSize = 16 },
                    new TextBlock { Text = "配置在上课时提醒打开的软件。可为每个软件指定限定课程（逗号分隔），留空则匹配所有课程。",
                        TextWrapping = TextWrapping.Wrap }
                }
            }
        };
        mainPanel.Children.Add(infoCard);

        // 全局自动禁用配置
        mainPanel.Children.Add(CreateGlobalSettingsPanel());

        // 可用课程列表
        _availableCoursesLabel = new TextBlock
        {
            Text = "在「限定课程」中输入课程名称（与课表科目名称一致），多个用逗号分隔，留空则匹配所有课程。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = Brushes.Gray
        };
        mainPanel.Children.Add(_availableCoursesLabel);

        // 软件列表
        _appsPanel = new StackPanel { Spacing = 8 };
        RebuildAppList();
        mainPanel.Children.Add(_appsPanel);

        // 添加按钮
        var addBtn = new Button
        {
            Content = "+ 添加软件",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        addBtn.Click += AddApp_Click;
        mainPanel.Children.Add(addBtn);

        // 监听集合变化
        Plugin.Settings.Apps.CollectionChanged += OnAppsCollectionChanged;

        // 页面脱离视觉树时清理事件订阅
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        scrollViewer.Content = mainPanel;
        Content = scrollViewer;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Plugin.Settings.Apps.CollectionChanged -= OnAppsCollectionChanged;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    private void AddApp_Click(object? sender, RoutedEventArgs e)
    {
        Plugin.Settings.Apps.Add(new AppEntry { Name = "新软件", Path = "" });
    }

    private void DeleteApp_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control element && element.Tag is AppEntry app)
        {
            Plugin.Settings.Apps.Remove(app);
        }
    }

    private void ResetApp_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control element && element.Tag is AppEntry app)
        {
            app.ResetAutoDisableState();
            app.IsEnabled = true;
            RebuildAppList();
        }
    }

    private void OnAppsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildAppList();
    }

    private void RebuildAppList()
    {
        foreach (var child in _appsPanel.Children)
        {
            if (child is Control ctrl && ctrl.Tag is AppEntry)
                DetachTextBoxEvents(ctrl);
        }
        _appsPanel.Children.Clear();

        foreach (var app in Plugin.Settings.Apps)
        {
            _appsPanel.Children.Add(CreateAppRow(app));
        }
    }

    private static void DetachTextBoxEvents(Control row)
    {
        foreach (var tb in FindVisualChildren<TextBox>(row))
        {
            tb.PropertyChanged -= OnTextBoxPropertyChanged;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(Control parent) where T : Control
    {
        if (parent is T t)
            yield return t;

        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                foreach (var found in FindVisualChildren<T>(child))
                    yield return found;
            }
        }
        else if (parent is Border border && border.Child is Control borderChild)
        {
            foreach (var found in FindVisualChildren<T>(borderChild))
                yield return found;
        }
        else if (parent is ContentControl cc && cc.Content is Control ccChild)
        {
            foreach (var found in FindVisualChildren<T>(ccChild))
                yield return found;
        }
    }

    private static void OnTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || sender is not TextBox tb)
            return;
        if (tb.Tag is not AppEntry app)
            return;

        switch (tb.Name)
        {
            case "NameBox":
                app.Name = tb.Text ?? "";
                break;
            case "PathBox":
                app.Path = tb.Text ?? "";
                UpdateArgsBoxState(tb);
                break;
            case "ArgsBox":
                app.Arguments = tb.Text ?? "";
                ValidateArgsBox(tb);
                break;
            case "CourseBox":
                app.CourseNames = tb.Text ?? "";
                break;
            case "AdvanceBox":
                if (double.TryParse(tb.Text, out var minutes))
                    app.AdvanceMinutes = minutes;
                else if (!string.IsNullOrEmpty(tb.Text))
                    app.AdvanceMinutes = 0;
                break;
        }
    }

    /// <summary>
    /// 根据 PathBox 内容更新关联的 ArgsBox 启用状态
    /// </summary>
    private static void UpdateArgsBoxState(TextBox pathBox)
    {
        var argsBox = FindCompanionBox(pathBox, "ArgsBox");
        if (argsBox == null)
            return;

        var isExe = IsPathExecutable(pathBox.Text);
        argsBox.IsEnabled = isExe;
        argsBox.Watermark = isExe ? "示例: -w -h 1024" : "仅 .exe 文件支持参数";
    }

    /// <summary>
    /// 验证参数格式：非空时必须以 - 开头
    /// </summary>
    private static void ValidateArgsBox(TextBox argsBox)
    {
        var text = argsBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            argsBox.BorderBrush = null;
            ToolTip.SetTip(argsBox, null);
            return;
        }

        var args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allValid = args.All(a => a.StartsWith('-'));
        if (!allValid)
        {
            argsBox.BorderBrush = new SolidColorBrush(Colors.Red);
            ToolTip.SetTip(argsBox, "参数格式错误：每个参数必须以 - 开头");
        }
        else
        {
            argsBox.BorderBrush = null;
            ToolTip.SetTip(argsBox, null);
        }
    }

    /// <summary>
    /// 检测路径是否指向可执行文件（.exe）
    /// </summary>
    private static bool IsPathExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // 检查是否为目录
        if (Directory.Exists(path))
            return false;

        // 检查扩展名
        return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 在同一个 Grid 行中按 Name 查找关联的 TextBox
    /// </summary>
    private static TextBox? FindCompanionBox(TextBox source, string targetName)
    {
        // 从 source 的 StackPanel 找到 Grid
        var sp = source.Parent as StackPanel;
        var grid = sp?.Parent as Grid;
        if (grid == null)
            return null;

        foreach (var child in grid.Children)
        {
            foreach (var tb in FindVisualChildren<TextBox>(child))
            {
                if (tb.Name == targetName)
                    return tb;
            }
        }
        return null;
    }

    /// <summary>
    /// 创建全局自动禁用配置面板。
    /// </summary>
    private Control CreateGlobalSettingsPanel()
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = "自动禁用设置",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        panel.Children.Add(new TextBlock
        {
            Text = "当软件启动失败达到指定次数时，系统可自动禁用该规则，避免反复弹出错误提示。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            FontSize = 12
        });

        // 启用自动禁用
        var autoDisableRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        var autoDisableToggle = new ToggleSwitch
        {
            IsChecked = Plugin.Settings.AutoDisableEnabled,
            OnContent = "开",
            OffContent = "关"
        };
        autoDisableToggle.IsCheckedChanged += (_, _) =>
        {
            Plugin.Settings.AutoDisableEnabled = autoDisableToggle.IsChecked ?? true;
        };
        Grid.SetColumn(autoDisableToggle, 0);
        autoDisableRow.Children.Add(autoDisableToggle);

        var autoDisableLabel = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        autoDisableLabel.Children.Add(new TextBlock { Text = "启用自动禁用机制", FontSize = 13 });
        autoDisableLabel.Children.Add(new TextBlock { Text = "关闭后，启动失败时不会自动禁用任何规则", FontSize = 11, Foreground = Brushes.Gray });
        Grid.SetColumn(autoDisableLabel, 1);
        autoDisableRow.Children.Add(autoDisableLabel);
        panel.Children.Add(autoDisableRow);

        // 失败阈值
        var thresholdPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0), Spacing = 2 };
        thresholdPanel.Children.Add(new TextBlock { Text = "失败次数阈值", FontSize = 12, Foreground = Brushes.Gray });
        var thresholdBox = new TextBox
        {
            Text = Plugin.Settings.AutoDisableFailThreshold.ToString(),
            Width = 80,
            Watermark = "1"
        };
        thresholdBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            if (int.TryParse(thresholdBox.Text, out var val))
                Plugin.Settings.AutoDisableFailThreshold = val;
        };
        thresholdPanel.Children.Add(thresholdBox);
        panel.Children.Add(thresholdPanel);

        border.Child = panel;
        return border;
    }

    /// <summary>
    /// 处理 ToggleSwitch 切换：用户手动重新启用时，重置自动禁用状态。
    /// </summary>
    private static void OnToggleSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts || ts.Tag is not AppEntry app)
            return;

        var isChecked = ts.IsChecked ?? false;
        app.IsEnabled = isChecked;

        if (isChecked && app.AutoDisabledAt.HasValue)
        {
            app.ResetAutoDisableState();
        }
    }

    private Control CreateAppRow(AppEntry app)
    {
        var isDisabled = !app.IsEnabled;
        var isAutoDisabled = app.AutoDisabledAt.HasValue;

        var border = new Border
        {
            BorderBrush = isAutoDisabled ? new SolidColorBrush(Colors.OrangeRed) :
                          isDisabled ? Brushes.Gray : Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 8, 0, 8),
            Tag = app
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,*,*,Auto,60,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto")
        };

        // 行 0: 启用开关 + 名称 + 路径 + 参数 + 课程 + 提前 + 删除
        // 启用开关
        var toggle = new ToggleSwitch
        {
            IsChecked = app.IsEnabled,
            OnContent = "",
            OffContent = "",
            Tag = app,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 8, 0)
        };
        toggle.IsCheckedChanged += OnToggleSwitchChanged;
        Grid.SetColumn(toggle, 0);
        Grid.SetRow(toggle, 0);
        grid.Children.Add(toggle);

        // 名称
        var namePanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        namePanel.Children.Add(new TextBlock { Text = "软件名称", FontSize = 12, Foreground = Brushes.Gray });
        var nameBox = new TextBox { MinWidth = 100, Text = app.Name, Name = "NameBox", Tag = app };
        nameBox.PropertyChanged += OnTextBoxPropertyChanged;
        namePanel.Children.Add(nameBox);
        Grid.SetColumn(namePanel, 1);
        Grid.SetRow(namePanel, 0);
        grid.Children.Add(namePanel);

        // 路径
        var pathPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        pathPanel.Children.Add(new TextBlock { Text = "软件路径", FontSize = 12, Foreground = Brushes.Gray });
        var pathBox = new TextBox { MinWidth = 100, Text = app.Path, Name = "PathBox", Tag = app };
        pathBox.PropertyChanged += OnTextBoxPropertyChanged;
        pathPanel.Children.Add(pathBox);
        Grid.SetColumn(pathPanel, 2);
        Grid.SetRow(pathPanel, 0);
        grid.Children.Add(pathPanel);

        // 参数
        var argsPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        argsPanel.Children.Add(new TextBlock { Text = "启动参数", FontSize = 12, Foreground = Brushes.Gray });
        var isExe = IsPathExecutable(app.Path);
        var argsBox = new TextBox
        {
            MinWidth = 100,
            Text = app.Arguments,
            Name = "ArgsBox",
            Tag = app,
            IsEnabled = isExe,
            Watermark = isExe ? "示例: -w -h 1024" : "仅 .exe 文件支持参数"
        };
        argsBox.PropertyChanged += OnTextBoxPropertyChanged;
        argsPanel.Children.Add(argsBox);
        Grid.SetColumn(argsPanel, 3);
        Grid.SetRow(argsPanel, 0);
        grid.Children.Add(argsPanel);

        // 限定课程
        var coursePanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        coursePanel.Children.Add(new TextBlock { Text = "限定课程（逗号分隔）", FontSize = 12, Foreground = Brushes.Gray });
        var courseBox = new TextBox { MinWidth = 100, Text = app.CourseNames, Name = "CourseBox", Tag = app,
            Watermark = "留空=所有课程" };
        courseBox.PropertyChanged += OnTextBoxPropertyChanged;
        coursePanel.Children.Add(courseBox);
        Grid.SetColumn(coursePanel, 4);
        Grid.SetRow(coursePanel, 0);
        grid.Children.Add(coursePanel);

        // 提前提示
        var advancePanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        advancePanel.Children.Add(new TextBlock { Text = "提前(分钟)", FontSize = 12, Foreground = Brushes.Gray });
        var advanceBox = new TextBox { Width = 50, Text = app.AdvanceMinutes.ToString(), Name = "AdvanceBox", Tag = app,
            Watermark = "0" };
        advanceBox.PropertyChanged += OnTextBoxPropertyChanged;
        advancePanel.Children.Add(advanceBox);
        Grid.SetColumn(advancePanel, 5);
        Grid.SetRow(advancePanel, 0);
        grid.Children.Add(advancePanel);

        // 删除按钮
        var deleteBtn = new Button
        {
            Content = "删除",
            Tag = app,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
        };
        deleteBtn.Click += DeleteApp_Click;
        Grid.SetColumn(deleteBtn, 6);
        Grid.SetRow(deleteBtn, 0);
        grid.Children.Add(deleteBtn);

        // 行 1: 状态指示器（跨列）
        if (isDisabled)
        {
            var statusPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var statusDot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = isAutoDisabled ? new SolidColorBrush(Colors.OrangeRed) : new SolidColorBrush(Colors.Gray),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            statusPanel.Children.Add(statusDot);

            var statusText = new TextBlock
            {
                Text = app.StatusText,
                FontSize = 11,
                Foreground = isAutoDisabled ? new SolidColorBrush(Colors.OrangeRed) : Brushes.Gray,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            statusPanel.Children.Add(statusText);

            if (isAutoDisabled)
            {
                var resetBtn = new Button
                {
                    Content = "重置",
                    FontSize = 11,
                    Tag = app,
                    Padding = new Thickness(8, 2, 8, 2)
                };
                resetBtn.Click += ResetApp_Click;
                statusPanel.Children.Add(resetBtn);
            }

            Grid.SetColumn(statusPanel, 0);
            Grid.SetColumnSpan(statusPanel, 7);
            Grid.SetRow(statusPanel, 1);
            grid.Children.Add(statusPanel);
        }

        border.Child = grid;
        return border;
    }
}