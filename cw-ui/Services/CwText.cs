using CnCNetClient.Core.Extensions;

namespace CwUi;

/// <summary>
/// 插件文案查表：翻译键为 <c>Plugin:CwUi:&lt;key&gt;</c>，值写在游戏
/// <c>Resources\Translations\&lt;语言&gt;\Translation.ini</c>；未收录时回退到中文原文。
/// </summary>
internal static class CwText
{
    public static string T(string key, string fallback) => fallback.L10N("Plugin:CwUi:" + key);
}
