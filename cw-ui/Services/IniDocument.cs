using System.Text;

namespace CwUi.Services;

/// <summary>
/// 极简 INI 读写：按行保留原文件内容，仅更新目标键，避免覆盖游戏/客户端写入的其它配置。
/// </summary>
internal sealed class IniDocument
{
    private readonly List<string> _lines;

    private IniDocument(List<string> lines) => _lines = lines;

    public static IniDocument Load(string path)
    {
        if (!File.Exists(path))
            return new IniDocument(new List<string>());

        return new IniDocument(new List<string>(File.ReadAllLines(path)));
    }

    public string? Get(string section, string key)
    {
        int start = FindSection(section);
        if (start < 0)
            return null;

        for (int i = start + 1; i < _lines.Count; i++)
        {
            if (IsSectionHeader(_lines[i]))
                break;

            string trimmed = _lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            int eq = trimmed.IndexOf('=');
            if (eq <= 0)
                continue;

            if (!trimmed[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            return StripComment(trimmed[(eq + 1)..].Trim());
        }

        return null;
    }

    public bool GetBool(string section, string key, bool fallback)
    {
        string? value = Get(section, key);
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback
        };
    }

    public int GetInt(string section, string key, int fallback)
        => int.TryParse(Get(section, key), out int value) ? value : fallback;

    public void Set(string section, string key, string value)
    {
        int start = FindSection(section);
        if (start < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Trim().Length > 0)
                _lines.Add(string.Empty);

            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }

        int lastContent = start;
        for (int i = start + 1; i < _lines.Count; i++)
        {
            if (IsSectionHeader(_lines[i]))
                break;

            lastContent = i;

            string trimmed = _lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            int eq = trimmed.IndexOf('=');
            if (eq <= 0)
                continue;

            if (trimmed[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                _lines[i] = $"{key}={value}";
                return;
            }
        }

        _lines.Insert(lastContent + 1, $"{key}={value}");
    }

    public void Save(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllLines(path, _lines, new UTF8Encoding(false));
    }

    private int FindSection(string section)
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            string trimmed = _lines[i].Trim();
            if (trimmed.Length < 3 || trimmed[0] != '[' || trimmed[^1] != ']')
                continue;

            if (trimmed[1..^1].Trim().Equals(section, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static bool IsSectionHeader(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']';
    }

    private static string StripComment(string value)
    {
        int idx = value.IndexOf(';');
        return idx < 0 ? value : value[..idx].TrimEnd();
    }
}
