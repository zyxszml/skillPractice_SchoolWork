using Iso11820Simulator.Services;
using Xunit;

namespace Iso11820Simulator.Tests
{
    /// <summary>温漂线性回归测试。</summary>
    public class DriftCalculatorTests
    {
        // 800ms 采样间隔 × 750 点 = 600 秒(10分钟)窗口
        private static DriftCalculator NewCalc(int maxPoints = 750)
            => new(maxPoints, sampleIntervalSeconds: 0.8, windowSeconds: 600);

        [Fact]
        public void Compute_数据不足80窗口时返回null()
        {
            var calc = NewCalc(maxPoints: 750);
            // 80% = 600 点，只喂 100 点应返回 null
            for (int i = 0; i < 100; i++) calc.Push(750, 750);
            var (d1, d2) = calc.Compute();
            Assert.Null(d1);
            Assert.Null(d2);
        }

        [Fact]
        public void Compute_恒定温度温漂为0()
        {
            var calc = NewCalc(maxPoints: 10);
            for (int i = 0; i < 10; i++) calc.Push(750.0, 749.0);
            var (d1, d2) = calc.Compute();
            Assert.NotNull(d1);
            Assert.InRange(Math.Abs(d1!.Value), 0, 1e-6);
            Assert.InRange(Math.Abs(d2!.Value), 0, 1e-6);
        }

        [Fact]
        public void Compute_线性上升温漂符号正确且量级合理()
        {
            // 每 0.8s 升 0.5℃：slope = 0.5/0.8 = 0.625 ℃/s；×600 = 375 ℃/10min
            var calc = NewCalc(maxPoints: 10);
            double t = 750;
            for (int i = 0; i < 10; i++) { calc.Push(t, t); t += 0.5; }
            var (d1, d2) = calc.Compute();
            Assert.NotNull(d1);
            Assert.True(d1 > 0, "升温应得到正温漂");
            // 量级应在 375 附近（线性回归）
            Assert.InRange(d1!.Value, 370, 380);
        }

        [Fact]
        public void Clear_清空后返回null()
        {
            var calc = NewCalc(maxPoints: 10);
            for (int i = 0; i < 10; i++) calc.Push(750, 750);
            calc.Clear();
            Assert.Equal(0, calc.Count);
            var (d1, d2) = calc.Compute();
            Assert.Null(d1);
        }
    }
}
