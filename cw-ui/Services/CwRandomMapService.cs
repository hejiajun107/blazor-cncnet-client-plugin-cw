using System.Diagnostics;
using System.Globalization;
using System.Text;

using CnCNetClient.Domain.Multiplayer;

namespace CwUi.Services;

/// <summary>随机地图生成参数（对应旧客户端 RandomMapOptionWindow：最大玩家数 / 地形 / 大小 / 战损）。</summary>
public sealed record CwRandomMapOptions(int MaxPlayers, string Type, int Size, int Damage);

/// <summary>生成结果：成功时给出写入 Maps/Custom 的文件名。</summary>
public sealed record CwRandomMapResult(bool Success, string Message, string? FileName = null);

/// <summary>
/// 随机地图生成（移植自旧客户端 GameLobbyBase.PickRandomMap）：
/// 调用 Resources/RandomMapGenerator/RandomMapGenerator.exe，读取其 Output/随机地图.yrm，
/// 复制为 Maps/Custom/随机地图N.map，然后重新加载地图列表。
/// </summary>
public sealed class CwRandomMapService
{
    /// <summary>旧客户端限制：最多保留 10 张随机地图。</summary>
    private const int MaxGeneratedMaps = 10;

    private readonly CwEnvironment _env;
    private readonly MapLoader _maps;

    /// <summary>同一时刻只允许一个生成任务（生成器输出文件名固定，并发会互相覆盖）。</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CwRandomMapService(CwEnvironment env, MapLoader maps)
    {
        _env = env;
        _maps = maps;
    }

    private string GeneratorDirectory => _env.GamePath("Resources", "RandomMapGenerator");

    private string CustomMapsDirectory => _env.GamePath("Maps", "Custom");

    /// <summary>已生成的随机地图（按编号排序），用于页面列表展示。</summary>
    public IReadOnlyList<string> ListGenerated()
        => Enumerable.Range(1, MaxGeneratedMaps)
            .Select(i => $"随机地图{i}.map")
            .Where(name => File.Exists(Path.Combine(CustomMapsDirectory, name)))
            .ToArray();

    /// <summary>生成一张随机地图并写入 Maps/Custom；完成后刷新宿主地图列表。</summary>
    public async Task<CwRandomMapResult> GenerateAsync(CwRandomMapOptions options, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct))
            return new CwRandomMapResult(false, "正在生成中，请稍候…");

        try
        {
            string exe = Path.Combine(GeneratorDirectory, "RandomMapGenerator.exe");
            if (!File.Exists(exe))
                return new CwRandomMapResult(false, "未找到随机地图生成器（Resources/RandomMapGenerator/RandomMapGenerator.exe）");

            string output = Path.Combine(GeneratorDirectory, "Output", "随机地图.yrm");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            if (File.Exists(output))
                File.Delete(output);

            string args = BuildArgs(options);
            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = GeneratorDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using Process process = new() { StartInfo = psi };
            process.Start();
            // 生成器会输出进度，必须持续读取，否则管道写满会阻塞子进程
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> stderr = process.StandardError.ReadToEndAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(4));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return new CwRandomMapResult(false, ct.IsCancellationRequested ? "已取消生成" : "生成超时（超过 4 分钟）");
            }

            string log = (await stdout).Trim();
            if (log.Length == 0)
                log = (await stderr).Trim();

            if (process.ExitCode != 0 || !File.Exists(output))
                return new CwRandomMapResult(false,
                    $"生成失败（退出码 {process.ExitCode}）{(log.Length > 0 ? "：" + LastLine(log) : string.Empty)}");

            Directory.CreateDirectory(CustomMapsDirectory);
            for (int i = 1; i <= MaxGeneratedMaps; i++)
            {
                string fileName = $"随机地图{i}.map";
                string dest = Path.Combine(CustomMapsDirectory, fileName);
                if (File.Exists(dest))
                    continue;

                File.Copy(output, dest, true);
                // 宿主 MapFileWatcher 监听 Maps/Custom，新文件会自动加入内存地图集合；
                // LoadMapsAsync 仅在首次加载时真正执行，这里作为兜底（已加载时是空操作）。
                await _maps.LoadMapsAsync();
                return new CwRandomMapResult(true, $"已生成 {fileName}", fileName);
            }

            return new CwRandomMapResult(false, "随机地图数量过多，请先删除原有的随机地图（遭遇战地图列表右键删除）");
        }
        catch (Exception ex)
        {
            return new CwRandomMapResult(false, "生成异常：" + ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>拼接生成器命令行（与旧客户端 GetRandomMapArgs 完全一致）。</summary>
    private static string BuildArgs(CwRandomMapOptions options)
    {
        Random random = Random.Shared;
        int width = (options.Size + 1) * 40 + random.Next(20, 40);
        int height = (options.Size + 1) * 40 + random.Next(20, 40);
        double damage = options.Damage switch { 0 => 0, 1 => 0.02, _ => 0.06 };

        // 0=北 1=南 2=西 3=东；每方向两张，洗牌后取前 N 张（旧客户端做法）
        List<int> dirs = new[] { 0, 0, 1, 1, 2, 2, 3, 3 }
            .OrderBy(_ => random.Next(100))
            .Take(Math.Clamp(options.MaxPlayers, 2, 8))
            .ToList();

        bool straight = random.Next(10) > 5;
        string dirArgs = straight
            ? $"--nep {dirs.Count(x => x == 0)} --nwp {dirs.Count(x => x == 1)} --sep {dirs.Count(x => x == 2)} --swp {dirs.Count(x => x == 3)}"
            : $"--np {dirs.Count(x => x == 0)} --sp {dirs.Count(x => x == 1)} --wp {dirs.Count(x => x == 2)} --ep {dirs.Count(x => x == 3)}";

        string dmg = damage.ToString(CultureInfo.InvariantCulture);
        return $"-w {width} -h {height} {dirArgs} -s {dmg}{(options.Damage > 0 ? " -d " : " ")}--type {options.Type} -g standard";
    }

    private static string LastLine(string text)
    {
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? text : lines[^1];
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // 进程已退出或无权结束：忽略，交由上层提示超时
        }
    }
}
