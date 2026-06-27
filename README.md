# ISO 11820 建材不燃性试验仿真系统

Windows 桌面应用（.NET 8 / WinForms），用仿真引擎替代真实硬件，完整模拟建材不燃性试验流程：升温 → 稳定 → 记录 → 完成保存 → 报告导出。

数据库使用 **SQLite（`Microsoft.Data.Sqlite` + `e_sqlite3.dll`）**，零配置：文件库 `iso11820.db` 会在程序首次启动时自动生成在运行目录，**无需安装或配置任何数据库服务**。

## 一、运行环境

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11 x64 |
| .NET SDK | **8.0** |
| 数据库 | SQLite（随程序自带，无需额外安装） |

## 二、首次使用步骤

### 1) 安装 .NET 8 SDK

如果命令行执行 `dotnet --version` 报错"No .NET SDKs were found"，说明只装了运行时，需要装 SDK：

```bat
winget install Microsoft.DotNet.SDK.8
```

或手动下载：https://dotnet.microsoft.com/download/dotnet/8.0 → 选 **SDK x64** 安装。

装完重开命令行，确认 `dotnet --version` 能输出版本号（如 `8.0.x`）。

### 2) 确认数据库（无需安装）

本项目使用 SQLite，不需要 MySQL 等外部数据库服务。程序首次启动会自动在运行目录生成 `iso11820.db`，并执行 `src/Database/schema.sql`（建表）和 `seed.sql`（初始账号/设备/传感器），整个过程无需人工干预。

### 3) 连接字符串（通常无需修改）

`appsettings.json` 默认配置如下，绝大多数情况直接使用即可：

```json
"Provider": "Sqlite",
"ConnectionString": "Data Source=iso11820.db"
```

如需把数据库文件放到指定位置或目录，可改为绝对/相对路径，`DbHelper` 会自动创建缺失的目录：

```json
"ConnectionString": "Data Source=.\\db\\iso11820.db"
```

### 4) 编译并运行

在工程目录（`Iso11820Simulator.csproj` 所在目录）下执行：

```bat
cd /d E:\code\工程实践\一\Iso11820Simulator
dotnet restore
dotnet run
```

第一次会自动下载 NuGet 包（Microsoft.Data.Sqlite / OxyPlot / EPPlus / PDFsharp 等），稍等 1~2 分钟。

### 5) 登录

| 角色 | 用户名 | 密码 |
|------|--------|------|
| 管理员 | admin | 123456 |
| 试验员 | experimenter | 123456 |

> **密码存储**：密码在数据库中以 `SHA256(盐 + 用户名 + 密码)` 的 64 位十六进制哈希存储（见 `seed.sql`），不再明文落库。`PasswordHasher.Verify` 同时兼容历史明文记录，旧库升级后仍可登录。

## 三、操作流程（对应开发文档 §1.3）

1. 登录 → 主界面
2. 点 **新建试验** → 填样品信息 + 试验前质量 → 创建
3. 点 **开始升温** → 炉温从 720°C 升到 750°C（约 1 秒，仿真很快）
4. 温度稳定后状态变 **就绪** → 点 **开始记录**
5. 计时器开始走，曲线实时滚动
6. 标准 60 分钟模式会到 3600 秒自动结束（演示时可点 **停止记录** 提前结束）
7. 状态变 **完成** → 点 **试验记录** → 填试验后质量 + 现象 → 保存
8. 自动生成 CSV / Excel / PDF（路径见 `Output\Reports`）
9. 切 **记录查询** Tab 查看历史

## 四、文件输出位置

| 类型 | 路径 |
|------|------|
| 逐秒温度 CSV | `Output\TestData\{样品编号}\{试验编号}\sensor_data.csv` |
| Excel 报告 | `Output\Reports\{试验编号}_报告.xlsx` |
| PDF 报告 | `Output\Reports\{试验编号}_报告.pdf` |
| 运行日志 | `bin\...\logs\app-yyyyMMdd.log` |

路径可在 `appsettings.json` 的 `FileStorage.BaseDirectory` 和 `Report.OutputDirectory` 中修改。

## 五、工程结构

```
Iso11820Simulator/
├── Iso11820Simulator.csproj        # 工程文件 + NuGet 引用
├── Program.cs                      # 入口
├── appsettings.json                # 配置（连接串/仿真参数/路径）
├── README.md                       # 本文件
└── src/
    ├── Configuration/AppSettings.cs     # 强类型配置
    ├── Database/
    │   ├── schema.sql                   # SQLite 建表（自动执行）
    │   ├── seed.sql                     # 初始数据（自动执行）
    │   └── DbHelper.cs                  # 全部 SQL 操作
    ├── Core/
    │   ├── TestState.cs                 # 5 状态枚举
    │   ├── SensorSimulator.cs           # 仿真引擎（5 通道）
    │   ├── TestController.cs            # 状态机
    │   ├── DataBroadcastEventArgs.cs    # 跨线程事件载体
    │   └── MasterMessage.cs             # 消息结构
    ├── Services/
    │   ├── DaqWorker.cs                 # 每 800ms 后台采集
    │   ├── ExportService.cs             # CSV/Excel/PDF 导出
    │   └── DriftCalculator.cs           # 温漂线性回归
    ├── Models/                          # 实体类
    ├── Forms/                           # WinForms 窗体
    │   ├── LoginForm.cs                 # 登录
    │   ├── MainForm.cs                  # 主界面（曲线/状态/按钮）
    │   ├── NewTestForm.cs               # 新建试验
    │   ├── TestRecordForm.cs            # 试验现象记录
    │   ├── HistoryForm.cs               # 历史查询
    │   ├── CalibrationForm.cs           # 设备校准
    │   └── SettingsForm.cs              # 参数设置
    └── Global/AppContext.cs             # 全局单例容器
```

## 六、关键设计

- **状态机**：`Idle → Preparing → Ready → Recording → Complete → Preparing`，详见 `TestController.cs`
- **仿真算法**：升温阶段 `TF1 += HeatingRatePerSecond*0.8`；稳定钳位 750°C；记录阶段表面温指数接近炉温×0.95（详见文档 §2.3）
- **跨线程通信**：`DaqWorker` 后台线程触发 `DataBroadcast` 事件，UI 用 `Invoke` 切回，避免跨线程异常
- **未保存保护**：`totaltesttime>0 且 flag≠10000000` 时禁止新建试验和重新记录
- **判定结论**：样品温升 ≤ 50℃ 且 失重率 ≤ 50% 且 火焰持续 < 5s → 通过

## 七、常见问题

**Q: 启动报"数据库连接失败"**
A: SQLite 是文件库，检查程序运行目录是否有读写权限、`iso11820.db` 是否被占用（如被其他进程打开或只读）。删除该文件后重启，程序会自动重建库与种子数据。

**Q: 曲线不动**
A: 必须先 **新建试验** 才能点 **开始升温**（按钮才会启用）。

**Q: 升温太快/太慢**
A: 改 `appsettings.json` 的 `Simulation.HeatingRatePerSecond`（默认 40，越大越快）。

**Q: EPPlus 报许可证错误**
A: 已在 `ExportService.cs` 中设置 `LicenseContext.NonCommercial`，正常使用不会触发。
