using CnCNetClient.Plugin.Abstractions;

namespace CwUi.Services;

/// <summary>
/// 宿主启动在线更新前的闸门（对齐旧客户端 MainMenu.CheckForUpdates）：
/// 命中 racw 的 NeedDownload=true 时返回 false，跳过宿主在线更新，由覆盖层提示手动下载；
/// 否则返回 true，宿主照常执行在线更新。判定结果缓存，避免与覆盖层重复请求。
/// </summary>
public sealed class CwStartupUpdateGate : IStartupUpdateGate
{
    private readonly CwUpdateChecker _checker;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CwUpdateCheckResult? _result;
    private bool _checked;

    public CwStartupUpdateGate(CwUpdateChecker checker) => _checker = checker;

    /// <summary>已缓存的大版本判定结果（未启用检查或未查询成功时为 null）。</summary>
    public CwUpdateCheckResult? Result => _result;

    /// <summary>NeedDownload=true → false（跳过在线更新）；其余（含未启用检查）→ true。</summary>
    public async Task<bool> ShouldRunOnlineUpdateAsync(CancellationToken cancellationToken = default)
    {
        CwUpdateCheckResult? result = await EnsureCheckedAsync(cancellationToken);
        return result?.NeedDownload != true;
    }

    /// <summary>
    /// 执行一次大版本判定并缓存。查询异常向上抛出：宿主按旧客户端语义跳过在线更新，
    /// 覆盖层捕获后静默跳过提示。
    /// </summary>
    public async Task<CwUpdateCheckResult?> EnsureCheckedAsync(CancellationToken cancellationToken = default)
    {
        if (_checked)
            return _result;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_checked)
                return _result;

            _result = await _checker.CheckAsync(cancellationToken);
            _checked = true;
            return _result;
        }
        finally
        {
            _lock.Release();
        }
    }
}
