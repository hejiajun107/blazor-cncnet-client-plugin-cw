using CnCNetClient.Tools;

namespace CwUi.Services;

/// <summary>
/// IRC 大厅节点设置，存于游戏目录 ClientSettings.ini 的 [MultiPlayer] IRCServerNode
/// （与老客户端同名同位置，便于迁移）。0=国内，1=国际。
/// </summary>
public sealed class CwIrcNodeSetting
{
    private const string Section = "MultiPlayer";
    private const string Key = "IRCServerNode";

    private readonly string _path;

    public CwIrcNodeSetting(CwEnvironment env) => _path = env.GamePath("ClientSettings.ini");

    public int Node
    {
        get
        {
            try
            {
                return new IniFile(_path).GetIntValue(Section, Key, 0);
            }
            catch
            {
                return 0;
            }
        }
        set
        {
            try
            {
                IniFile ini = new(_path);
                ini.SetIntValue(Section, Key, value);
                ini.WriteIniFile();
            }
            catch
            {
                // 设置文件不可写时保持原值，下次连接仍按旧节点
            }
        }
    }
}
