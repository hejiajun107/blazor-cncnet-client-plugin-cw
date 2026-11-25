using CnCNetClient.Tools;

namespace CwUi.Services;

/// <summary>
/// spawnmap.ini 补丁：按 <c>Assets/event_spawnmap.ini</c> 的日期区间与条件写入节日限定代码
/// （对齐旧客户端 GameLobbyBase.WriteSpawnMapIni 的国庆活动逻辑），日期与键值只改 ini、无需重编译。
/// </summary>
public sealed class CwSpawnMapPatcher : ISpawnMapPatcher
{
    private readonly CwEnvironment _env;

    public CwSpawnMapPatcher(CwEnvironment env) => _env = env;

    public void Patch(IniFile spawnMapIni, SpawnMapPatchContext context)
    {
        string configPath = _env.PluginPath("Assets", "event_spawnmap.ini");
        if (!File.Exists(configPath))
            return;

        IniFile config = new IniFile(configPath);

        if (config.GetBooleanValue("Event", "RequireAI", true) && context.AiPlayerCount <= 0)
            return;

        string start = config.GetStringValue("Event", "Start", string.Empty);
        string end = config.GetStringValue("Event", "End", string.Empty);
        if (!InWindow(start, end, DateTime.Now))
            return;

        List<string>? keys = config.GetSectionKeys("Patches");
        if (keys is null)
            return;

        foreach (string key in keys)
        {
            // 形如 V5.Prerequisite=V5SP：点号前为 spawnmap.ini 的节，后面为键
            int dot = key.IndexOf('.');
            if (dot <= 0 || dot == key.Length - 1)
                continue;

            string section = key[..dot];
            string iniKey = key[(dot + 1)..];
            string value = config.GetStringValue("Patches", key, string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
                spawnMapIni.SetStringValue(section, iniKey, value);
        }
    }

    /// <summary>判断今天是否落在 [Start, End] 的 MM-DD 区间内（支持跨年，如 12-28 ~ 01-05）。</summary>
    private static bool InWindow(string? startText, string? endText, DateTime today)
    {
        if (!TryParseMonthDay(startText, out int start) || !TryParseMonthDay(endText, out int end))
            return false;

        int now = today.Month * 100 + today.Day;
        return start <= end ? now >= start && now <= end : now >= start || now <= end;
    }

    private static bool TryParseMonthDay(string? text, out int monthDay)
    {
        monthDay = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts = text.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out int month)
            || !int.TryParse(parts[1], out int day)
            || month < 1 || month > 12
            || day < 1 || day > 31)
        {
            return false;
        }

        monthDay = month * 100 + day;
        return true;
    }
}
