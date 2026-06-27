using System.Windows.Forms;
using Iso11820Simulator.Global;

namespace Iso11820Simulator.Services
{
    /// <summary>
    /// 数据库状态统一提示：各页面查询失败时调用，避免零散、互相不一致的报错。
    /// </summary>
    public static class DbStatusUI
    {
        /// <summary>查询失败时的统一提示。返回固定文案，调用方决定弹窗还是日志。</summary>
        public static string BuildQueryFailMessage(string operation, string detail)
            => $"[{operation}] 数据库操作失败：{detail}\r\n\r\n请检查 appsettings.json 的数据库连接字符串是否正确、程序目录是否有读写权限。";

        /// <summary>查询失败弹窗（统一外观）。仅当确实发生异常时调用。</summary>
        public static void ShowQueryFailed(IWin32Window owner, string operation, string detail)
        {
            MessageBox.Show(owner, BuildQueryFailMessage(operation, detail),
                "数据库异常", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>当前数据库是否在线（供主界面横幅等使用）。AppHost 未初始化时返回 false。</summary>
        public static bool IsOnline()
        {
            try { return AppHost.Db?.CheckConnection().ok ?? false; }
            catch { return false; }
        }
    }
}
