using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ClassLauncher.Models;
using ClassLauncher.Services;
using ClassLauncher.Views.SettingsPages;

namespace ClassLauncher;

[PluginEntrance]
public class Plugin : PluginBase
{
    public Settings Settings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Settings = ConfigureFileHelper.LoadConfig<Settings>(
            Path.Combine(PluginConfigFolder, "Settings.json"));

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
        ConfigureFileHelper.SaveConfig(
            Path.Combine(PluginConfigFolder, "Settings.json"), Settings);
    }
}