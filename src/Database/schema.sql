-- ============================================================
--  ISO11820 仿真系统  SQLite 建表脚本
--  说明: 程序首次启动时若库内无 operators 表，会自动执行此脚本
--  注意: SQLite 不支持 ON DUPLICATE KEY / MODIFY COLUMN / ENGINE 等
--        幂等性靠 CREATE TABLE IF NOT EXISTS 保证
--        PRAGMA foreign_keys = ON 在连接层开启
-- ============================================================

-- ---------- operators 操作员表 ----------
-- 按文档保留"无主键"特性，登录按 username + pwd 校验
CREATE TABLE IF NOT EXISTS operators (
    userid    TEXT NOT NULL,   -- 用户ID（初始化为 1、2）
    username  TEXT NOT NULL,   -- 登录用户名 admin / experimenter
    pwd       TEXT NOT NULL,   -- 密码哈希(SHA256+盐，64位十六进制)；兼容历史明文
    usertype  TEXT NOT NULL    -- 角色: admin 或 operator
);

-- ---------- apparatus 设备表 ----------
CREATE TABLE IF NOT EXISTS apparatus (
    apparatusid   INTEGER PRIMARY KEY AUTOINCREMENT,  -- 设备ID
    innernumber   TEXT NOT NULL,   -- 设备内部编号，如 FURNACE-01
    apparatusname TEXT NOT NULL,   -- 设备名称，如 一号试验炉
    checkdatef    TEXT NOT NULL,   -- 检定有效期开始 (yyyy-MM-dd)
    checkdatet    TEXT NOT NULL,   -- 检定有效期结束 (yyyy-MM-dd)
    pidport       TEXT NOT NULL,   -- PID串口，如 COM9
    powerport     TEXT NOT NULL,   -- 功率串口，如 COM9
    constpower    INTEGER          -- 上次记录的恒功率值（可空）
);

-- ---------- productmaster 样品表 ----------
CREATE TABLE IF NOT EXISTS productmaster (
    productid   TEXT PRIMARY KEY,   -- 样品编号，如 20240613-001
    productname TEXT NOT NULL,      -- 样品名称，如 岩棉隔热板
    specific    TEXT NOT NULL,      -- 规格型号，如 100x50x25mm
    diameter    REAL NOT NULL,      -- 直径（mm）
    height      REAL NOT NULL,      -- 高度（mm）
    flag        TEXT                -- 备用字段
);

-- ---------- testmaster 试验记录表（核心） ----------
-- 联合主键 (productid, testid)；productid 外键 → productmaster
-- SQLite 用 REAL 存浮点，INTEGER 存整数；日期统一存 TEXT(yyyy-MM-dd)
CREATE TABLE IF NOT EXISTS testmaster (
    -- ===== 基本信息 =====
    productid        TEXT NOT NULL,   -- 样品编号
    testid           TEXT NOT NULL,   -- 试验ID yyyyMMdd-HHmmss
    testdate         TEXT NOT NULL,   -- 试验日期
    ambtemp          REAL NOT NULL,   -- 环境温度（℃）
    ambhumi          REAL NOT NULL,   -- 环境湿度（%）
    according        TEXT NOT NULL,   -- 试验依据，如 ISO 11820:2022
    operator         TEXT NOT NULL,   -- 操作员用户名
    apparatusid      TEXT NOT NULL,   -- 设备编号
    apparatusname    TEXT NOT NULL,   -- 设备名称（冗余）
    apparatuschkdate TEXT NOT NULL,   -- 设备检定日期
    rptno            TEXT NOT NULL,   -- 报告编号

    -- ===== 质量数据 =====
    preweight        REAL NOT NULL,   -- 试验前质量（g）
    postweight       REAL NOT NULL,   -- 试验后质量（g）
    lostweight       REAL NOT NULL,   -- 失重量 = pre - post
    lostweight_per   REAL NOT NULL,   -- 【判定项】失重率（%）

    -- ===== 试验过程 =====
    totaltesttime    INTEGER NOT NULL,  -- 总试验时长（秒）
    constpower       INTEGER NOT NULL,  -- 恒功率值（0~25600）
    phenocode        TEXT NOT NULL,     -- 现象编码（勾选项序列化字符串）
    flametime        INTEGER NOT NULL,  -- 火焰开始时刻（秒，无火焰填0）
    flameduration    INTEGER NOT NULL,  -- 火焰持续时间（秒，无火焰填0）

    -- ===== 各通道温度最大值 =====
    maxtf1           REAL NOT NULL,
    maxtf2           REAL NOT NULL,
    maxts            REAL NOT NULL,
    maxtc            REAL NOT NULL,
    maxtf1_time      INTEGER NOT NULL,
    maxtf2_time      INTEGER NOT NULL,
    maxts_time       INTEGER NOT NULL,
    maxtc_time       INTEGER NOT NULL,

    -- ===== 各通道温度最终值（试验结束时刻）=====
    finaltf1         REAL NOT NULL,
    finaltf2         REAL NOT NULL,
    finalts          REAL NOT NULL,
    finaltc          REAL NOT NULL,
    finaltf1_time    INTEGER NOT NULL,
    finaltf2_time    INTEGER NOT NULL,
    finalts_time     INTEGER NOT NULL,
    finaltc_time     INTEGER NOT NULL,

    -- ===== 温升（结束值 - 开始值）=====
    deltatf1         REAL NOT NULL,  -- 炉温1温升
    deltatf2         REAL NOT NULL,  -- 炉温2温升
    deltatf          REAL NOT NULL,  -- 【判定项】样品温升（取表面温升 deltats）
    deltats          REAL NOT NULL,  -- 表面温温升
    deltatc          REAL NOT NULL,  -- 中心温温升

    -- ===== 备注 =====
    memo             TEXT,            -- 备注
    flag             TEXT,            -- 完成标记，10000000 表示已保存

    PRIMARY KEY (productid, testid),
    FOREIGN KEY (productid) REFERENCES productmaster (productid)
        ON DELETE RESTRICT ON UPDATE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Testmaster_Testdate           ON testmaster (testdate);
CREATE INDEX IF NOT EXISTS IX_Testmaster_Operator           ON testmaster (operator);
CREATE INDEX IF NOT EXISTS IX_Testmaster_Testdate_Productid ON testmaster (testdate, productid);

-- ---------- sensors 传感器配置表 ----------
CREATE TABLE IF NOT EXISTS sensors (
    sensorid    INTEGER PRIMARY KEY,  -- 传感器ID
    sensorname  TEXT NOT NULL,        -- 传感器代号，如 TF1
    dispname    TEXT NOT NULL,        -- 显示名，如 炉内温度1
    sensorgroup TEXT NOT NULL,        -- 分组标识
    unit        TEXT NOT NULL,        -- 单位，如 ℃
    discription TEXT NOT NULL,        -- 描述
    flag        TEXT NOT NULL,        -- 标记（启用/停用）
    signalzero  REAL NOT NULL,        -- 信号零点
    signalspan  REAL NOT NULL,        -- 信号量程
    outputzero  REAL NOT NULL,        -- 输出温度下限
    outputspan  REAL NOT NULL,        -- 输出温度上限
    outputvalue REAL NOT NULL,        -- 当前温度值（运行时更新）
    inputvalue  REAL NOT NULL,        -- 当前输入值（运行时更新）
    signaltype  INTEGER NOT NULL      -- 信号类型 4=数字量
);

-- ---------- CalibrationRecords 校准记录表 ----------
-- 注意: 表名 PascalCase，与其他表小写不同
CREATE TABLE IF NOT EXISTS CalibrationRecords (
    Id                 TEXT PRIMARY KEY,   -- GUID
    CalibrationDate    TEXT NOT NULL,      -- 校准日期时间 ISO8601
    CalibrationType    TEXT NOT NULL,      -- 类型: Surface 或 Center
    ApparatusId        INTEGER NOT NULL,   -- 设备ID
    Operator           TEXT NOT NULL,      -- 操作员
    TemperatureData    TEXT,               -- JSON 字符串（SQLite 无原生 JSON 类型）
    UniformityResult   REAL,
    MaxDeviation       REAL,
    AverageTemperature REAL,
    PassedCriteria     INTEGER NOT NULL,   -- 0=未通过 1=通过
    Remarks            TEXT NOT NULL,
    CreatedAt          TEXT NOT NULL,

    -- 炉壁 9 测温点（A/B/C 层 × 1/2/3 轴）
    TempA1 REAL, TempA2 REAL, TempA3 REAL,
    TempB1 REAL, TempB2 REAL, TempB3 REAL,
    TempC1 REAL, TempC2 REAL, TempC3 REAL,

    -- 计算结果
    TAvg        REAL,  -- 总均温
    TAvgAxis1   REAL, TAvgAxis2 REAL, TAvgAxis3 REAL,
    TAvgLevela  REAL, TAvgLevelb REAL, TAvgLevelc REAL,
    TDevAxis1   REAL, TDevAxis2 REAL, TDevAxis3 REAL,
    TDevLevela  REAL, TDevLevelb REAL, TDevLevelc REAL,
    TAvgDevAxis REAL, TAvgDevLevel REAL,

    CenterTempData TEXT,  -- 中心轴JSON数据（可空）
    Memo           TEXT
);

CREATE INDEX IF NOT EXISTS IX_CalibrationRecord_Date     ON CalibrationRecords (CalibrationDate);
CREATE INDEX IF NOT EXISTS IX_CalibrationRecord_Operator ON CalibrationRecords (Operator);
