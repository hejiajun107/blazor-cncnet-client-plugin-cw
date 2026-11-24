using System.Collections.Generic;

using CnCNetClient.Online;

namespace CwUi.Services;

/// <summary>
/// IRC 服务器列表选择器实现（覆盖宿主默认实现）：
/// 节点 0 = 国内老客户端节点 irc.racw.top；节点 1 = 国际，原样使用宿主配置（GameSurge）。
/// </summary>
public sealed class CwIrcServerListProvider : IIrcServerListProvider
{
    private static readonly string[] DomesticServers =
    [
        "irc.racw.top|cw,cn|5557",
    ];

    private readonly CwIrcNodeSetting _nodeSetting;

    public CwIrcServerListProvider(CwIrcNodeSetting nodeSetting) => _nodeSetting = nodeSetting;

    public IReadOnlyList<string> SelectServers(IReadOnlyList<string> configuredServers)
        => _nodeSetting.Node == 0 ? DomesticServers : configuredServers;
}
