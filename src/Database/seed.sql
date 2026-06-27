-- ============================================================
--  ISO11820 仿真系统  初始数据（程序首次启动时写入）
--  使用 INSERT ... WHERE NOT EXISTS 模式，可重复执行（幂等）
--  注意: SQLite 无 CURDATE()/DATE_ADD()/CONCAT()，用 date() 函数和 || 拼接
--        SQLite 不支持反引号，标识符不加分隔符即可
-- ============================================================

-- 操作员（不重复插入）
-- 密码以 SHA256(盐 + 用户名 + ":" + 密码) 的 64 位十六进制存储，不再明文落库。
--   admin/123456        -> 8cf3c5b42c77d68a18cb4795f7172d2e71c94fe8c048f6d9091a64069d1b8e5d
--   experimenter/123456 -> 73455b1b2d1f0f263d928c5180db55805079d4ddbbbf3cc23e6af833f58d14d9
-- 注意：DbHelper.PasswordHasher.Verify 同时兼容历史明文，旧库升级后仍可登录。
INSERT INTO operators (userid, username, pwd, usertype)
SELECT '1', 'admin', '8cf3c5b42c77d68a18cb4795f7172d2e71c94fe8c048f6d9091a64069d1b8e5d', 'admin'
WHERE NOT EXISTS (SELECT 1 FROM operators WHERE username = 'admin');

INSERT INTO operators (userid, username, pwd, usertype)
SELECT '2', 'experimenter', '73455b1b2d1f0f263d928c5180db55805079d4ddbbbf3cc23e6af833f58d14d9', 'operator'
WHERE NOT EXISTS (SELECT 1 FROM operators WHERE username = 'experimenter');

-- 设备（仅当表为空时插入）
-- SQLite: date('now') 取今天，date('now','+1 year') 加一年
INSERT INTO apparatus
    (apparatusid, innernumber, apparatusname, checkdatef, checkdatet, pidport, powerport, constpower)
SELECT 1, 'FURNACE-01', '一号试验炉',
       date('now'), date('now', '+1 year'),
       'COM9', 'COM9', 2048
WHERE NOT EXISTS (SELECT 1 FROM apparatus LIMIT 1);

-- 传感器：业务主要 0/1/2/3/16，其余 4~15 备用
INSERT INTO sensors
    (sensorid, sensorname, dispname, sensorgroup, unit, discription, flag,
     signalzero, signalspan, outputzero, outputspan, outputvalue, inputvalue, signaltype)
SELECT 0, 'Sensor0', '炉温1',     '采集', '℃', '炉温1',     '启用', 0,0,0,1000,0,0,4
WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = 0);

INSERT INTO sensors
    (sensorid, sensorname, dispname, sensorgroup, unit, discription, flag,
     signalzero, signalspan, outputzero, outputspan, outputvalue, inputvalue, signaltype)
SELECT 1, 'Sensor1', '炉温2',     '采集', '℃', '炉温2',     '启用', 0,0,0,1000,0,0,4
WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = 1);

INSERT INTO sensors
    (sensorid, sensorname, dispname, sensorgroup, unit, discription, flag,
     signalzero, signalspan, outputzero, outputspan, outputvalue, inputvalue, signaltype)
SELECT 2, 'Sensor2', '表面温度',   '采集', '℃', '表面温度',   '启用', 0,0,0,1000,0,0,4
WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = 2);

INSERT INTO sensors
    (sensorid, sensorname, dispname, sensorgroup, unit, discription, flag,
     signalzero, signalspan, outputzero, outputspan, outputvalue, inputvalue, signaltype)
SELECT 3, 'Sensor3', '中心温度',   '采集', '℃', '中心温度',   '启用', 0,0,0,1000,0,0,4
WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = 3);

INSERT INTO sensors
    (sensorid, sensorname, dispname, sensorgroup, unit, discription, flag,
     signalzero, signalspan, outputzero, outputspan, outputvalue, inputvalue, signaltype)
SELECT 16, 'Sensor16', '校准温度', '校准', '℃', '校准温度',   '启用', 0,0,0,1000,0,0,4
WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = 16);

-- 4 ~ 15 备用通道，显示名 "备用通道{sensorId+1}"
-- SQLite: 用递归 CTE 生成 4..15 序列（替代 MySQL 的 UNION ALL 派生表）
INSERT INTO sensors
    (sensorid, sensorname, dispname, sensorgroup, unit, discription, flag,
     signalzero, signalspan, outputzero, outputspan, outputvalue, inputvalue, signaltype)
SELECT n,
       'Sensor' || n,
       '备用通道' || (n + 1),
       '备用', '℃',
       '备用通道' || (n + 1),
       '停用', 0,0,0,1000,0,0,4
FROM (
    WITH RECURSIVE seq(n) AS (
        SELECT 4 UNION ALL SELECT n + 1 FROM seq WHERE n < 15
    )
    SELECT n FROM seq
) s
WHERE NOT EXISTS (SELECT 1 FROM sensors WHERE sensorid = s.n);
