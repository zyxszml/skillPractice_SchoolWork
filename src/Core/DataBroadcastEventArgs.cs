using System;
using System.Collections.Generic;

namespace Iso11820Simulator.Core
{
    /// <summary>当前仿真快照（每 800ms 推送一次给 UI）。</summary>
    public class SensorSnapshot
    {
        public DateTime Timestamp { get; set; }
        /// <summary>从升温开始的累计秒数（仿真时间）</summary>
        public int SimSeconds { get; set; }
        /// <summary>从升温开始的连续累计秒数，用于实时曲线横轴。</summary>
        public double SimElapsedSeconds { get; set; }
        public double Tf1 { get; set; }
        public double Tf2 { get; set; }
        public double Ts { get; set; }
        public double Tc { get; set; }
        public double TCal { get; set; }
        public TestState State { get; set; }
        /// <summary>当前记录阶段累计秒数（仅 Recording 递增）</summary>
        public int RecordSeconds { get; set; }
        /// <summary>最近 10 分钟炉温1 温漂（℃/10min），数据不足为 null</summary>
        public double? DriftTf1 { get; set; }
        public double? DriftTf2 { get; set; }
    }

    /// <summary>跨线程事件参数：携带最新快照 + 本 tick 新增消息。</summary>
    public class DataBroadcastEventArgs : EventArgs
    {
        public SensorSnapshot Snapshot { get; set; }
        public List<MasterMessage> Messages { get; set; } = new();
    }
}
