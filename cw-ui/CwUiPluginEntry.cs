using CnCNetClient.Plugin.Abstractions;

namespace CwUi;

/// <summary>
/// CW UI 插件入口：新增独立页面 <c>/plugin/cw-ui/home</c>、
/// 主菜单入口，以及设置页侧栏卡片（追加型槽位）。
/// </summary>
public sealed class CwUiPluginEntry : ICncNetPlugin
{
    public string Id => "cw-ui";

    public string Name => "CW UI";

    /// <summary>必须与宿主 <see cref="PluginApi.CurrentVersion"/> 一致，否则宿主禁用本插件。</summary>
    public int ApiVersion => PluginApi.CurrentVersion;

    public void RegisterServices(IPluginContext context)
    {
    }

    public void RegisterUi(IPluginUiRegistrar ui)
    {
        // 本程序集内的 @page 路由交给宿主 Router 发现
        ui.AddPageAssembly(typeof(CwUiPluginEntry).Assembly);

        // 追加型槽位：设置页左侧栏
        ui.ContributeSlot("page.settings.aside", typeof(Components.CwSettingsAside));

        // 主菜单入口
        ui.AddMenuEntry(new PluginMenuEntry(
            Key: "cw-ui.home",
            Label: "CW 界面",
            LabelEn: "CW UI",
            Icon: "◆",
            Route: "/plugin/cw-ui/home"));
    }
}
