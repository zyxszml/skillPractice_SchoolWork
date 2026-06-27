using System.Collections.Generic;
using MathNet.Numerics.LinearRegression;

namespace Iso11820Simulator.Services
{
    /// <summary>
    /// 温漂线性回归：对最近 N 个采样点做 SimpleRegression.Slope()。
    /// 默认按 800ms 采样间隔保留最近 10 分钟数据。
    /// </summary>
    public class DriftCalculator
    {
        private readonly Queue<double> _tf1 = new();
        private readonly Queue<double> _tf2 = new();
        private readonly int _maxPoints;
        private readonly double _sampleIntervalSeconds;
        private readonly double _windowSeconds;

        /// <summary>开始输出温漂所需的最小样本比例（默认 80%）。</summary>
        private const double MinFractionToOutput = 0.8;

        public DriftCalculator(int maxPoints, double sampleIntervalSeconds = 0.8, double windowSeconds = 600)
        {
            _maxPoints = maxPoints;
            _sampleIntervalSeconds = sampleIntervalSeconds;
            _windowSeconds = windowSeconds;
        }

        public void Push(double tf1, double tf2)
        {
            _tf1.Enqueue(tf1);
            _tf2.Enqueue(tf2);
            while (_tf1.Count > _maxPoints) _tf1.Dequeue();
            while (_tf2.Count > _maxPoints) _tf2.Dequeue();
        }

        public void Clear()
        {
            _tf1.Clear();
            _tf2.Clear();
        }

        public int Count => _tf1.Count;

        /// <summary>至少需要这么多样本才开始输出温漂。</summary>
        private int MinPointsToOutput => (int)System.Math.Max(2, _maxPoints * MinFractionToOutput);

        /// <summary>
        /// 返回 (tf1漂移, tf2漂移)，单位 ℃/10min。
        /// slope 来自 SimpleRegression，单位是 ℃/秒；×600秒/10min 换算为 ℃/10min。
        /// 样本数不足窗口的 80% 时返回 null（避免升温初期短序列误判）。
        /// </summary>
        public (double? tf1, double? tf2) Compute()
        {
            if (_tf1.Count < MinPointsToOutput) return (null, null);
            if (_tf1.Count < 2) return (null, null);

            var xs = new double[_tf1.Count];
            for (int i = 0; i < xs.Length; i++) xs[i] = i * _sampleIntervalSeconds;

            var ys1 = _tf1.ToArray();
            var ys2 = _tf2.ToArray();

            // SimpleRegression.Fit 需要至少 2 点，返回 (intercept, slope)
            var r1 = SimpleRegression.Fit(xs, ys1);
            var r2 = SimpleRegression.Fit(xs, ys2);

            // slope 单位 ℃/秒；×600 秒 = ℃/10min（10分钟 = 600秒）
            double d1 = r1.Item2 * _windowSeconds;
            double d2 = r2.Item2 * _windowSeconds;
            return (d1, d2);
        }
    }
}
