using CnCNetClient.Plugin.Abstractions;
using CwUi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CwUi;

/// <summary>
/// CW UI 插件入口：银白科技风主题（plugin.css）、ReShade 画质设置页、
/// 主菜单小贴士与旧客户端的大版本更新提示。
/// </summary>
public sealed class CwUiPluginEntry : ICncNetPlugin
{
    public string Id => "cw-ui";

    public string Name => "CW UI";

    /// <summary>必须与宿主 <see cref="PluginApi.CurrentVersion"/> 一致，否则宿主禁用本插件。</summary>
    public int ApiVersion => PluginApi.CurrentVersion;

    public void RegisterServices(IPluginContext context)
    {
        CwEnvironment env = new(context.GameRoot, context.PluginDirectory);
        context.Services.AddSingleton(env);
        context.Services.AddSingleton<CwReShadeService>();
        context.Services.AddSingleton<CwTipService>();
        context.Services.AddSingleton<CwRandomMapService>();
        context.Services.AddSingleton<CwUpdateChecker>();
        context.Services.AddSingleton<CwStartupUpdateGate>();
        // 宿主启动检查前询问：大版本时返回 false，跳过在线更新（旧客户端语义）
        context.Services.AddSingleton<IStartupUpdateGate>(sp => sp.GetRequiredService<CwStartupUpdateGate>());
    }

    public void RegisterUi(IPluginUiRegistrar ui)
    {
        // 本程序集内的 @page 路由交给宿主 Router 发现（/plugin/cw-ui/reshade）
        ui.AddPageAssembly(typeof(CwUiPluginEntry).Assembly);

        // 追加型槽位：设置页左侧栏入口
        ui.ContributeSlot("page.settings.aside", typeof(Components.CwSettingsAside));

        // 追加型槽位：战役页任务列表（按阵营分组，替换内置扁平列表）
        ui.ContributeSlot("page.campaign.aside", typeof(Components.CwCampaignGroups));

        // 追加型槽位：全局覆盖层宿主（小贴士 + 版本提示，DOM 由 cw-overlay.js 挂到 body）
        ui.ContributeSlot("layout.topbar", typeof(Components.CwOverlayHost));

        // 替换型槽位：主菜单列表外观（条目数据与点击仍由宿主提供，旧客户端玻璃贴片风）
        ui.ReplaceSlot("page.menu.list", typeof(Components.CwMainMenu));

        // 启动闪屏品牌：CW 改名（ClientDefinitions.ini [LoadingScreen] 未生效时的可靠覆盖）
        ui.SetLoadingScreenContent(new LoadingScreenContent
        {
            BrandTitle = "Century War",
            BrandSubtitle = "COMMAND & CONQUER"
        });

        // 大厅地图工具条：生成按钮 + 弹窗（宿主槽位位于 GameLobby.razor 的 .map-tools 内）
        ui.ContributeSlot("page.skirmish.map.tools", typeof(Components.CwMapTools));
    }
}
