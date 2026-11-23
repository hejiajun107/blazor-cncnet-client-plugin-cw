using System.Text.Json;

namespace CwUi.Services;

public sealed record CwUpdateCheckResult(
    bool NeedDownload,
    string NextVersion,
    string DownloadUrl1,
    string DownloadUrl2);

/// <summary>
/// 旧客户端的“大版本更新”判断：调用 racw.top NeedDownloadNew，
/// 返回 NeedDownload=true 时不进入在线更新，而是提示手动下载。
/// </summary>
public sealed class CwUpdateChecker
{
    private const string DefaultEndpoint = "http://www.racw.top/updates/NeedDownloadNew";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CwEnvironment _env;

    public CwUpdateChecker(CwEnvironment env) => _env = env;

    /// <summary>当前用于查询的版本号（null = 未配置）。</summary>
    public string? LocalVersion { get; private set; }

    /// <summary>
    /// 插件 plugin.ini [Update] CheckOnStartup 决定是否做「大版本」判定。
    /// 用户级 [Options] CheckforUpdates 由宿主在询问启动闸门前判断，这里不再重复
    /// （宿主读的是游戏根 ClientSettings.ini，与插件此前猜测的 ClientApp 路径不一致）。
    /// </summary>
    public bool IsCheckEnabled()
    {
        IniDocument pluginIni = IniDocument.Load(_env.PluginPath("plugin.ini"));
        return pluginIni.GetBool("Update", "CheckOnStartup", true);
    }

    /// <summary>plugin.ini [Update] Version 优先，其次游戏根 version 文件 [DTA] Version。</summary>
    public string? ResolveVersion()
    {
        IniDocument pluginIni = IniDocument.Load(_env.PluginPath("plugin.ini"));
        string? version = pluginIni.Get("Update", "Version");
        if (!string.IsNullOrWhiteSpace(version))
            return version.Trim();

        IniDocument versionFile = IniDocument.Load(_env.GamePath("version"));
        version = versionFile.Get("DTA", "Version");
        return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    public async Task<CwUpdateCheckResult?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsCheckEnabled())
            return null;

        LocalVersion = ResolveVersion();
        if (string.IsNullOrWhiteSpace(LocalVersion))
            return null;

        IniDocument pluginIni = IniDocument.Load(_env.PluginPath("plugin.ini"));
        string endpoint = pluginIni.Get("Update", "Endpoint") ?? DefaultEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = DefaultEndpoint;

        string separator = endpoint.Contains('?') ? "&" : "?";
        string url = $"{endpoint}{separator}version={Uri.EscapeDataString(LocalVersion)}";

        using HttpResponseMessage response = await Http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        UpdateDto? dto = await JsonSerializer.DeserializeAsync<UpdateDto>(stream, JsonOptions, cancellationToken);
        if (dto is null)
            return null;

        return new CwUpdateCheckResult(
            dto.NeedDownload,
            dto.NextVersion ?? string.Empty,
            dto.DownloadUrl1 ?? string.Empty,
            dto.DownloadUrl2 ?? string.Empty);
    }

    private sealed class UpdateDto
    {
        public bool NeedDownload { get; set; }

        public string? NextVersion { get; set; }

        public string? DownloadUrl1 { get; set; }

        public string? DownloadUrl2 { get; set; }
    }
}
