namespace Iso11820Simulator.Core
{
    /// <summary>试验状态机的 5 个状态。</summary>
    public enum TestState
    {
        /// <summary>空闲：炉子未加热</summary>
        Idle,
        /// <summary>升温中</summary>
        Preparing,
        /// <summary>就绪：炉温稳定可开始记录</summary>
        Ready,
        /// <summary>记录中</summary>
        Recording,
        /// <summary>完成</summary>
        Complete
    }

    public static class TestStateExtensions
    {
        public static string ToChinese(this TestState s) => s switch
        {
            TestState.Idle => "空闲",
            TestState.Preparing => "升温中",
            TestState.Ready => "就绪",
            TestState.Recording => "记录中",
            TestState.Complete => "完成",
            _ => s.ToString()
        };
    }
}
