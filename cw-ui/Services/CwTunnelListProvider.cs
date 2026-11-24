using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

using CnCNetClient.Online;

namespace CwUi.Services;

/// <summary>
/// 隧道列表来源（覆盖宿主 <see cref="ITunnelListProvider"/>）：
/// 官方 master-list → racw 镜像 → 插件内置名单，[CN] 优先；结果缓存 10 分钟。
/// 直接返回文本给宿主，不监听本地端口，因此不存在端口冲突与配置加载时序问题。
/// </summary>
public sealed class CwTunnelListProvider : ITunnelListProvider
{
    private const string Header = "address;country;countrycode;name;password;clients;maxclients;official;latitude;longitude;version;distance";
    private const string OfficialUrl = "https://cncnet.org/master-list";
    private const string MirrorUrl = "http://www.racw.top/master-list";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly CwEnvironment _env;
    private readonly object _sync = new();
    private string? _cached;
    private DateTime _cachedAt = DateTime.MinValue;

    public CwTunnelListProvider(CwEnvironment env) => _env = env;

    public string GetTunnelList()
    {
        lock (_sync)
        {
            if (_cached is not null && DateTime.UtcNow - _cachedAt < CacheDuration)
                return _cached;

            string? text = null;
            foreach (string url in new[] { OfficialUrl, MirrorUrl })
            {
                try
                {
                    string fetched = Http.GetStringAsync(url).GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(fetched))
                    {
                        text = fetched;
                        break;
                    }
                }
                catch
                {
                    // 换下一个来源
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                string fallback = _env.PluginPath("Assets", "tunnel_fallback.txt");
                if (File.Exists(fallback))
                    text = File.ReadAllText(fallback);
            }

            _cached = NormalizeAndSort(text ?? string.Empty);
            _cachedAt = DateTime.UtcNow;
            return _cached;
        }
    }

    /// <summary>补表头、[CN] 优先、按地址去重（旧客户端 TunnelHandler 的排序语义）。</summary>
    private static string NormalizeAndSort(string text)
    {
        List<string> lines = text
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        // 镜像文件没有表头，宿主解析时会无条件跳过首行，这里统一补上
        if (lines.Count > 0 && lines[0].StartsWith("address;", StringComparison.OrdinalIgnoreCase))
            lines.RemoveAt(0);

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> cn = new();
        List<string> others = new();

        foreach (string line in lines)
        {
            string[] parts = line.Split(';');
            if (parts.Length < 3 || !seen.Add(parts[0]))
                continue;

            bool isCn = parts[2].Equals("CN", StringComparison.OrdinalIgnoreCase)
                        || parts.Length > 3 && parts[3].StartsWith("[CN]", StringComparison.OrdinalIgnoreCase);

            (isCn ? cn : others).Add(line);
        }

        StringBuilder builder = new();
        builder.Append(Header).Append('\n');
        foreach (string line in cn.Concat(others))
            builder.Append(line).Append('\n');
        return builder.ToString();
    }
}
