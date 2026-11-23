using System.Globalization;

namespace CwUi.Services;

/// <summary>旧客户端 ReShade 选项（ClientSettings.ini [RESHADE]）。</summary>
public sealed class CwReShadeSettings
{
    public bool ChangeDayAndNight { get; set; }

    public bool WaterImprovement { get; set; }

    public bool CloudMap { get; set; } = true;

    public bool MagicBloom { get; set; } = true;

    public bool HDR { get; set; }

    public bool Clarity { get; set; }

    public bool Colorful { get; set; }

    public bool Darkness { get; set; }

    /// <summary>0 无 / 1 低 / 2 中 / 3 高(推荐) / 4 超高。</summary>
    public int BloomLevel { get; set; } = 3;
}

/// <summary>
/// 读取/保存 ReShade 设置，并按旧客户端 ReshadeOptionPanel 的规则重新生成
/// 游戏根 DefaultPreset.ini（模板随插件分发：Assets/ReshadePreset.ini）。
/// </summary>
public sealed class CwReShadeService
{
    private const string Section = "RESHADE";
    private const string PresetFileName = "DefaultPreset.ini";

    private readonly CwEnvironment _env;

    public CwReShadeService(CwEnvironment env) => _env = env;

    public string SettingsPath => _env.GamePath("ClientSettings.ini");

    public string TemplatePath => _env.PluginPath("Assets", "ReshadePreset.ini");

    public string PresetPath => _env.GamePath(PresetFileName);

    public CwReShadeSettings Load()
    {
        IniDocument ini = IniDocument.Load(SettingsPath);
        return new CwReShadeSettings
        {
            ChangeDayAndNight = ini.GetBool(Section, "ChangeDayAndNightEnable", false),
            WaterImprovement = ini.GetBool(Section, "WaterImprovementEnable", false),
            CloudMap = ini.GetBool(Section, "CloudMapEnable", true),
            MagicBloom = ini.GetBool(Section, "MagicBloomEnable", true),
            HDR = ini.GetBool(Section, "HDREnable", false),
            Clarity = ini.GetBool(Section, "ClarityEnable", false),
            Colorful = ini.GetBool(Section, "ColorfulEnable", false),
            Darkness = ini.GetBool(Section, "DarknessEnable", false),
            BloomLevel = Math.Clamp(ini.GetInt(Section, "BloomLevel", 3), 0, 4)
        };
    }

    /// <summary>保存设置并重建预设；模板缺失时抛出 <see cref="FileNotFoundException"/>。</summary>
    public void Save(CwReShadeSettings settings)
    {
        IniDocument ini = IniDocument.Load(SettingsPath);
        ini.Set(Section, "ChangeDayAndNightEnable", Bool(settings.ChangeDayAndNight));
        ini.Set(Section, "WaterImprovementEnable", Bool(settings.WaterImprovement));
        ini.Set(Section, "CloudMapEnable", Bool(settings.CloudMap));
        ini.Set(Section, "MagicBloomEnable", Bool(settings.MagicBloom));
        ini.Set(Section, "HDREnable", Bool(settings.HDR));
        ini.Set(Section, "ClarityEnable", Bool(settings.Clarity));
        ini.Set(Section, "ColorfulEnable", Bool(settings.Colorful));
        ini.Set(Section, "DarknessEnable", Bool(settings.Darkness));
        ini.Set(Section, "BloomLevel", settings.BloomLevel.ToString(CultureInfo.InvariantCulture));
        ini.Save(SettingsPath);

        WritePreset(settings);
    }

    private void WritePreset(CwReShadeSettings settings)
    {
        if (!File.Exists(TemplatePath))
            throw new FileNotFoundException($"缺少 ReShade 预设模板：{TemplatePath}");

        string preset = File.ReadAllText(TemplatePath);

        List<string> techniques = new();

        if (settings.BloomLevel >= 4)
            techniques.Add("MagicBloom");

        if (settings.CloudMap)
            techniques.Add("CloudMapMag");

        techniques.Add("BlitLaser");
        techniques.Add("IndexedNeoBloom");

        if (settings.Clarity)
            techniques.Add("Clarity");

        if (settings.Colorful)
            techniques.Add("Colourfulness");

        if (settings.HDR)
            techniques.Add("HDREnable");

        if (settings.WaterImprovement)
            techniques.Add("ShowWaterDepthMap");

        if (settings.Darkness)
        {
            techniques.Add("FXAA");
            techniques.Add("LiftGammaGain");
            techniques.Add("Vignette");
        }

        preset = preset.Replace("<Techniques>", string.Join(",", techniques));

        preset = preset.Replace(
            "<NeoBloomIndexedFilter:night_coeff>",
            settings.ChangeDayAndNight ? "0.709999,0.810000,1.190000,0.750000" : "1.000000,1.000000,1.000000,1.000000");
        preset = preset.Replace(
            "<NeoBloomIndexedFilter:start_time>",
            settings.ChangeDayAndNight ? "0.000000" : "1.000000");
        preset = preset.Replace(
            "<NeoBloomIndexedFilter:time_speed>",
            settings.ChangeDayAndNight ? "45000.000000" : "1.000000");

        preset = preset.Replace(
            "<show_water_depth:game_obj_cover_judge>",
            settings.WaterImprovement ? "0.366999" : "0.000000");

        preset = preset.Replace(
            "<NeoBloomIndexedFilter:normalize_brightness>",
            settings.BloomLevel <= 0 ? "1" : "0");
        preset = preset.Replace(
            "<NeoBloomIndexedFilter:intensity>",
            (0.266000M * settings.BloomLevel / 3).ToString(CultureInfo.InvariantCulture).PadRight(8, '0'));

        File.WriteAllText(PresetPath, preset);
    }

    private static string Bool(bool value) => value ? "True" : "False";
}
