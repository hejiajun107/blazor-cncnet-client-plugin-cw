namespace CwUi.Services;

/// <summary>插件运行时路径信息（由宿主在 Build 前注入）。</summary>
public sealed class CwEnvironment
{
    public CwEnvironment(string gameRoot, string pluginDirectory)
    {
        GameRoot = gameRoot;
        PluginDirectory = pluginDirectory;
    }

    public string GameRoot { get; }

    public string PluginDirectory { get; }

    /// <summary>插件内文件 → 客户端可加载的 http URL（由宿主 ContentUrl 注入）。</summary>
    public Func<string, string> AssetUrl { get; set; } = _ => string.Empty;

    public string GamePath(params string[] parts) => Combine(GameRoot, parts);

    public string PluginPath(params string[] parts) => Combine(PluginDirectory, parts);

    private static string Combine(string root, string[] parts)
        => parts.Length == 0 ? root : Path.Combine(new[] { root }.Concat(parts).ToArray());
}
