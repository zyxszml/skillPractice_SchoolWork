using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iso11820Simulator.Global;

namespace Iso11820Simulator.Services
{
    /// <summary>
    /// 试验相关本地文件清理（数据目录、CSV、曲线图、Excel/PDF 报告）。
    /// 抽离自 MainForm 与 HistoryForm 中重复的删除逻辑，集中维护。
    /// </summary>
    public static class RecordFileCleanup
    {
        /// <summary>
        /// 删除某次试验的全部本地文件：试验数据目录（含 CSV、chart.png）+ 同名 Excel/PDF 报告。
        /// 返回清理过程中遇到的错误列表（为空表示全部成功）；任何单个文件失败都不中断其余清理。
        /// </summary>
        public static List<string> DeleteAll(string productId, string testId)
        {
            var errors = new List<string>();

            // 1) 试验数据目录 + 空样品目录回收
            try
            {
                var testDir = AppHost.Exporter.GetTestDir(productId, testId);
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);

                var productDir = Path.GetDirectoryName(testDir);
                if (!string.IsNullOrWhiteSpace(productDir)
                    && Directory.Exists(productDir)
                    && !Directory.EnumerateFileSystemEntries(productDir).Any())
                {
                    Directory.Delete(productDir);
                }
            }
            catch (System.Exception ex)
            {
                errors.Add($"试验数据目录：{ex.Message}");
            }

            // 2) 同名 Excel / PDF 报告
            var reportDir = AppHost.Settings.Report.OutputDirectory;
            DeleteFile(Path.Combine(reportDir, $"{testId}_报告.xlsx"), errors);
            DeleteFile(Path.Combine(reportDir, $"{testId}_报告.pdf"), errors);

            return errors;
        }

        private static void DeleteFile(string path, List<string> errors)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (System.Exception ex)
            {
                errors.Add($"{path}：{ex.Message}");
            }
        }
    }
}
