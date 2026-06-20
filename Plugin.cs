using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClassLauncher.Models;
using ClassLauncher.Services;
using ClassLauncher.Views.SettingsPages;

namespace ClassLauncher;

[PluginEntrance]
public class Plugin : PluginBase
{
    private ILogger<Plugin>? _logger;

    public Settings Settings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        _logger = context.Configuration.GetSection("Logging").Get<ILogger<Plugin>>();

        try
        {
            Settings = ConfigureFileHelper.LoadConfig<Settings>(
                Path.Combine(PluginConfigFolder, "Settings.json"));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "加载设置文件失败，使用默认设置");
            Settings = new Settings();
        }

        // 监听集合变更：添加/移除条目时，正确管理 PropertyChanged 订阅
        Settings.Apps.CollectionChanged += OnAppsCollectionChanged;

        // 初始化时为已有条目订阅
        foreach (var app in Settings.Apps)
        {
            app.PropertyChanged += OnAppPropertyChanged;
        }

        services.AddSettingsPage<SettingsPage>();
        services.AddHostedService<AppLaunchService>();
    }

    private void OnAppsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (AppEntry app in e.NewItems)
                app.PropertyChanged += OnAppPropertyChanged;
        }
        if (e.OldItems != null)
        {
            foreach (AppEntry app in e.OldItems)
                app.PropertyChanged -= OnAppPropertyChanged;
        }
        SaveSettings();
    }

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            ConfigureFileHelper.SaveConfig(
                Path.Combine(PluginConfigFolder, "Settings.json"), Settings);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存设置文件失败");
        }
    }
}