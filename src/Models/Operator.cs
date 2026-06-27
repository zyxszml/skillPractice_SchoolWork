namespace Iso11820Simulator.Models
{
    /// <summary>操作员（用户账号）</summary>
    public class Operator
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Pwd { get; set; }
        /// <summary>角色：admin / operator</summary>
        public string UserType { get; set; }
    }
}
