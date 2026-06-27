using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Iso11820Simulator.Models;
using Microsoft.Data.Sqlite;

namespace Iso11820Simulator.Database
{
    /// <summary>
    /// 统一管理 SQLite 连接与全部 SQL 操作。
    /// 程序首次启动会自动执行 schema.sql + seed.sql 完成建表与种子数据写入。
    /// 数据库文件默认放在程序运行目录下（见 appsettings.json 的连接串 Data Source），
    /// 真正"零配置"——无需安装任何数据库服务。
    /// </summary>
    public class DbHelper
    {
        private readonly string _connStr;

        public DbHelper(string connectionString)
        {
            _connStr = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureDataDirectory();
        }

        // ---------- 连接管理 ----------
        public SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection(_connStr);
            conn.Open();
            // SQLite 外键约束默认关闭，每次连接显式开启，
            // 保证 testmaster.productid → productmaster 的外键生效。
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }
            return conn;
        }

        /// <summary>
        /// 确保 SQLite 数据库文件所在目录存在，否则 SqliteConnection 会报找不到路径。
        /// 连接串形如 Data Source=.\db\iso11820.db 时需要预先建好 db 目录。
        /// </summary>
        private void EnsureDataDirectory()
        {
            try
            {
                var builder = new SqliteConnectionStringBuilder(_connStr);
                var dataSource = builder.DataSource;
                if (string.IsNullOrWhiteSpace(dataSource)) return;
                // ":memory:" 或纯文件名（无目录）无需建目录
                if (dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)) return;
                var dir = Path.GetDirectoryName(dataSource);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch
            {
                // 解析失败不致命，留给连接阶段报错
            }
        }

        /// <summary>
        /// 轻量级连接探测（SELECT 1），用于主界面横幅显示数据库在线状态。
        /// 返回 (ok, errorMsg)；失败时 ok=false 且 errorMsg 含简短原因。
        /// </summary>
        public (bool ok, string error) CheckConnection()
        {
            try
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                cmd.ExecuteScalar();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 测试连接并首次初始化（库为空时执行 schema.sql + seed.sql）。
        /// 返回 (ok, message)。
        /// </summary>
        public (bool ok, string message) TryConnectAndInit()
        {
            try
            {
                using var conn = CreateConnection();
                var firstInit = !TableExists(conn, "operators");

                // 每次启动都补跑幂等脚本：表或种子数据被手动删除时可自动恢复基础结构。
                ExecuteScript(conn, ReadEmbededOrFile("schema.sql"));
                ExecuteScript(conn, ReadEmbededOrFile("seed.sql"));

                var (valid, detail) = ValidateCoreDatabase(conn);
                if (!valid) return (false, detail);

                return firstInit
                    ? (true, "首次启动：已自动创建数据库并写入初始数据。")
                    : (true, "数据库连接正常，结构和初始数据已检查。");
            }
            catch (Exception ex)
            {
                return (false, "数据库连接失败：" + ex.Message);
            }
        }

        private static bool TableExists(SqliteConnection conn, string tableName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND LOWER(name)=LOWER(@table)";
            cmd.Parameters.AddWithValue("@table", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
        }

        private static long ScalarLong(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static (bool ok, string message) ValidateCoreDatabase(SqliteConnection conn)
        {
            var requiredTables = new[]
            {
                "operators", "apparatus", "productmaster", "testmaster", "sensors", "CalibrationRecords"
            };
            var missing = new List<string>();
            foreach (var table in requiredTables)
            {
                if (!TableExists(conn, table))
                    missing.Add(table);
            }

            if (missing.Count > 0)
                return (false, "数据库初始化不完整，缺少表：" + string.Join(", ", missing));

            if (ScalarLong(conn, "SELECT COUNT(*) FROM operators WHERE username='admin' AND pwd='8cf3c5b42c77d68a18cb4795f7172d2e71c94fe8c048f6d9091a64069d1b8e5d'") == 0
                && ScalarLong(conn, "SELECT COUNT(*) FROM operators WHERE username='admin' AND pwd='123456'") == 0)
                return (false, "数据库初始化不完整：缺少初始管理员账号 admin/123456。");

            if (ScalarLong(conn, "SELECT COUNT(*) FROM apparatus") == 0)
                return (false, "数据库初始化不完整：缺少默认试验设备。");

            if (ScalarLong(conn, "SELECT COUNT(*) FROM sensors WHERE sensorid IN (0,1,2,3,16)") < 5)
                return (false, "数据库初始化不完整：缺少必要传感器通道 0/1/2/3/16。");

            return (true, "数据库核心结构和初始数据正常。");
        }

        private static string ReadEmbededOrFile(string name)
        {
            // 优先读取输出目录下的 src\Database\xxx.sql（csproj 配置了 CopyToOutputDirectory）
            var candidates = new[]
            {
                Path.Combine(System.AppContext.BaseDirectory, "src", "Database", name),
                Path.Combine(System.AppContext.BaseDirectory, name),
            };
            foreach (var p in candidates)
            {
                if (File.Exists(p))
                    return File.ReadAllText(p);
            }
            // 兜底：从嵌入资源读取
            var asm = Assembly.GetExecutingAssembly();
            var resName = $"Iso11820Simulator.Database.{name}";
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream != null)
                using (var sr = new StreamReader(stream))
                    return sr.ReadToEnd();
            throw new FileNotFoundException($"找不到 {name}，请确认 csproj 的 CopyToOutputDirectory 配置。");
        }

        /// <summary>按分号拆分并逐条执行（DDL/INSERT 幂等脚本）。</summary>
        private void ExecuteScript(SqliteConnection conn, string script)
        {
            if (string.IsNullOrWhiteSpace(script)) return;
            var parts = SplitSqlStatements(script);
            foreach (var raw in parts)
            {
                var sql = raw.Trim();
                if (sql.Length == 0) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                try { cmd.ExecuteNonQuery(); }
                catch (SqliteException)
                {
                    // 幂等初始化脚本重复执行时的"表/记录已存在"类错误忽略；
                    // schema/seed 全部用 IF NOT EXISTS / WHERE NOT EXISTS，正常不会进到这里。
                }
            }
        }

        private static IEnumerable<string> SplitSqlStatements(string script)
        {
            var statements = new List<string>();
            var current = new StringBuilder();
            bool inSingleQuote = false;
            bool inLineComment = false;
            bool inBlockComment = false;

            for (int i = 0; i < script.Length; i++)
            {
                char c = script[i];
                char next = i + 1 < script.Length ? script[i + 1] : '\0';

                if (inLineComment)
                {
                    if (c == '\r' || c == '\n')
                        inLineComment = false;
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (!inSingleQuote)
                {
                    if (c == '-' && next == '-')
                    {
                        inLineComment = true;
                        i++;
                        continue;
                    }

                    if (c == '/' && next == '*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }
                }

                current.Append(c);

                if (c == '\'' && !IsEscaped(script, i))
                    inSingleQuote = !inSingleQuote;
                else if (c == ';' && !inSingleQuote)
                {
                    statements.Add(current.ToString(0, current.Length - 1));
                    current.Clear();
                }
            }

            if (current.Length > 0)
                statements.Add(current.ToString());

            return statements;
        }

        private static bool IsEscaped(string text, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && text[i] == '\\'; i--)
                slashCount++;
            return slashCount % 2 == 1;
        }

        // ============================================================
        //  登录
        // ============================================================
        public bool Login(string username, string pwd, out Operator op)
        {
            op = null;
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            // 先按用户名取出存储的密码（可能是哈希或旧明文），再用 PasswordHasher 校验
            cmd.CommandText = "SELECT userid, username, pwd, usertype FROM operators WHERE username=@u LIMIT 1";
            cmd.Parameters.AddWithValue("@u", username);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var storedPwd = reader.GetString(2);
                if (!Iso11820Simulator.Services.PasswordHasher.Verify(username, pwd, storedPwd))
                    return false;

                op = new Operator
                {
                    UserId = reader.GetString(0),
                    UserName = reader.GetString(1),
                    Pwd = storedPwd,
                    UserType = reader.GetString(3)
                };
                return true;
            }
            return false;
        }

        public List<Operator> GetOperators()
        {
            var list = new List<Operator>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT userid, username, pwd, usertype FROM operators ORDER BY userid";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Operator
                {
                    UserId = reader.GetString(0),
                    UserName = reader.GetString(1),
                    Pwd = reader.GetString(2),
                    UserType = reader.GetString(3)
                });
            }
            return list;
        }

        // ============================================================
        //  设备
        // ============================================================
        public Apparatus GetDefaultApparatus()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT apparatusid, innernumber, apparatusname, checkdatef, checkdatet, pidport, powerport, constpower FROM apparatus ORDER BY apparatusid LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Apparatus
                {
                    ApparatusId = ReadInt(reader, 0),
                    InnerNumber = reader.GetString(1),
                    ApparatusName = reader.GetString(2),
                    CheckDateF = ParseDate(reader, 3),
                    CheckDateT = ParseDate(reader, 4),
                    PidPort = reader.GetString(5),
                    PowerPort = reader.GetString(6),
                    ConstPower = reader.IsDBNull(7) ? (int?)null : ReadInt(reader, 7)
                };
            }
            return null;
        }

        public void UpdateApparatusConstPower(int apparatusId, int constPower)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE apparatus SET constpower=@p WHERE apparatusid=@id";
            cmd.Parameters.AddWithValue("@p", constPower);
            cmd.Parameters.AddWithValue("@id", apparatusId);
            cmd.ExecuteNonQuery();
        }

        // ============================================================
        //  样品
        // ============================================================
        public void UpsertProduct(ProductMaster p)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            // SQLite 的 UPSERT 语法：INSERT ... ON CONFLICT(主键) DO UPDATE SET ...
            cmd.CommandText = @"
                INSERT INTO productmaster (productid, productname, specific, diameter, height, flag)
                VALUES (@pid, @pname, @spec, @dia, @h, @flag)
                ON CONFLICT(productid) DO UPDATE SET
                    productname=excluded.productname,
                    specific=excluded.specific,
                    diameter=excluded.diameter,
                    height=excluded.height,
                    flag=excluded.flag";
            cmd.Parameters.AddWithValue("@pid", p.ProductId);
            cmd.Parameters.AddWithValue("@pname", p.ProductName);
            cmd.Parameters.AddWithValue("@spec", p.Specific);
            cmd.Parameters.AddWithValue("@dia", p.Diameter);
            cmd.Parameters.AddWithValue("@h", p.Height);
            cmd.Parameters.AddWithValue("@flag", (object)p.Flag ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public ProductMaster GetProduct(string productId)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT productid, productname, specific, diameter, height, flag FROM productmaster WHERE productid=@pid";
            cmd.Parameters.AddWithValue("@pid", productId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ProductMaster
                {
                    ProductId = reader.GetString(0),
                    ProductName = reader.GetString(1),
                    Specific = reader.GetString(2),
                    Diameter = reader.GetDouble(3),
                    Height = reader.GetDouble(4),
                    Flag = reader.IsDBNull(5) ? null : reader.GetString(5)
                };
            }
            return null;
        }

        // ============================================================
        //  试验主表 testmaster
        // ============================================================

        /// <summary>新建试验：写入基本信息，统计字段全部填 0（或空）。</summary>
        public void InsertTest(TestMaster t)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO testmaster
                  (productid, testid, testdate, ambtemp, ambhumi, according, operator,
                   apparatusid, apparatusname, apparatuschkdate, rptno,
                   preweight, postweight, lostweight, lostweight_per,
                   totaltesttime, constpower, phenocode, flametime, flameduration,
                   maxtf1,maxtf2,maxts,maxtc, maxtf1_time,maxtf2_time,maxts_time,maxtc_time,
                   finaltf1,finaltf2,finalts,finaltc,
                   finaltf1_time,finaltf2_time,finalts_time,finaltc_time,
                   deltatf1,deltatf2,deltatf,deltats,deltatc, memo, flag)
                VALUES
                  (@pid,@tid,@date,@ambt,@ambh,@acc,@op,
                   @appid,@appname,@appdate,@rptno,
                   @prewt,0,0,0,
                   0,@cp,'',0,0,
                   0,0,0,0, 0,0,0,0,
                   0,0,0,0, 0,0,0,0,
                   0,0,0,0,0, NULL, NULL)";
            cmd.Parameters.AddWithValue("@pid", t.ProductId);
            cmd.Parameters.AddWithValue("@tid", t.TestId);
            cmd.Parameters.AddWithValue("@date", t.TestDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ambt", t.AmbTemp);
            cmd.Parameters.AddWithValue("@ambh", t.AmbHumi);
            cmd.Parameters.AddWithValue("@acc", t.According ?? "ISO 11820:2022");
            cmd.Parameters.AddWithValue("@op", t.Operator);
            cmd.Parameters.AddWithValue("@appid", t.ApparatusId);
            cmd.Parameters.AddWithValue("@appname", t.ApparatusName);
            cmd.Parameters.AddWithValue("@appdate", t.ApparatusChkDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@rptno", t.RptNo);
            cmd.Parameters.AddWithValue("@prewt", t.PreWeight);
            cmd.Parameters.AddWithValue("@cp", t.ConstPower);
            cmd.ExecuteNonQuery();
        }

        /// <summary>试验完成后更新全部统计字段。</summary>
        public void UpdateTestResult(TestMaster t)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE testmaster SET
                    postweight=@post, lostweight=@lost, lostweight_per=@lostper,
                    totaltesttime=@tt, constpower=@cp, phenocode=@pheno,
                    flametime=@ftime, flameduration=@fdur,
                    maxtf1=@mtf1, maxtf2=@mtf2, maxts=@mts, maxtc=@mtc,
                    maxtf1_time=@mtf1t, maxtf2_time=@mtf2t, maxts_time=@mtst, maxtc_time=@mtct,
                    finaltf1=@ftf1, finaltf2=@ftf2, finalts=@fts, finaltc=@ftc,
                    finaltf1_time=@ftf1t, finaltf2_time=@ftf2t, finalts_time=@ftst, finaltc_time=@ftct,
                    deltatf1=@dtf1, deltatf2=@dtf2, deltatf=@dtf, deltats=@dts, deltatc=@dtc,
                    memo=@memo, flag='10000000'
                WHERE productid=@pid AND testid=@tid";
            cmd.Parameters.AddWithValue("@post", t.PostWeight);
            cmd.Parameters.AddWithValue("@lost", t.LostWeight);
            cmd.Parameters.AddWithValue("@lostper", t.LostWeightPer);
            cmd.Parameters.AddWithValue("@tt", t.TotalTestTime);
            cmd.Parameters.AddWithValue("@cp", t.ConstPower);
            cmd.Parameters.AddWithValue("@pheno", t.PhenoCode ?? "");
            cmd.Parameters.AddWithValue("@ftime", t.FlameTime);
            cmd.Parameters.AddWithValue("@fdur", t.FlameDuration);
            cmd.Parameters.AddWithValue("@mtf1", t.MaxTf1);
            cmd.Parameters.AddWithValue("@mtf2", t.MaxTf2);
            cmd.Parameters.AddWithValue("@mts", t.MaxTs);
            cmd.Parameters.AddWithValue("@mtc", t.MaxTc);
            cmd.Parameters.AddWithValue("@mtf1t", t.MaxTf1Time);
            cmd.Parameters.AddWithValue("@mtf2t", t.MaxTf2Time);
            cmd.Parameters.AddWithValue("@mtst", t.MaxTsTime);
            cmd.Parameters.AddWithValue("@mtct", t.MaxTcTime);
            cmd.Parameters.AddWithValue("@ftf1", t.FinalTf1);
            cmd.Parameters.AddWithValue("@ftf2", t.FinalTf2);
            cmd.Parameters.AddWithValue("@fts", t.FinalTs);
            cmd.Parameters.AddWithValue("@ftc", t.FinalTc);
            cmd.Parameters.AddWithValue("@ftf1t", t.FinalTf1Time);
            cmd.Parameters.AddWithValue("@ftf2t", t.FinalTf2Time);
            cmd.Parameters.AddWithValue("@ftst", t.FinalTsTime);
            cmd.Parameters.AddWithValue("@ftct", t.FinalTcTime);
            cmd.Parameters.AddWithValue("@dtf1", t.DeltaTf1);
            cmd.Parameters.AddWithValue("@dtf2", t.DeltaTf2);
            cmd.Parameters.AddWithValue("@dtf", t.DeltaTf);
            cmd.Parameters.AddWithValue("@dts", t.DeltaTs);
            cmd.Parameters.AddWithValue("@dtc", t.DeltaTc);
            cmd.Parameters.AddWithValue("@memo", (object)t.Memo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pid", t.ProductId);
            cmd.Parameters.AddWithValue("@tid", t.TestId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>试验结束但现象/质量尚未保存时，先落库完成时长，便于重启后继续保护和补录。</summary>
        public void MarkTestCompletedPending(string productId, string testId, int totalTestTime, int constPower)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE testmaster SET
                    totaltesttime=@tt,
                    constpower=@cp,
                    flag=NULL
                WHERE productid=@pid AND testid=@tid";
            cmd.Parameters.AddWithValue("@tt", totalTestTime);
            cmd.Parameters.AddWithValue("@cp", constPower);
            cmd.Parameters.AddWithValue("@pid", productId);
            cmd.Parameters.AddWithValue("@tid", testId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>查询某试验完整记录，不存在返回 null。</summary>
        public TestMaster GetTest(string productId, string testId)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT productid, testid, testdate, ambtemp, ambhumi, according, operator,
                       apparatusid, apparatusname, apparatuschkdate, rptno,
                       preweight, postweight, lostweight, lostweight_per,
                       totaltesttime, constpower, phenocode, flametime, flameduration,
                       maxtf1,maxtf2,maxts,maxtc, maxtf1_time,maxtf2_time,maxts_time,maxtc_time,
                       finaltf1,finaltf2,finalts,finaltc,
                       finaltf1_time,finaltf2_time,finalts_time,finaltc_time,
                       deltatf1,deltatf2,deltatf,deltats,deltatc, memo, flag
                FROM testmaster WHERE productid=@pid AND testid=@tid";
            cmd.Parameters.AddWithValue("@pid", productId);
            cmd.Parameters.AddWithValue("@tid", testId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return MapTest(reader);
        }

        /// <summary>查询是否有"已完成但未保存"的试验（用于 UI 保护）。</summary>
        public TestMaster GetLastUnsavedTest()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT productid, testid, testdate, ambtemp, ambhumi, according, operator,
                       apparatusid, apparatusname, apparatuschkdate, rptno,
                       preweight, postweight, lostweight, lostweight_per,
                       totaltesttime, constpower, phenocode, flametime, flameduration,
                       maxtf1,maxtf2,maxts,maxtc, maxtf1_time,maxtf2_time,maxts_time,maxtc_time,
                       finaltf1,finaltf2,finalts,finaltc,
                       finaltf1_time,finaltf2_time,finalts_time,finaltc_time,
                       deltatf1,deltatf2,deltatf,deltats,deltatc, memo, flag
                FROM testmaster
                WHERE totaltesttime > 0 AND (flag IS NULL OR flag <> '10000000')
                ORDER BY testid DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return MapTest(reader);
        }

        /// <summary>历史查询：按日期 + 样品编号模糊 + 操作员。</summary>
        public List<TestMaster> QueryTests(DateTime from, DateTime to, string productId = "", string op = "")
        {
            var list = new List<TestMaster>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            // SQLite 的 LIKE 拼接用 || 操作符（MySQL 用 CONCAT）
            cmd.CommandText = @"
                SELECT productid, testid, testdate, ambtemp, ambhumi, according, operator,
                       apparatusid, apparatusname, apparatuschkdate, rptno,
                       preweight, postweight, lostweight, lostweight_per,
                       totaltesttime, constpower, phenocode, flametime, flameduration,
                       maxtf1,maxtf2,maxts,maxtc, maxtf1_time,maxtf2_time,maxts_time,maxtc_time,
                       finaltf1,finaltf2,finalts,finaltc,
                       finaltf1_time,finaltf2_time,finalts_time,finaltc_time,
                       deltatf1,deltatf2,deltatf,deltats,deltatc, memo, flag
                FROM testmaster
                WHERE testdate BETWEEN @from AND @to
                  AND (@pid = '' OR productid LIKE '%' || @pid || '%')
                  AND (@op = '' OR operator = @op)
                ORDER BY testdate DESC, testid DESC";
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@pid", productId ?? "");
            cmd.Parameters.AddWithValue("@op", op ?? "");
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(MapTest(reader));
            return list;
        }

        /// <summary>删除试验记录；如果该样品已无其他试验，同步清理孤立样品。</summary>
        public bool DeleteTest(string productId, string testId)
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                int affected;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM testmaster WHERE productid=@pid AND testid=@tid";
                    cmd.Parameters.AddWithValue("@pid", productId);
                    cmd.Parameters.AddWithValue("@tid", testId);
                    affected = cmd.ExecuteNonQuery();
                }

                if (affected == 0)
                {
                    tx.Rollback();
                    return false;
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        DELETE FROM productmaster
                        WHERE productid=@pid
                          AND NOT EXISTS (
                              SELECT 1 FROM testmaster WHERE productid=@pid LIMIT 1
                          )";
                    cmd.Parameters.AddWithValue("@pid", productId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        private static TestMaster MapTest(SqliteDataReader r)
        {
            return new TestMaster
            {
                ProductId = r.GetString(0),
                TestId = r.GetString(1),
                TestDate = ParseDate(r, 2),
                AmbTemp = r.GetDouble(3),
                AmbHumi = r.GetDouble(4),
                According = ReadString(r, 5),
                Operator = r.GetString(6),
                ApparatusId = ReadString(r, 7),
                ApparatusName = ReadString(r, 8),
                ApparatusChkDate = ParseDate(r, 9),
                RptNo = ReadString(r, 10),
                PreWeight = r.GetDouble(11),
                PostWeight = r.GetDouble(12),
                LostWeight = r.GetDouble(13),
                LostWeightPer = r.GetDouble(14),
                TotalTestTime = ReadInt(r, 15),
                ConstPower = ReadInt(r, 16),
                PhenoCode = ReadString(r, 17),
                FlameTime = ReadInt(r, 18),
                FlameDuration = ReadInt(r, 19),
                MaxTf1 = r.GetDouble(20), MaxTf2 = r.GetDouble(21), MaxTs = r.GetDouble(22), MaxTc = r.GetDouble(23),
                MaxTf1Time = ReadInt(r, 24), MaxTf2Time = ReadInt(r, 25), MaxTsTime = ReadInt(r, 26), MaxTcTime = ReadInt(r, 27),
                FinalTf1 = r.GetDouble(28), FinalTf2 = r.GetDouble(29), FinalTs = r.GetDouble(30), FinalTc = r.GetDouble(31),
                FinalTf1Time = ReadInt(r, 32), FinalTf2Time = ReadInt(r, 33), FinalTsTime = ReadInt(r, 34), FinalTcTime = ReadInt(r, 35),
                DeltaTf1 = r.GetDouble(36), DeltaTf2 = r.GetDouble(37),
                DeltaTf = r.GetDouble(38), DeltaTs = r.GetDouble(39), DeltaTc = r.GetDouble(40),
                Memo = r.IsDBNull(41) ? null : ReadString(r, 41),
                Flag = r.IsDBNull(42) ? null : ReadString(r, 42)
            };
        }

        // ============================================================
        //  传感器
        // ============================================================
        public List<Sensor> GetSensors()
        {
            var list = new List<Sensor>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sensorid, sensorname, dispname, sensorgroup, unit, discription, flag, signalzero, signalspan, outputzero, outputspan, outputvalue, inputvalue, signaltype FROM sensors ORDER BY sensorid";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Sensor
                {
                    SensorId = ReadInt(reader, 0),
                    SensorName = reader.GetString(1),
                    DispName = reader.GetString(2),
                    SensorGroup = reader.GetString(3),
                    Unit = reader.GetString(4),
                    Discription = reader.GetString(5),
                    Flag = reader.GetString(6),
                    SignalZero = reader.GetDouble(7),
                    SignalSpan = reader.GetDouble(8),
                    OutputZero = reader.GetDouble(9),
                    OutputSpan = reader.GetDouble(10),
                    OutputValue = reader.GetDouble(11),
                    InputValue = reader.GetDouble(12),
                    SignalType = reader.GetInt32(13)
                });
            }
            return list;
        }

        // ============================================================
        //  校准记录
        // ============================================================
        public void InsertCalibration(CalibrationRecord c)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO CalibrationRecords
                  (Id, CalibrationDate, CalibrationType, ApparatusId, Operator,
                   TemperatureData, UniformityResult, MaxDeviation, AverageTemperature,
                   PassedCriteria, Remarks, CreatedAt,
                   TempA1,TempA2,TempA3, TempB1,TempB2,TempB3, TempC1,TempC2,TempC3,
                   TAvg, TAvgAxis1,TAvgAxis2,TAvgAxis3, TAvgLevela,TAvgLevelb,TAvgLevelc,
                   TDevAxis1,TDevAxis2,TDevAxis3, TDevLevela,TDevLevelb,TDevLevelc,
                   TAvgDevAxis, TAvgDevLevel, CenterTempData, Memo)
                VALUES
                  (@id,@date,@type,@app,@op,
                   @td,@ur,@md,@at,
                   @pc,@rm,@ca,
                   @a1,@a2,@a3, @b1,@b2,@b3, @c1,@c2,@c3,
                   @tavg, @ax1,@ax2,@ax3, @la,@lb,@lc,
                   @dx1,@dx2,@dx3, @dl1,@dl2,@dl3,
                   @dax, @dlv, @ctd, @memo)";
            cmd.Parameters.AddWithValue("@id", c.Id);
            cmd.Parameters.AddWithValue("@date", c.CalibrationDate);
            cmd.Parameters.AddWithValue("@type", c.CalibrationType);
            cmd.Parameters.AddWithValue("@app", c.ApparatusId);
            cmd.Parameters.AddWithValue("@op", c.Operator);
            cmd.Parameters.AddWithValue("@td", string.IsNullOrWhiteSpace(c.TemperatureData) ? "[]" : c.TemperatureData);
            cmd.Parameters.AddWithValue("@ur", (object)c.UniformityResult ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@md", (object)c.MaxDeviation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@at", (object)c.AverageTemperature ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pc", c.PassedCriteria);
            cmd.Parameters.AddWithValue("@rm", c.Remarks ?? "");
            cmd.Parameters.AddWithValue("@ca", c.CreatedAt);
            cmd.Parameters.AddWithValue("@a1", (object)c.TempA1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a2", (object)c.TempA2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a3", (object)c.TempA3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@b1", (object)c.TempB1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@b2", (object)c.TempB2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@b3", (object)c.TempB3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@c1", (object)c.TempC1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@c2", (object)c.TempC2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@c3", (object)c.TempC3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tavg", (object)c.TAvg ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ax1", (object)c.TAvgAxis1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ax2", (object)c.TAvgAxis2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ax3", (object)c.TAvgAxis3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@la", (object)c.TAvgLevela ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lb", (object)c.TAvgLevelb ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lc", (object)c.TAvgLevelc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dx1", (object)c.TDevAxis1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dx2", (object)c.TDevAxis2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dx3", (object)c.TDevAxis3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dl1", (object)c.TDevLevela ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dl2", (object)c.TDevLevelb ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dl3", (object)c.TDevLevelc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dax", (object)c.TAvgDevAxis ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dlv", (object)c.TAvgDevLevel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ctd", (object)c.CenterTempData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@memo", (object)c.Memo ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public List<CalibrationRecord> QueryCalibrations(DateTime from, DateTime to)
        {
            var list = new List<CalibrationRecord>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            // CalibrationDate 存的是 ISO8601 文本（如 2024-06-13 10:00:00），
            // 取前 10 位（日期部分）做 BETWEEN 比较。
            cmd.CommandText = @"
                SELECT Id, CalibrationDate, CalibrationType, ApparatusId, Operator,
                       TemperatureData, UniformityResult, MaxDeviation, AverageTemperature,
                       PassedCriteria, Remarks, CreatedAt,
                       TempA1,TempA2,TempA3, TempB1,TempB2,TempB3, TempC1,TempC2,TempC3,
                       TAvg, TAvgAxis1,TAvgAxis2,TAvgAxis3, TAvgLevela,TAvgLevelb,TAvgLevelc,
                       TDevAxis1,TDevAxis2,TDevAxis3, TDevLevela,TDevLevelb,TDevLevelc,
                       TAvgDevAxis, TAvgDevLevel, CenterTempData, Memo
                FROM CalibrationRecords
                WHERE substr(CalibrationDate, 1, 10) BETWEEN @from AND @to
                ORDER BY CalibrationDate DESC";
            cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd"));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new CalibrationRecord
                {
                    Id = ReadString(reader, 0),
                    CalibrationDate = ReadString(reader, 1),
                    CalibrationType = ReadString(reader, 2),
                    ApparatusId = ReadInt(reader, 3),
                    Operator = ReadString(reader, 4),
                    TemperatureData = reader.IsDBNull(5) ? null : ReadString(reader, 5),
                    UniformityResult = reader.IsDBNull(6) ? (double?)null : reader.GetDouble(6),
                    MaxDeviation = reader.IsDBNull(7) ? (double?)null : reader.GetDouble(7),
                    AverageTemperature = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8),
                    PassedCriteria = ReadInt(reader, 9),
                    Remarks = ReadString(reader, 10),
                    CreatedAt = ReadString(reader, 11),
                    TempA1 = reader.IsDBNull(12) ? (double?)null : reader.GetDouble(12),
                    TempA2 = reader.IsDBNull(13) ? (double?)null : reader.GetDouble(13),
                    TempA3 = reader.IsDBNull(14) ? (double?)null : reader.GetDouble(14),
                    TempB1 = reader.IsDBNull(15) ? (double?)null : reader.GetDouble(15),
                    TempB2 = reader.IsDBNull(16) ? (double?)null : reader.GetDouble(16),
                    TempB3 = reader.IsDBNull(17) ? (double?)null : reader.GetDouble(17),
                    TempC1 = reader.IsDBNull(18) ? (double?)null : reader.GetDouble(18),
                    TempC2 = reader.IsDBNull(19) ? (double?)null : reader.GetDouble(19),
                    TempC3 = reader.IsDBNull(20) ? (double?)null : reader.GetDouble(20),
                    TAvg = reader.IsDBNull(21) ? (double?)null : reader.GetDouble(21),
                    TAvgAxis1 = reader.IsDBNull(22) ? (double?)null : reader.GetDouble(22),
                    TAvgAxis2 = reader.IsDBNull(23) ? (double?)null : reader.GetDouble(23),
                    TAvgAxis3 = reader.IsDBNull(24) ? (double?)null : reader.GetDouble(24),
                    TAvgLevela = reader.IsDBNull(25) ? (double?)null : reader.GetDouble(25),
                    TAvgLevelb = reader.IsDBNull(26) ? (double?)null : reader.GetDouble(26),
                    TAvgLevelc = reader.IsDBNull(27) ? (double?)null : reader.GetDouble(27),
                    TDevAxis1 = reader.IsDBNull(28) ? (double?)null : reader.GetDouble(28),
                    TDevAxis2 = reader.IsDBNull(29) ? (double?)null : reader.GetDouble(29),
                    TDevAxis3 = reader.IsDBNull(30) ? (double?)null : reader.GetDouble(30),
                    TDevLevela = reader.IsDBNull(31) ? (double?)null : reader.GetDouble(31),
                    TDevLevelb = reader.IsDBNull(32) ? (double?)null : reader.GetDouble(32),
                    TDevLevelc = reader.IsDBNull(33) ? (double?)null : reader.GetDouble(33),
                    TAvgDevAxis = reader.IsDBNull(34) ? (double?)null : reader.GetDouble(34),
                    TAvgDevLevel = reader.IsDBNull(35) ? (double?)null : reader.GetDouble(35),
                    CenterTempData = reader.IsDBNull(36) ? null : ReadString(reader, 36),
                    Memo = reader.IsDBNull(37) ? null : ReadString(reader, 37),
                });
            }
            return list;
        }

        // 字符串日期辅助
        public static string Fmt(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static int ReadInt(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return 0;
            var value = reader.GetValue(ordinal);
            return value switch
            {
                int i => i,
                long l => (int)l,
                short s => s,
                byte b => b,
                decimal d => (int)d,
                double d => (int)Math.Round(d),
                float f => (int)Math.Round(f),
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => Convert.ToInt32(value, CultureInfo.InvariantCulture)
            };
        }

        private static string ReadString(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return "";
            var value = reader.GetValue(ordinal);
            return value switch
            {
                DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
        }

        /// <summary>
        /// SQLite 日期以 TEXT(yyyy-MM-dd) 存储，读回时统一转 DateTime。
        /// 兼容 "yyyy-MM-dd" 与 "yyyy-MM-dd HH:mm:ss" 两种格式。
        /// </summary>
        private static DateTime ParseDate(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return DateTime.Today;
            var s = reader.GetString(ordinal);
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d))
                return d;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
            return DateTime.Today;
        }
    }
}
