using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Iso11820Simulator.Configuration
{
    /// <summary>
    /// 强类型配置根，对应 appsettings.json 的结构。
    /// </summary>
    public class AppSettings
    {
        public DatabaseSection Database { get; set; } = new();
        public HardwareSection Hardware { get; set; } = new();
        public SimulationSection Simulation { get; set; } = new();
        public FileStorageSection FileStorage { get; set; } = new();
        public ReportSection Report { get; set; } = new();

        public static AppSettings Load()
        {
            // 注意：用 System.AppContext 全限定名，避免与本项目的 Global.AppContext 类冲突
            var basePath = System.AppContext.BaseDirectory;
            var cfg = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var settings = new AppSettings();
            cfg.GetSection("Database").Bind(settings.Database);
            cfg.GetSection("Hardware").Bind(settings.Hardware);
            cfg.GetSection("Simulation").Bind(settings.Simulation);
            cfg.GetSection("FileStorage").Bind(settings.FileStorage);
            cfg.GetSection("Report").Bind(settings.Report);

            // 文件输出路径：若为相对路径，统一以程序运行目录为基准解析成绝对路径，
            // 避免双击 exe 与从命令行启动时工作目录不同导致输出落到意外位置。
            settings.FileStorage.BaseDirectory = ResolvePath(basePath, settings.FileStorage.BaseDirectory, "Output");
            if (string.IsNullOrWhiteSpace(settings.FileStorage.TestDataDirectory))
                settings.FileStorage.TestDataDirectory = Path.Combine(settings.FileStorage.BaseDirectory, "TestData");
            else
                settings.FileStorage.TestDataDirectory = ResolvePath(basePath, settings.FileStorage.TestDataDirectory, null);

            settings.Report.OutputDirectory = ResolvePath(basePath, settings.Report.OutputDirectory, "Reports");

            return settings;

            static string ResolvePath(string basePath, string path, string defaultName)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return defaultName == null ? basePath : Path.Combine(basePath, defaultName);
                return Path.IsPathRooted(path)
                    ? path
                    : Path.GetFullPath(Path.Combine(basePath, path));
            }
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(FileStorage.TestDataDirectory))
                FileStorage.TestDataDirectory = Path.Combine(FileStorage.BaseDirectory, "TestData");

            var path = Path.Combine(System.AppContext.BaseDirectory, "appsettings.json");
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
    }

    public class DatabaseSection
    {
        public string Provider { get; set; } = "Sqlite";
        public string ConnectionString { get; set; } = "Data Source=iso11820.db";
    }

    public class HardwareSection
    {
        public int ConstPower { get; set; } = 2048;
        public double PidTemperature { get; set; } = 750;
        public string SensorProtocol { get; set; } = "ModbusRtu";
    }

    public class SimulationSection
    {
        public bool EnableSimulation { get; set; } = true;
        public bool SimulateSensors { get; set; } = true;
        public bool SimulatePidController { get; set; } = true;
        public double InitialFurnaceTemp { get; set; } = 720.0;
        public double TargetFurnaceTemp { get; set; } = 750.0;
        public double HeatingRatePerSecond { get; set; } = 40.0;
        public double TempFluctuation { get; set; } = 0.5;
        public double StableThreshold { get; set; } = 3.0;
        public int StableTickCount { get; set; } = 3;
        public bool SimulateFlame { get; set; } = false;
        public double MaxTemperatureDriftPerTenMinutes { get; set; } = 2.0;
        public int TickIntervalMs { get; set; } = 800;
    }

    public class FileStorageSection
    {
        public string BaseDirectory { get; set; } = "Output";
        public string TestDataDirectory { get; set; } = "";
    }

    public class ReportSection
    {
        public string OutputDirectory { get; set; } = "Output\\Reports";
        public bool EnablePdfExport { get; set; } = true;
    }
}
