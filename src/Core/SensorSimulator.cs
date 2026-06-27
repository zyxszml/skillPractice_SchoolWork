using System;
using Iso11820Simulator.Configuration;

namespace Iso11820Simulator.Core
{
    /// <summary>
    /// 仿真引擎：5 通道温度生成（对应文档 §2.3）。
    /// 调用方每 800ms 调一次 Update()，传入当前状态，返回新的温度值。
    /// </summary>
    public class SensorSimulator
    {
        private readonly SimulationSection _sim;
        private readonly Random _rng = new();

        // 内部状态
        public double Tf1 { get; private set; }
        public double Tf2 { get; private set; }
        public double Ts { get; private set; }
        public double Tc { get; private set; }
        public double TCal { get; private set; }

        // 稳定计数器（>StableTickCount 视为稳定）
        public int StableCounter { get; private set; }

        public SensorSimulator(SimulationSection sim)
        {
            _sim = sim;
            Tf1 = sim.InitialFurnaceTemp;
            Tf2 = sim.InitialFurnaceTemp - 1;
            Ts = sim.InitialFurnaceTemp * 0.3;
            Tc = sim.InitialFurnaceTemp * 0.25;
            TCal = sim.InitialFurnaceTemp;
        }

        /// <summary>每次 tick 推进一步。dt 秒默认 0.8。</summary>
        public void Update(TestState state, double dt = 0.8)
        {
            double target = _sim.TargetFurnaceTemp;            // 750
            double threshold = target - _sim.StableThreshold;  // 747
            double noise = _rng.NextDouble() * 2.0 - 1.0;      // [-1,1]
            noise *= _sim.TempFluctuation;

            switch (state)
            {
                case TestState.Preparing:
                    if (Tf1 < threshold)
                    {
                        // 升温阶段
                        Tf1 += _sim.HeatingRatePerSecond * dt + noise;
                        Tf2 += _sim.HeatingRatePerSecond * dt + noise2();
                        Ts = Tf1 * 0.3 + noise;
                        Tc = Tf1 * 0.25 + noise;
                        TCal = Tf1 + noise2() * 2;
                        StableCounter = 0;
                    }
                    else
                    {
                        // 稳定阶段（未切到 Ready 时）
                        Tf1 = target + noise;
                        Tf2 = target + noise2();
                        StableCounter++;
                    }
                    break;

                case TestState.Ready:
                    // 钳位到目标温度，继续累积稳定计数
                    Tf1 = target + noise;
                    Tf2 = target + noise2();
                    Ts += (Tf1 * 0.3 - Ts) * 0.05 + noise;
                    Tc += (Tf1 * 0.25 - Tc) * 0.05 + noise;
                    TCal = Tf1 + noise2() * 2;
                    StableCounter++;
                    break;

                case TestState.Recording:
                    // 炉温钳位
                    Tf1 = target + noise;
                    Tf2 = target + noise2();
                    // 表面温指数接近炉温*0.95，上限 800
                    double surfTarget = Math.Min(Tf1 * 0.95, 800);
                    Ts += (surfTarget - Ts) * 0.02 * dt / 0.8 + noise;
                    // 中心温指数接近炉温*0.85，上限 750（更慢）
                    double centerTarget = Math.Min(Tf1 * 0.85, 750);
                    Tc += (centerTarget - Tc) * 0.01 * dt / 0.8 + noise;
                    TCal = Tf1 + noise2() * 2;
                    break;

                case TestState.Idle:
                    // 停止加热：缓慢冷却
                    Tf1 -= 0.5 + noise2() * 0.1;
                    Tf2 -= 0.5 + noise2() * 0.1;
                    Ts += (Tf1 * 0.3 - Ts) * 0.02 + noise;
                    Tc += (Tf1 * 0.25 - Tc) * 0.02 + noise;
                    TCal = Tf1 + noise2() * 2;
                    StableCounter = 0;
                    break;

                case TestState.Complete:
                    // 保持炉温，UI 提示保存
                    Tf1 = target + noise;
                    Tf2 = target + noise2();
                    break;
            }

            // 防御性下限
            Tf1 = Math.Max(0, Tf1);
            Tf2 = Math.Max(0, Tf2);
            Ts = Math.Max(0, Ts);
            Tc = Math.Max(0, Tc);
        }

        /// <summary>停止加热后从外部直接重置（用于 Idle 状态）。</summary>
        public void Reset()
        {
            StableCounter = 0;
        }

        /// <summary>
        /// 为下一次试验重置样品状态：炉温保持（炉子不冷却，省升温时间），
        /// 但样品温（TS/TC）回到冷态——模拟"换一个新样品放入炉中"。
        /// 稳定计数器也清零，避免继承上一次的稳定判定。
        /// </summary>
        public void ResetForNewSample(double ambientTemp = 25.0)
        {
            // 炉温 TF1/TF2/TCal 保持不动；样品是新的、冷的
            Ts = ambientTemp;
            Tc = ambientTemp;
            StableCounter = 0;
        }

        private double noise2()
        {
            double n = _rng.NextDouble() * 2.0 - 1.0;
            return n * _sim.TempFluctuation;
        }
    }
}
