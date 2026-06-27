using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Iso11820Simulator.Core;

namespace Iso11820Simulator.Services
{
    /// <summary>
    /// 数据采集/仿真工作线程：每 TickIntervalMs（默认 800ms）调用控制器 Tick，
    /// 推送最新快照和增量消息给订阅者。所有 DataBroadcast 事件在后台线程触发，
    /// UI 必须用 Invoke 切回。
    /// </summary>
    public class DaqWorker
    {
        private readonly TestController _controller;
        private readonly DriftCalculator _drift;
        private readonly int _intervalMs;
        private readonly double _dtSec;

        private Task _task;
        private CancellationTokenSource _cts;
        public bool IsRunning { get; private set; }

        public DaqWorker(TestController controller, DriftCalculator drift, int intervalMs = 800)
        {
            _controller = controller;
            _drift = drift;
            _intervalMs = intervalMs;
            _dtSec = intervalMs / 1000.0;
        }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _task = Task.Run(() => LoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            try { _cts.Cancel(); } catch { }
            try { _task?.Wait(2000); } catch { }
        }

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1) 推进仿真与基础状态机
                    _controller.Tick(_dtSec);

                    // 2) 喂入温漂（仅记录阶段）
                    if (_controller.State == TestState.Recording)
                        _drift.Push(_controller.Sensors.Tf1, _controller.Sensors.Tf2);
                    else
                        _drift.Clear();

                    // 3) 计算温漂并执行记录阶段终止判定
                    var (d1, d2) = _drift.Compute();
                    _controller.UpdateDrift(d1, d2);
                    _controller.EvaluateRecordingTermination();

                    // 4) 构建快照
                    var snap = _controller.BuildSnapshot(d1, d2);

                    // 5) 广播（通过 Controller 的公开方法触发事件）
                    _controller.RaiseBroadcast(new DataBroadcastEventArgs
                    {
                        Snapshot = snap,
                        Messages = new List<MasterMessage>()
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("DaqWorker error: " + ex.Message);
                }

                try { await Task.Delay(_intervalMs, token); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
