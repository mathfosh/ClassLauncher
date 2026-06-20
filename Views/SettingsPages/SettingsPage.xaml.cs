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
        if (source.Parent is not Panel parentPanel)
            return null;
        var grid = parentPanel.Parent as Grid;
        if (grid == null)
        {
            // source 可能被 StackPanel 包裹，再上一层是 Grid
            grid = parentPanel.Parent as Grid;
        }
        // 从 source 的 StackPanel 找到 Grid
        var sp = source.Parent as StackPanel;
        grid = sp?.Parent as Grid;
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

    private Control CreateAppRow(AppEntry app)
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 8, 0, 8),
            Tag = app
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,Auto,60,Auto"),
            RowDefinitions = new RowDefinitions("Auto")
        };

        // 名称
        var namePanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        namePanel.Children.Add(new TextBlock { Text = "软件名称", FontSize = 12, Foreground = Brushes.Gray });
        var nameBox = new TextBox { MinWidth = 100, Text = app.Name, Name = "NameBox", Tag = app };
        nameBox.PropertyChanged += OnTextBoxPropertyChanged;
        namePanel.Children.Add(nameBox);
        Grid.SetColumn(namePanel, 0);
        grid.Children.Add(namePanel);

        // 路径
        var pathPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        pathPanel.Children.Add(new TextBlock { Text = "软件路径", FontSize = 12, Foreground = Brushes.Gray });
        var pathBox = new TextBox { MinWidth = 100, Text = app.Path, Name = "PathBox", Tag = app };
        pathBox.PropertyChanged += OnTextBoxPropertyChanged;
        pathPanel.Children.Add(pathBox);
        Grid.SetColumn(pathPanel, 1);
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
        Grid.SetColumn(argsPanel, 2);
        grid.Children.Add(argsPanel);

        // 限定课程
        var coursePanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        coursePanel.Children.Add(new TextBlock { Text = "限定课程（逗号分隔）", FontSize = 12, Foreground = Brushes.Gray });
        var courseBox = new TextBox { MinWidth = 100, Text = app.CourseNames, Name = "CourseBox", Tag = app,
            Watermark = "留空=所有课程" };
        courseBox.PropertyChanged += OnTextBoxPropertyChanged;
        coursePanel.Children.Add(courseBox);
        Grid.SetColumn(coursePanel, 3);
        grid.Children.Add(coursePanel);

        // 提前提示
        var advancePanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
        advancePanel.Children.Add(new TextBlock { Text = "提前(分钟)", FontSize = 12, Foreground = Brushes.Gray });
        var advanceBox = new TextBox { Width = 50, Text = app.AdvanceMinutes.ToString(), Name = "AdvanceBox", Tag = app,
            Watermark = "0" };
        advanceBox.PropertyChanged += OnTextBoxPropertyChanged;
        advancePanel.Children.Add(advanceBox);
        Grid.SetColumn(advancePanel, 4);
        grid.Children.Add(advancePanel);

        // 删除按钮
        var deleteBtn = new Button
        {
            Content = "删除",
            Tag = app,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
        };
        deleteBtn.Click += DeleteApp_Click;
        Grid.SetColumn(deleteBtn, 5);
        grid.Children.Add(deleteBtn);

        border.Child = grid;
        return border;
    }
}