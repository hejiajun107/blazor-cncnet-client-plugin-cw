using CnCNetClient.Domain.Multiplayer.CnCNet;
using CnCNetClient.Tools;

namespace CwUi.Services;

/// <summary>
/// 昵称标准化：把白名单外字符替换为 '-'（对齐旧客户端 eca835c），
/// 避免输入法标点或全角字符导致连接被拒；不改名的 MOD 不注册本实现即可。
/// </summary>
public sealed class CwPlayerNameProcessor : IPlayerNameProcessor
{
    public string Process(string name) => NameValidator.SanitizePlayerName(name);
}
