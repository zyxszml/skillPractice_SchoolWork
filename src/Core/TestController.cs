using System;
using System.Collections.Generic;
using Iso11820Simulator.Configuration;

namespace Iso11820Simulator.Core
{
    /// <summary>
    /// 试验控制器：维护状态机、判定状态切换、终止条件、恒功率队列、计时。
    /// 由 DaqWorker 每 800ms 调用 Tick() 推进。
    /// </summary>
    public class TestController
    {
        private readonly SimulationSection _sim;
        private readonly SensorSimulator _sensors;

        // 模式
        public bool StandardMode { get; set; } = true;            // true=标准60min，false=自定义
        public int TargetDurationSeconds { get; set; } = 3600;

        // 运行状态
        public TestState State { get; private set; } = TestState.Idle;
        public int SimSeconds { get; private set; }      // 升温开始累计秒
        public int RecordSeconds { get; private set; }  // 记录阶段累计秒
        private double _simElapsedSeconds;
        private double _recordElapsedSeconds;

        // 恒功率记录（Ready 后开始采样）
        private readonly Queue<double> _pidQueue = new();

        // 升温阶段的初始温度（用于计算温升）
        public double InitialAmbTemp { get; set; } = 25.0;

        // 终止检查点（标准模式每5分钟检查，30/35/40/45/50/55 分钟）
        private readonly HashSet<int> _checkpointsHit = new();
        private double? _lastDriftTf1;
        private double? _lastDriftTf2;

        public event EventHandler<DataBroadcastEventArgs> DataBroadcast;

        public SensorSimulator Sensors => _sensors;
        public int ConstPowerAverage { get; private set; }

        public TestController(SimulationSection sim, SensorSimulator sensors)
        {
            _sim = sim;
            _sensors = sensors;
        }

        // ---------------- 用户动作 ----------------

        public void StartHeating()
        {
            if (State == TestState.Idle)
            {
                State = TestState.Preparing;
                SimSeconds = 0;
                _simElapsedSeconds = 0;
                _checkpointsHit.Clear();
                Broadcast($"开始升温，系统升温中", MessageColor.Normal);
            }
        }

        public void StopHeating()
        {
            // 从 Preparing/Ready/Complete 回到 Idle
            if (State != TestState.Recording)
            {
                State = TestState.Idle;
                _sensors.Reset();
                Broadcast($"用户停止升温，炉子开始冷却", MessageColor.Normal);
            }
        }

        public void StartRecording()
        {
            if (State == TestState.Ready)
            {
                State = TestState.Recording;
                RecordSeconds = 0;
                _recordElapsedSeconds = 0;
                _checkpointsHit.Clear();
                // 计算恒功率平均值（最近 600 个 PID 输出）
                ConstPowerAverage = ComputeConstPower();
                Broadcast($"开始记录，计时开始", MessageColor.Normal);
            }
        }

        public void StopRecording()
        {
            if (State == TestState.Recording)
            {
                if (RecordSeconds > 0)
                {
                    State = TestState.Complete;
                    Broadcast($"用户手动停止记录", MessageColor.Normal);
                }
                else
                {
                    // 还没产生有效记录，回 Preparing
                    State = TestState.Preparing;
                    Broadcast($"无有效记录数据，回到升温状态", MessageColor.Warning);
                }
            }
        }

        /// <summary>保存完毕后调用，回到 Preparing 保持炉温等待下一次试验。</summary>
        public void MarkSavedAndPrepare()
        {
            State = TestState.Preparing;
            RecordSeconds = 0;
            _recordElapsedSeconds = 0;
            _checkpointsHit.Clear();
            _pidQueue.Clear();
            _lastDriftTf1 = null;
            _lastDriftTf2 = null;
        }

        public void UpdateDrift(double? driftTf1, double? driftTf2)
        {
            _lastDriftTf1 = driftTf1;
            _lastDriftTf2 = driftTf2;
        }

        // ---------------- 每 tick 推进 ----------------

        /// <summary>每 800ms 由 DaqWorker 调用。dt 单位秒，默认 0.8。</summary>
        public void Tick(double dt = 0.8)
        {
            // 任何状态都推进仿真时间，保证降温曲线（Idle）也能在曲线上连续显示
            _simElapsedSeconds += dt;
            SimSeconds = (int)Math.Floor(_simElapsedSeconds);

            // 先推进传感器
            _sensors.Update(State, dt);

            // Ready 状态采样 PID 输出（恒功率计算）
            if (State == TestState.Ready)
            {
                _pidQueue.Enqueue(_sensors.Tf1);
                while (_pidQueue.Count > 600) _pidQueue.Dequeue();
            }

            // 状态切换判定
            switch (State)
            {
                case TestState.Preparing:
                    {
                        if (_sensors.Tf1 >= _sim.TargetFurnaceTemp - _sim.StableThreshold
                            && _sensors.StableCounter > _sim.StableTickCount)
                        {
                            // 满足 CheckStartCriteria：745~755 且稳定
                            if (_sensors.Tf1 >= 745 && _sensors.Tf1 <= 755)
                            {
                                State = TestState.Ready;
                                Broadcast($"温度已稳定，可以开始记录", MessageColor.Normal);
                            }
                        }
                        break;
                    }
                case TestState.Ready:
                    {
                        // 跌出 745~755 → 回 Preparing
                        if (_sensors.Tf1 < 745 || _sensors.Tf1 > 755)
                        {
                            State = TestState.Preparing;
                            _sensors.Reset();
                            Broadcast($"温度偏离稳定范围，回到升温状态", MessageColor.Warning);
                        }
                        break;
                    }
                case TestState.Recording:
                    {
                        _recordElapsedSeconds += dt;
                        RecordSeconds = (int)Math.Floor(_recordElapsedSeconds);
                        break;
                    }
            }
        }

        public void EvaluateRecordingTermination()
        {
            if (State != TestState.Recording) return;

            if (StandardMode)
            {
                int[] checkpoints = { 1800, 2100, 2400, 2700, 3000, 3300 }; // 30~55 min
                foreach (var cp in checkpoints)
                {
                    if (RecordSeconds >= cp && !_checkpointsHit.Contains(cp))
                    {
                        _checkpointsHit.Add(cp);
                        if (CheckEarlyTermination())
                        {
                            State = TestState.Complete;
                            Broadcast($"满足终止条件，试验结束", MessageColor.Warning);
                            return;
                        }
                    }
                }

                if (RecordSeconds >= 3600)
                {
                    State = TestState.Complete;
                    Broadcast($"记录时间到达 3600 秒，试验自动结束", MessageColor.Normal);
                }
            }
            else if (RecordSeconds >= TargetDurationSeconds)
            {
                State = TestState.Complete;
                Broadcast($"记录时间到达 {TargetDurationSeconds} 秒，试验结束", MessageColor.Normal);
            }
        }

        // ---------------- 提前终止判定 ----------------
        // 当前代码复用炉温稳定条件：10 分钟温漂有效，且 TF1/TF2 均 <= MaxTemperatureDriftPerTenMinutes
        private bool CheckEarlyTermination()
        {
            if (!_lastDriftTf1.HasValue || !_lastDriftTf2.HasValue) return false;

            var max = _sim.MaxTemperatureDriftPerTenMinutes;
            return Math.Abs(_lastDriftTf1.Value) <= max
                && Math.Abs(_lastDriftTf2.Value) <= max;
        }

        private int ComputeConstPower()
        {
            if (_pidQueue.Count == 0) return 0;
            double sum = 0;
            foreach (var v in _pidQueue) sum += v;
            double avg = sum / _pidQueue.Count;
            // 简单映射：温度（℃）→ 恒功率（0~25600），按 TargetFurnaceTemp 比例
            int power = (int)Math.Round(avg / _sim.TargetFurnaceTemp * 25600);
            return Math.Clamp(power, 0, 25600);
        }

        // ---------------- 生成快照 ----------------
        public SensorSnapshot BuildSnapshot(double? driftTf1, double? driftTf2)
        {
            return new SensorSnapshot
            {
                Timestamp = DateTime.Now,
                SimSeconds = SimSeconds,
                SimElapsedSeconds = _simElapsedSeconds,
                Tf1 = _sensors.Tf1,
                Tf2 = _sensors.Tf2,
                Ts = _sensors.Ts,
                Tc = _sensors.Tc,
                TCal = _sensors.TCal,
                State = State,
                RecordSeconds = RecordSeconds,
                DriftTf1 = driftTf1,
                DriftTf2 = driftTf2
            };
        }

        private void Broadcast(string msg, MessageColor color)
        {
            var args = new DataBroadcastEventArgs
            {
                Snapshot = BuildSnapshot(null, null),
                Messages = { new MasterMessage(DateTime.Now.ToString("HH:mm:ss"), msg, color) }
            };
            DataBroadcast?.Invoke(this, args);
        }

        /// <summary>
        /// 供 DaqWorker 在后台线程广播快照（事件本身只能在声明类内 Invoke）。
        /// </summary>
        public void RaiseBroadcast(DataBroadcastEventArgs args)
        {
            DataBroadcast?.Invoke(this, args);
        }
    }
}
