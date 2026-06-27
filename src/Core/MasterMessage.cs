using System.Drawing;

namespace Iso11820Simulator.Core
{
    /// <summary>系统消息（对应文档 §2.6）。</summary>
    public class MasterMessage
    {
        /// <summary>HH:mm:ss</summary>
        public string Time { get; set; }

        public string Message { get; set; }

        public MessageColor Color { get; set; } = MessageColor.Normal;

        public MasterMessage() { }
        public MasterMessage(string time, string message, MessageColor color = MessageColor.Normal)
        {
            Time = time; Message = message; Color = color;
        }
    }

    public enum MessageColor
    {
        /// <summary>白色普通</summary>
        Normal,
        /// <summary>黄色提示</summary>
        Warning,
        /// <summary>红色错误</summary>
        Error
    }

    public static class MessageColorExtensions
    {
        public static Color ToColor(this MessageColor c) => c switch
        {
            MessageColor.Warning => Color.Yellow,
            MessageColor.Error => Color.Red,
            _ => Color.White
        };
    }
}
