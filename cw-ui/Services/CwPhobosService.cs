namespace CwUi.Services;

/// <summary>旧客户端“游戏”面板的 Phobos 增强选项。</summary>
public sealed class CwPhobosSettings
{
    public bool ShowBuildingPlacementPreview { get; set; } = true;

    public bool DigitalDisplay { get; set; } = true;

    public bool SuperWeaponSidebar { get; set; } = true;

    public bool ImprovedLabelDisplay { get; set; } = true;
}

/// <summary>
/// 读写 Phobos 的游戏内选项。Phobos 从游戏自身 INI（YR 为 RA2MD.ini）读取 [Phobos]，
/// 而不是客户端设置文件，因此这里按 ClientDefinitions [Settings] SettingsFile 定位目标文件。
/// </summary>
public sealed class CwPhobosService
{
    private const string Section = "Phobos";

    private readonly CwEnvironment _env;

    public CwPhobosService(CwEnvironment env) => _env = env;

    public string SettingsPath
    {
        get
        {
            string definitions = _env.GamePath("Resources", "ClientDefinitions.ini");
            string? name = IniDocument.Load(definitions).Get("Settings", "SettingsFile");

            return _env.GamePath(string.IsNullOrWhiteSpace(name) ? "RA2MD.ini" : name.Trim());
        }
    }

    public CwPhobosSettings Load()
    {
        IniDocument ini = IniDocument.Load(SettingsPath);

        return new CwPhobosSettings
        {
            ShowBuildingPlacementPreview = ini.GetBool(Section, "ShowBuildingPlacementPreview", true),
            DigitalDisplay = ini.GetBool(Section, "DigitalDisplay.Enable", true),
            SuperWeaponSidebar = ini.GetBool(Section, "Client.UserSuperWeaponSidebar", true),
            ImprovedLabelDisplay = ini.GetBool(Section, "Client.UserImprovedLabelDiplaying", true)
        };
    }

    /// <summary>写入 [Phobos]；侧栏门槛与任务消息的联动键沿用旧客户端保存规则。</summary>
    public void Save(CwPhobosSettings settings)
    {
        IniDocument ini = IniDocument.Load(SettingsPath);

        ini.Set(Section, "ShowBuildingPlacementPreview", Bool(settings.ShowBuildingPlacementPreview));
        ini.Set(Section, "DigitalDisplay.Enable", Bool(settings.DigitalDisplay));

        ini.Set(Section, "Client.UserSuperWeaponSidebar", Bool(settings.SuperWeaponSidebar));
        ini.Set(Section, "SuperWeaponSidebar.RequiredSignificance", settings.SuperWeaponSidebar ? "10" : "20");

        ini.Set(Section, "Client.UserImprovedLabelDiplaying", Bool(settings.ImprovedLabelDisplay));
        ini.Set(Section, "MessageApplyHoverState", "True");
        ini.Set(Section, "MessageDisplayInCenter", Bool(settings.ImprovedLabelDisplay));

        if (settings.ImprovedLabelDisplay)
        {
            ini.Set(Section, "MessageDisplayInCenter.LabelsCount", "2");
            ini.Set(Section, "MessageDisplayInCenter.RecordsCount", "16");
        }

        ini.Save(SettingsPath);
    }

    private static string Bool(bool value) => value ? "True" : "False";
}
