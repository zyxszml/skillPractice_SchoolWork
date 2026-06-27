using Iso11820Simulator.Models;
using Iso11820Simulator.Services;
using Xunit;

namespace Iso11820Simulator.Tests
{
    /// <summary>判定逻辑与密码哈希测试。</summary>
    public class ExportJudgeAndPasswordTests
    {
        [Fact]
        public void Judge_三项全达标_通过()
        {
            var t = new TestMaster { DeltaTf = 30, LostWeightPer = 10, FlameDuration = 2 };
            Assert.True(ExportService.Judge(t));
        }

        [Fact]
        public void Judge_温升超标_不通过()
        {
            var t = new TestMaster { DeltaTf = 51, LostWeightPer = 10, FlameDuration = 2 };
            Assert.False(ExportService.Judge(t));
        }

        [Fact]
        public void Judge_失重率超标_不通过()
        {
            var t = new TestMaster { DeltaTf = 30, LostWeightPer = 51, FlameDuration = 2 };
            Assert.False(ExportService.Judge(t));
        }

        [Fact]
        public void Judge_火焰持续超5秒_不通过()
        {
            var t = new TestMaster { DeltaTf = 30, LostWeightPer = 10, FlameDuration = 5 };
            Assert.False(ExportService.Judge(t));
        }

        [Fact]
        public void Judge_边界值恰好达标_通过()
        {
            // ≤50 且 ≤50 且 <5
            var t = new TestMaster { DeltaTf = 50, LostWeightPer = 50, FlameDuration = 4 };
            Assert.True(ExportService.Judge(t));
        }

        // ---------- 密码哈希 ----------

        [Fact]
        public void Hash_相同用户名密码产生相同哈希()
        {
            string h1 = PasswordHasher.Hash("admin", "123456");
            string h2 = PasswordHasher.Hash("admin", "123456");
            Assert.Equal(h1, h2);
            Assert.Equal(64, h1.Length);   // SHA256 = 64 位十六进制
        }

        [Fact]
        public void Hash_不同用户名产生不同哈希()
        {
            string h1 = PasswordHasher.Hash("admin", "123456");
            string h2 = PasswordHasher.Hash("experimenter", "123456");
            Assert.NotEqual(h1, h2);
        }

        [Fact]
        public void Verify_正确密码返回true()
        {
            string stored = PasswordHasher.Hash("admin", "123456");
            Assert.True(PasswordHasher.Verify("admin", "123456", stored));
        }

        [Fact]
        public void Verify_错误密码返回false()
        {
            string stored = PasswordHasher.Hash("admin", "123456");
            Assert.False(PasswordHasher.Verify("admin", "wrong", stored));
        }

        [Fact]
        public void Verify_兼容历史明文()
        {
            // 旧库存的是明文 "123456"
            Assert.True(PasswordHasher.Verify("admin", "123456", "123456"));
            Assert.False(PasswordHasher.Verify("admin", "wrong", "123456"));
        }

        [Fact]
        public void Verify_空存储返回false()
        {
            Assert.False(PasswordHasher.Verify("admin", "123456", ""));
            Assert.False(PasswordHasher.Verify("admin", "123456", null!));
        }
    }
}
