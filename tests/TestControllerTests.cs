using Iso11820Simulator.Configuration;
using Iso11820Simulator.Core;
using Xunit;

namespace Iso11820Simulator.Tests
{
    /// <summary>试验状态机流转测试。</summary>
    public class TestControllerTests
    {
        private static (TestController c, SensorSimulator s) NewController()
        {
            var sim = new SimulationSection
            {
                InitialFurnaceTemp = 750.0,           // 直接从目标温度起步，便于快速到 Ready
                TargetFurnaceTemp = 750.0,
                HeatingRatePerSecond = 40.0,
                TempFluctuation = 0.5,
                StableThreshold = 3.0,
                StableTickCount = 3
            };
            var sensors = new SensorSimulator(sim);
            return (new TestController(sim, sensors), sensors);
        }

        [Fact]
        public void 初始状态为Idle()
        {
            var (c, _) = NewController();
            Assert.Equal(TestState.Idle, c.State);
        }

        [Fact]
        public void StartHeating_Idle转Preparing()
        {
            var (c, _) = NewController();
            c.StartHeating();
            Assert.Equal(TestState.Preparing, c.State);
        }

        [Fact]
        public void StartHeating_非Idle不重复切换()
        {
            var (c, _) = NewController();
            c.StartHeating();
            c.StartHeating();   // 已 Preparing，不应出错或回到 Idle
            Assert.Equal(TestState.Preparing, c.State);
        }

        [Fact]
        public void StopHeating_从Preparing回Idle()
        {
            var (c, _) = NewController();
            c.StartHeating();
            c.StopHeating();
            Assert.Equal(TestState.Idle, c.State);
        }

        [Fact]
        public void StopRecording_无有效记录回Preparing()
        {
            var (c, _) = NewController();
            c.StartHeating();
            // 推进到 Ready（初始炉温=目标温度，几 tick 后即可稳定到 Ready）
            for (int i = 0; i < 10; i++) c.Tick(0.8);
            Assert.Equal(TestState.Ready, c.State);
            c.StartRecording();
            Assert.Equal(TestState.Recording, c.State);
            // RecordSeconds 仍为 0 时停止 → 回 Preparing
            c.StopRecording();
            Assert.Equal(TestState.Preparing, c.State);
        }

        [Fact]
        public void StopRecording_有有效记录进Complete()
        {
            var (c, _) = NewController();
            c.StartHeating();
            for (int i = 0; i < 10; i++) c.Tick(0.8);   // 到 Ready
            c.StartRecording();
            for (int i = 0; i < 10; i++) c.Tick(0.8);   // 累积 RecordSeconds > 0
            Assert.True(c.RecordSeconds > 0, "应已累积记录秒数");
            c.StopRecording();
            Assert.Equal(TestState.Complete, c.State);
        }

        [Fact]
        public void MarkSavedAndPrepare_回Preparing并清零记录()
        {
            var (c, _) = NewController();
            c.MarkSavedAndPrepare();
            Assert.Equal(TestState.Preparing, c.State);
            Assert.Equal(0, c.RecordSeconds);
        }
    }
}
