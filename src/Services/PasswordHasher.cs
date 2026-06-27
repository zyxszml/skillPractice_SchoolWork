using System;
using System.Security.Cryptography;
using System.Text;

namespace Iso11820Simulator.Services
{
    /// <summary>
    /// 密码哈希工具：SHA256。
    /// 用固定盐 + 用户名，避免彩虹表；返回 64 位十六进制小写字符串，与 operators.pwd VARCHAR(64) 对齐。
    /// 仍属演示级安全（无 PBKDF2/argon2 依赖），但对仿真教学项目足够，且不再明文落库。
    /// </summary>
    public static class PasswordHasher
    {
        private const string Salt = "ISO11820$";

        /// <summary>对明文密码做哈希。</summary>
        public static string Hash(string username, string password)
        {
            var raw = Salt + (username ?? "") + ":" + (password ?? "");
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// 校验密码。同时兼容：
        /// 1) 数据库存的是本算法的哈希（64位十六进制）→ 比对哈希；
        /// 2) 数据库存的是旧明文（如历史 seed 的 "123456"）→ 直接比对明文。
        /// 这样既支持新部署，也兼容旧库不破坏登录。
        /// </summary>
        public static bool Verify(string username, string password, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            if (stored.Length == 64 && IsHex(stored))
                return string.Equals(stored, Hash(username, password), StringComparison.OrdinalIgnoreCase);
            // 旧明文兼容
            return string.Equals(stored, password, StringComparison.Ordinal);
        }

        private static bool IsHex(string s)
        {
            foreach (var c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            return true;
        }
    }
}
