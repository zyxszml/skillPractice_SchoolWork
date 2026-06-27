using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Iso11820Simulator.Global;
using Iso11820Simulator.Models;
using Iso11820Simulator.Services;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 历史记录查询：按日期 / 样品编号 / 操作员筛选，查看详情，导出 Excel。
    /// 注意：本窗体作为非顶级窗体嵌入到 TabPage，Load 事件不会自动触发，
    /// 因此初始化逻辑在构造函数末尾直接调用。
    /// </summary>
    public class HistoryForm : Form
    {
        private readonly DateTimePicker _dtpFrom = new() { Format = DateTimePickerFormat.Short };
        private readonly DateTimePicker _dtpTo = new() { Format = DateTimePickerFormat.Short };
        private readonly TextBox _txtProduct = new();
        private readonly ComboBox _cmbOperator = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button _btnQuery = new() { Text = "查询" };
        private readonly Button _btnExport = new() { Text = "导出 Excel" };
        private readonly Button _btnExportReport = new() { Text = "导出报告(PDF)", Enabled = false };
        private readonly Button _btnDelete = new() { Text = "删除记录", Enabled = false };
        private readonly DataGridView _grid = new() { AllowUserToAddRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Dock = DockStyle.Fill };
        private readonly Label _lblSummary = new();
        private Label _gridEmptyHint;

        public HistoryForm()
        {
            UiTheme.ApplyForm(this);
            Text = "记录查询";
            ClientSize = new Size(1200, 600);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.AppBack
            };
            // 顶部筛选/按钮工具区：放两行（筛选 + 操作按钮），避免删除按钮被挤出可视区
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var top = UiTheme.CreateSurfacePanel(new Padding(12, 10, 12, 8));
            top.Dock = DockStyle.Fill;
            top.Margin = new Padding(12, 12, 12, 6);

            var filter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.Surface
            };

            _dtpFrom.Width = 126;
            _dtpTo.Width = 126;
            _txtProduct.Width = 160;
            _cmbOperator.Width = 138;
            UiTheme.StyleInput(_txtProduct);
            UiTheme.StyleInput(_cmbOperator);
            UiTheme.StyleButton(_btnQuery, UiButtonKind.Primary);
            UiTheme.StyleButton(_btnExport);
            UiTheme.StyleButton(_btnExportReport, UiButtonKind.Success);
            UiTheme.StyleButton(_btnDelete, UiButtonKind.Danger);

            _lblSummary.AutoSize = true;
            _lblSummary.ForeColor = UiTheme.MutedText;
            _lblSummary.Margin = new Padding(6, 10, 0, 0);

            filter.Controls.Add(CreateInlineLabel("从"));
            filter.Controls.Add(_dtpFrom);
            filter.Controls.Add(CreateInlineLabel("到"));
            filter.Controls.Add(_dtpTo);
            filter.Controls.Add(CreateInlineLabel("样品"));
            filter.Controls.Add(_txtProduct);
            filter.Controls.Add(CreateInlineLabel("操作员"));
            filter.Controls.Add(_cmbOperator);
            filter.Controls.Add(_btnQuery);
            filter.Controls.Add(_btnExport);
            filter.Controls.Add(_btnExportReport);
            filter.Controls.Add(_btnDelete);
            filter.Controls.Add(_lblSummary);
            top.Controls.Add(filter);

            var gridPanel = UiTheme.CreateSurfacePanel(new Padding(0));
            gridPanel.Dock = DockStyle.Fill;
            gridPanel.Margin = new Padding(12, 6, 12, 12);
            UiTheme.StyleGrid(_grid);
            gridPanel.Controls.Add(_grid);
            // 网格空状态提示：与曲线空状态视觉一致
            _gridEmptyHint = UiTheme.CreateEmptyHint(gridPanel, "暂无记录，请先进行试验");

            layout.Controls.Add(top, 0, 0);
            layout.Controls.Add(gridPanel, 0, 1);
            Controls.Add(layout);

            _dtpFrom.Value = DateTime.Today.AddMonths(-1);
            _dtpTo.Value = DateTime.Today;

            _btnQuery.Click += (s, e) => RefreshData();
            _btnExport.Click += (s, e) => ExportResults();
            _btnExportReport.Click += (s, e) => ExportReportForSelected();
            _btnDelete.Click += (s, e) => DeleteSelectedRecord();
            _txtProduct.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) RefreshData(); };
            _grid.DoubleClick += (s, e) => ShowDetail();
            _grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    e.Handled = true;
                    DeleteSelectedRecord();
                }
            };

            // 嵌入 TabPage 时 Load 事件不触发，构造函数末尾直接初始化
            LoadOperators();
            RefreshData();
        }

        private static Label CreateInlineLabel(string text)
            => new()
            {
                Text = text,
                AutoSize = true,
                ForeColor = UiTheme.MutedText,
                Margin = new Padding(0, 10, 6, 0)
            };

        public void RefreshData()
        {
            try
            {
                var list = AppHost.Db.QueryTests(_dtpFrom.Value, _dtpTo.Value, _txtProduct.Text.Trim(), _cmbOperator.SelectedItem?.ToString() ?? "");
                var rows = list.Select(t => new
                {
                    样品编号 = t.ProductId,
                    试验编号 = t.TestId,
                    试验日期 = t.TestDate.ToString("yyyy-MM-dd"),
                    操作员 = t.Operator,
                    试验前质量 = t.PreWeight.ToString("F2"),
                    试验后质量 = t.PostWeight.ToString("F2"),
                    失重率 = t.LostWeightPer.ToString("F2") + " %",
                    样品温升 = t.DeltaTf.ToString("F1") + " ℃",
                    总时长 = t.TotalTestTime + " s",
                    恒功率 = t.ConstPower,
                    火焰持续 = t.FlameDuration + " s",
                    判定 = ExportService.Judge(t) ? "通过" : "不通过",
                    已保存 = t.Flag == "10000000"
                }).ToList();
                _grid.DataSource = rows;
                _lblSummary.Text = $"共 {rows.Count} 条";
                _btnDelete.Enabled = rows.Count > 0;
                _btnExportReport.Enabled = rows.Count > 0;
                // 切换空状态提示可见性
                if (_gridEmptyHint != null) _gridEmptyHint.Visible = (rows.Count == 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("查询失败：" + ex.Message, "错误");
            }
        }

        private void LoadOperators()
        {
            try
            {
                _cmbOperator.Items.Clear();
                _cmbOperator.Items.Add(""); // 全部
                foreach (var op in AppHost.Db.GetOperators())
                    _cmbOperator.Items.Add(op.UserName);
                _cmbOperator.SelectedIndex = 0;
            }
            catch { }
        }

        private void ShowDetail()
        {
            if (!TryGetCurrentIds(out var productId, out var testId)) return;

            var t = AppHost.Db.GetTest(productId, testId);
            if (t == null) { MessageBox.Show("记录不存在"); return; }

            var msg =
                $"样品编号：{t.ProductId}\r\n" +
                $"试验编号：{t.TestId}\r\n" +
                $"试验日期：{t.TestDate:yyyy-MM-dd}\r\n" +
                $"操作员：{t.Operator}\r\n" +
                $"试验依据：{t.According}\r\n" +
                $"环境温度：{t.AmbTemp:F1} ℃    湿度：{t.AmbHumi:F1} %\r\n" +
                $"设备：{t.ApparatusName}\r\n" +
                $"试验前/后质量：{t.PreWeight:F2} / {t.PostWeight:F2} g\r\n" +
                $"失重量：{t.LostWeight:F2} g   失重率：{t.LostWeightPer:F2} %\r\n" +
                $"炉温1最大：{t.MaxTf1:F1} ℃ @ {t.MaxTf1Time}s\r\n" +
                $"炉温2最大：{t.MaxTf2:F1} ℃ @ {t.MaxTf2Time}s\r\n" +
                $"表面温最大：{t.MaxTs:F1} ℃ @ {t.MaxTsTime}s\r\n" +
                $"中心温最大：{t.MaxTc:F1} ℃ @ {t.MaxTcTime}s\r\n" +
                $"炉温1/2温升：{t.DeltaTf1:F1} / {t.DeltaTf2:F1} ℃\r\n" +
                $"表面/中心温升：{t.DeltaTs:F1} / {t.DeltaTc:F1} ℃\r\n" +
                $"总时长：{t.TotalTestTime} s   恒功率：{t.ConstPower}\r\n" +
                $"火焰：{(t.FlameDuration > 0 ? $"持续 {t.FlameDuration}s" : "无")}\r\n" +
                $"判定：{(ExportService.Judge(t) ? "通过" : "不通过")}\r\n" +
                $"备注：{t.Memo ?? ""}";
            MessageBox.Show(msg, "试验详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool TryGetCurrentIds(out string productId, out string testId)
        {
            productId = "";
            testId = "";
            if (_grid.CurrentRow?.DataBoundItem == null) return false;

            var row = _grid.CurrentRow.DataBoundItem;
            var type = row.GetType();
            productId = type.GetProperty("样品编号")?.GetValue(row)?.ToString() ?? "";
            testId = type.GetProperty("试验编号")?.GetValue(row)?.ToString() ?? "";
            return !string.IsNullOrWhiteSpace(productId) && !string.IsNullOrWhiteSpace(testId);
        }

        private void DeleteSelectedRecord()
        {
            if (!TryGetCurrentIds(out var productId, out var testId))
            {
                MessageBox.Show("请先选择一条试验记录。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"确定删除这条试验记录吗？\r\n\r\n样品编号：{productId}\r\n试验编号：{testId}\r\n\r\n将同时删除该试验的数据目录、CSV、曲线图片和同名 Excel/PDF 报告。",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            try
            {
                var deleted = AppHost.Db.DeleteTest(productId, testId);
                if (!deleted)
                {
                    MessageBox.Show("记录不存在或已被删除。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshData();
                    return;
                }

                var cleanupErrors = DeleteRecordFiles(productId, testId);
                RefreshData();

                if (cleanupErrors.Count == 0)
                {
                    MessageBox.Show("记录及关联文件已删除。", "完成",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        "数据库记录已删除，但部分关联文件未能清理：\r\n" + string.Join("\r\n", cleanupErrors),
                        "删除完成，文件清理有异常",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("删除失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static List<string> DeleteRecordFiles(string productId, string testId)
            => Iso11820Simulator.Services.RecordFileCleanup.DeleteAll(productId, testId);

        private void ExportResults()
        {
            if (_grid.Rows.Count == 0) { MessageBox.Show("无数据可导出"); return; }

            // 选择导出格式：Excel 清单 / 批量 PDF 报告
            var choice = MessageBox.Show(
                "是 = 导出当前查询结果为 Excel 清单（表格汇总）\r\n否 = 批量为每条已完成试验生成 PDF 报告（试验概要+曲线+判定）\r\n\r\n请选择：",
                "导出格式", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (choice == DialogResult.Cancel) return;

            if (choice == DialogResult.Yes)
                ExportListToExcel();
            else
                ExportBatchPdf();
        }

        private void ExportListToExcel()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "试验记录_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx" };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using var pkg = new OfficeOpenXml.ExcelPackage(new System.IO.FileInfo(sfd.FileName));
                var ws = pkg.Workbook.Worksheets.Add("试验记录");
                // 表头
                for (int c = 0; c < _grid.Columns.Count; c++)
                    ws.Cells[1, c + 1].Value = _grid.Columns[c].HeaderText;
                // 数据
                for (int r = 0; r < _grid.Rows.Count; r++)
                    for (int c = 0; c < _grid.Columns.Count; c++)
                        ws.Cells[r + 2, c + 1].Value = _grid.Rows[r].Cells[c].Value?.ToString();
                ws.Cells.AutoFitColumns();
                pkg.Save();
                MessageBox.Show("已导出：" + sfd.FileName, "完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "错误");
            }
        }

        /// <summary>批量为当前查询结果里所有"已保存"的试验生成 PDF 报告到报告目录。</summary>
        private void ExportBatchPdf()
        {
            // 收集所有已保存（flag=10000000）的试验
            var list = AppHost.Db.QueryTests(_dtpFrom.Value, _dtpTo.Value, _txtProduct.Text.Trim(), _cmbOperator.SelectedItem?.ToString() ?? "");
            var targets = list.Where(t => t.Flag == "10000000").ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("当前查询结果中没有已保存的试验可生成 PDF 报告。", "提示");
                return;
            }

            int ok = 0, fail = 0;
            using var progress = new Form
            {
                Text = "批量生成 PDF",
                Width = 380, Height = 130,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                Controls =
                {
                    new Label { Text = $"正在生成 PDF 报告（0/{targets.Count}）...", Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter },
                }
            };
            var lbl = (Label)progress.Controls[0];

            // 在后台线程生成，避免界面假死
            System.Threading.Tasks.Task.Run(() =>
            {
                var reportDir = AppHost.Settings.Report.OutputDirectory;
                System.IO.Directory.CreateDirectory(reportDir);
                foreach (var t in targets)
                {
                    try
                    {
                        var csvPath = AppHost.Exporter.GetCsvPath(t.ProductId, t.TestId);
                        var samples = ExportService.ReadCsv(csvPath);
                        string chartPath = null;
                        if (samples.Count > 0)
                            chartPath = AppHost.Exporter.ExportChartPng(t.ProductId, t.TestId, samples);
                        AppHost.Exporter.ExportPdf(t, samples, chartPath);
                        ok++;
                    }
                    catch { fail++; }
                    progress.BeginInvoke((Action)(() => lbl.Text = $"正在生成 PDF 报告（{ok + fail}/{targets.Count}）..."));
                }
                progress.BeginInvoke((Action)(() =>
                {
                    progress.Close();
                    MessageBox.Show(progress,
                        $"批量生成完成：成功 {ok} 条" + (fail > 0 ? $"，失败 {fail} 条" : "") + $"\r\n输出目录：{reportDir}",
                        "完成", MessageBoxButtons.OK, fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }));
            });

            progress.ShowDialog(this);
        }

        /// <summary>从历史记录重新导出当前选中试验的 PDF 报告（试验概要 + 温度曲线 + 判定结论）。</summary>
        private void ExportReportForSelected()
        {
            if (!TryGetCurrentIds(out var productId, out var testId))
            {
                MessageBox.Show("请先选择一条试验记录。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var t = AppHost.Db.GetTest(productId, testId);
            if (t == null)
            {
                MessageBox.Show("记录不存在或已被删除。", "提示");
                return;
            }

            if (string.IsNullOrEmpty(t.Flag) || t.Flag != "10000000")
            {
                MessageBox.Show("该试验尚未保存试验记录（现象/质量），无法生成完整报告。\r\n请先完成并保存试验记录。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 读取已落库的 CSV 温度数据
                var csvPath = AppHost.Exporter.GetCsvPath(productId, testId);
                var samples = ExportService.ReadCsv(csvPath);

                string excelPath = null, pdfPath = null, chartPath = null;
                try { excelPath = AppHost.Exporter.ExportExcel(t, samples); }
                catch (Exception ex) { Serilog.Log.Warning(ex, "Excel 导出失败"); }

                if (samples.Count > 0)
                {
                    try { chartPath = AppHost.Exporter.ExportChartPng(productId, testId, samples); }
                    catch (Exception ex) { Serilog.Log.Warning(ex, "曲线图导出失败"); }
                }

                pdfPath = AppHost.Exporter.ExportPdf(t, samples, chartPath);

                MessageBox.Show(
                    "报告已生成：\r\n" +
                    (pdfPath != null ? $"PDF：{pdfPath}\r\n" : "") +
                    (excelPath != null ? $"Excel：{excelPath}\r\n" : ""),
                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("报告生成失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
