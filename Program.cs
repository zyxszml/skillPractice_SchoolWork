using System;
using System.Diagnostics;
using System.Windows.Forms;
using Iso11820Simulator.Forms;
using Iso11820Simulator.Global;
using Serilog;

namespace Iso11820Simulator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Serilog 初始化（滚动日志写入运行目录 logs\）
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.File(
                        path: System.IO.Path.Combine(System.AppContext.BaseDirectory, "logs", "app-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

            try
            {
                ApplicationConfiguration.Initialize();

                // 启动画面：覆盖 AppHost.Initialize（建库/连接 SQLite，是会卡住的同步步骤）。
                // 关键：splash 必须在进入 LoginForm 之前【同步】关闭并释放，
                // 否则其异步淡出 Timer 会在 login 的模态消息循环里触发，
                // 且 splash 的 TopMost 会盖住登录框，导致窗口层级/模态上下文混乱而闪退。
                var splash = new SplashScreenForm();
                splash.Show();
                Application.DoEvents();   // 确保 splash 立即绘制

                // 初始化全局对象 + 数据库连接（splash 期间在 UI 线程执行，窗口已显示不再“无响应”）
                var sw = Stopwatch.StartNew();
                var (ok, msg) = AppHost.Initialize();

                // 保证 splash 至少显示 1 秒，避免初始化过快导致一闪而过看不清。
                // 等待期间持续 DoEvents，保持窗口响应（主要是让 splash 静态停留即可）。
                const int MinSplashMs = 2000;
                while (sw.ElapsedMilliseconds < MinSplashMs)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(20);
                }

                // 同步关闭并释放 splash：不依赖 Timer 异步淡出，彻底切断与后续 login 模态循环的交互
                splash.Close();
                splash.Dispose();
                Application.DoEvents();

                if (!ok)
                {
                    MessageBox.Show(
                        msg + "\r\n\r\n请检查 appsettings.json 中 Database.ConnectionString 是否正确（默认 Data Source=iso11820.db，数据库文件会自动生成在程序目录）。",
                        "数据库初始化失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Log.Information("系统启动: {msg}", msg);

                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                        return;
                }

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "程序启动异常");
                MessageBox.Show("程序启动失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
