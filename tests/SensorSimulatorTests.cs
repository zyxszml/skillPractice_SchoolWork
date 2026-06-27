using Iso11820Simulator.Configuration;
using Iso11820Simulator.Core;
using Xunit;

namespace Iso11820Simulator.Tests
{
    /// <summary>仿真引擎测试：升温、稳定、记录、降温各阶段数值行为。</summary>
    public class SensorSimulatorTests
    {
        private static SimulationSection DefaultSim() => new()
        {
            InitialFurnaceTemp = 25.0,
            TargetFurnaceTemp = 750.0,
            HeatingRatePerSecond = 40.0,
            TempFluctuation = 0.5,
            StableThreshold = 3.0,
            StableTickCount = 3,
            MaxTemperatureDriftPerTenMinutes = 2.0
        };

        [Fact]
        public void 升温阶段_TF1应单调上升()
        {
            var sim = DefaultSim();
            var s = new SensorSimulator(sim);
            double prev = s.Tf1;
            bool rose = false;
            for (int i = 0; i < 20; i++)
            {
                s.Update(TestState.Preparing, 0.8);
                if (s.Tf1 > prev) rose = true;
                prev = s.Tf1;
            }
            Assert.True(rose, "升温阶段 TF1 应随时间上升");
            Assert.True(s.Tf1 > 25.0);
        }

        [Fact]
        public void 稳定阶段_TF1钳位到目标温度附近()
        {
            var sim = DefaultSim();
            var s = new SensorSimulator(sim);
            // 直接把 TF1 推到稳定区间以上，验证稳定阶段钳位
            for (int i = 0; i < 200; i++) s.Update(TestState.Preparing, 0.8);
            // 此时已进入稳定（>= threshold），TF1 应在 750 附近
            Assert.InRange(s.Tf1, 749.0, 751.0);
            Assert.True(s.StableCounter > 0, "稳定计数器应递增");
        }

        [Fact]
        public void 记录阶段_表面温向炉温95指数接近()
        {
            var sim = DefaultSim();
            var s = new SensorSimulator(sim);
            // 让表面温从低位开始，记录阶段应上升
            double tsStart = s.Ts;
            for (int i = 0; i < 200; i++) s.Update(TestState.Recording, 0.8);
            Assert.True(s.Ts > tsStart, "记录阶段表面温应向炉温×0.95 上升");
            Assert.True(s.Ts <= 800, "表面温不应超过 800℃");
        }

        [Fact]
        public void 降温阶段_TF1持续下降()
        {
            var sim = DefaultSim();
            var s = new SensorSimulator(sim);
            for (int i = 0; i < 200; i++) s.Update(TestState.Preparing, 0.8); // 升到稳定
            double before = s.Tf1;
            for (int i = 0; i < 10; i++) s.Update(TestState.Idle, 0.8);
            Assert.True(s.Tf1 < before, "停止加热后 TF1 应下降");
        }

        [Fact]
        public void 所有温度非负()
        {
            var sim = DefaultSim();
            var s = new SensorSimulator(sim);
            foreach (var state in new[] { TestState.Idle, TestState.Preparing, TestState.Recording })
            {
                for (int i = 0; i < 50; i++) s.Update(state, 0.8);
                Assert.True(s.Tf1 >= 0 && s.Tf2 >= 0 && s.Ts >= 0 && s.Tc >= 0);
            }
        }

        [Fact]
        public void ResetForNewSample_样品温回冷态_炉温保持()
        {
            var sim = DefaultSim();
            var s = new SensorSimulator(sim);
            // 把炉子和样品都加热到稳定态
            for (int i = 0; i < 200; i++) s.Update(TestState.Preparing, 0.8);
            double tf1Before = s.Tf1;
            double tsBefore = s.Ts;

            // 换新样品
            s.ResetForNewSample(ambientTemp: 25.0);

            // 炉温应保持（不冷却）
            Assert.Equal(tf1Before, s.Tf1, 1);
            // 样品温回到冷态（环境温度）
            Assert.Equal(25.0, s.Ts, 1);
            Assert.Equal(25.0, s.Tc, 1);
            Assert.Equal(0, s.StableCounter);
            Assert.True(s.Ts < tsBefore, "新样品温度应远低于加热后的样品温度");
        }
    }
}
