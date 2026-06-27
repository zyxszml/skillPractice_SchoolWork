using Iso11820Simulator.Configuration;
using Iso11820Simulator.Core;
using Iso11820Simulator.Database;
using Iso11820Simulator.Models;
using Iso11820Simulator.Services;

namespace Iso11820Simulator.Global
{
    /// <summary>
    /// 全局单例容器：持有所有跨窗体共享的核心对象。
    /// 注意：类名故意取为 AppHost（不叫 AppContext），避免与 System.AppContext 冲突。
    /// </summary>
    public static class AppHost
    {
        public static AppSettings Settings { get; private set; }
        public static DbHelper Db { get; private set; }
        public static SensorSimulator Simulator { get; private set; }
        public static DriftCalculator Drift { get; private set; }
        public static TestController Controller { get; private set; }
        public static DaqWorker Daq { get; private set; }
        public static ExportService Exporter { get; private set; }

        /// <summary>当前登录的操作员</summary>
        public static Operator CurrentUser { get; set; }

        /// <summary>当前默认设备（启动时载入）</summary>
        public static Apparatus DefaultApparatus { get; set; }

        /// <summary>试验中实时温度采样缓存（Recording 时逐秒追加，保持时间顺序）</summary>
        public static System.Collections.Concurrent.ConcurrentQueue<TempSample> LiveSamples
            = new();

        /// <summary>初始化全局对象。返回 (ok, message)。</summary>
        public static (bool ok, string message) Initialize()
        {
            Settings = AppSettings.Load();
            Db = new DbHelper(Settings.Database.ConnectionString);

            var (ok, msg) = Db.TryConnectAndInit();
            if (!ok) return (false, msg);

            DefaultApparatus = Db.GetDefaultApparatus();
            Simulator = new SensorSimulator(Settings.Simulation);
            var driftPoints = Math.Max(2,
                (int)Math.Ceiling(600.0 / Math.Max(1, Settings.Simulation.TickIntervalMs) * 1000.0));
            Drift = new DriftCalculator(driftPoints, Settings.Simulation.TickIntervalMs / 1000.0);
            Controller = new TestController(Settings.Simulation, Simulator);
            Daq = new DaqWorker(Controller, Drift, Settings.Simulation.TickIntervalMs);
            Exporter = new ExportService(Settings.FileStorage, Settings.Report);

            return (true, msg);
        }
    }
}
