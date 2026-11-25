namespace CwUi.Services;

/// <summary>主菜单小贴士（MainMenu.ini [lblHintText]）。</summary>
public sealed class CwTipContent
{
    public CwTipContent(IReadOnlyList<string> items, int intervalMs)
    {
        Items = items;
        IntervalMs = intervalMs;
    }

    public IReadOnlyList<string> Items { get; }

    public int IntervalMs { get; }

    public bool HasItems => Items.Count > 0;
}

public sealed class CwTipService
{
    private readonly CwEnvironment _env;

    public CwTipService(CwEnvironment env) => _env = env;

    public CwTipContent Load()
    {
        IniDocument ini = IniDocument.Load(_env.GamePath("Resources", "MainMenu.ini"));

        bool visible = ini.GetBool("lblHintText", "Visible", true);
        int interval = ini.GetInt("lblHintText", "Interval", 5000);
        string raw = ini.Get("lblHintText", "TextList") ?? string.Empty;

        List<string> items = raw
            .Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(text => text.Length > 0)
            .ToList();

        // 贴士按序号查表（Plugin:CwUi:Tip.N），未收录时保持原文
        for (int i = 0; i < items.Count; i++)
            items[i] = CwText.T("Tip." + (i + 1), items[i]);

        return new CwTipContent(visible ? items : new List<string>(), interval <= 0 ? 5000 : interval);
    }
}
