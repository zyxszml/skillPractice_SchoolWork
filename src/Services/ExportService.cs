using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Iso11820Simulator.Configuration;
using Iso11820Simulator.Models;

namespace Iso11820Simulator.Services
{
    /// <summary>
    /// 一行温度采样记录。
    /// </summary>
    public class TempSample
    {
        public int Time { get; set; }
        public double Tf1 { get; set; }
        public double Tf2 { get; set; }
        public double Ts { get; set; }
        public double Tc { get; set; }
        public double TCal { get; set; }
    }

    /// <summary>
    /// 导出服务：CSV / Excel / PDF 三合一。
    /// </summary>
    public class ExportService
    {
        private readonly FileStorageSection _file;
        private readonly ReportSection _report;

        public ExportService(FileStorageSection file, ReportSection report)
        {
            _file = file;
            _report = report;
            // EPPlus 7+ 需显式设置许可证上下文
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            // PDFsharp 6.2 不再依赖 GDI+ 自动找系统字体，必须注册自定义字体解析器。
            // 这里覆盖全部字体请求（含 MigraDoc 内部的 "Courier New" 错误字体），
            // 避免出现 "The font 'Courier New' cannot be resolved for predefined error font"。
            GlobalFontSettings.FontResolver = new WindowsFontResolver();
        }

        // ============================================================
        //  路径生成
        // ============================================================
        public string GetTestDir(string productId, string testId)
            => Path.Combine(_file.TestDataDirectory, productId, testId);

        public string GetCsvPath(string productId, string testId)
            => Path.Combine(GetTestDir(productId, testId), "sensor_data.csv");

        public string GetExcelPath(string testId)
        {
            Directory.CreateDirectory(_report.OutputDirectory);
            return Path.Combine(_report.OutputDirectory, $"{testId}_报告.xlsx");
        }

        public string GetPdfPath(string testId)
        {
            Directory.CreateDirectory(_report.OutputDirectory);
            return Path.Combine(_report.OutputDirectory, $"{testId}_报告.pdf");
        }

        public string GetChartPngPath(string productId, string testId)
            => Path.Combine(GetTestDir(productId, testId), "chart.png");

        // ============================================================
        //  CSV
        // ============================================================
        public string ExportCsv(string productId, string testId, IEnumerable<TempSample> samples)
        {
            var path = GetCsvPath(productId, testId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            sw.WriteLine("Time,Temp1,Temp2,TempSurface,TempCenter,TempCalibration");
            foreach (var s in samples)
            {
                sw.WriteLine($"{s.Time},{F(s.Tf1)},{F(s.Tf2)},{F(s.Ts)},{F(s.Tc)},{F(s.TCal)}");
            }
            return path;
        }

        // ============================================================
        //  Excel（3 Sheet：信息 / 数据 / 曲线图）
        // ============================================================
        public string ExportExcel(TestMaster t, IEnumerable<TempSample> samples)
        {
            var path = GetExcelPath(t.TestId);
            if (File.Exists(path))
                File.Delete(path);

            using var pkg = new ExcelPackage(new FileInfo(path));

            // Sheet1：信息表
            var ws1 = pkg.Workbook.Worksheets.Add("试验信息");
            ws1.Cells[1, 1].Value = "ISO 11820 建材不燃性试验报告";
            ws1.Cells[1, 1, 1, 4].Merge = true;
            ws1.Cells[1, 1].Style.Font.Bold = true;
            ws1.Cells[1, 1].Style.Font.Size = 14;

            var rows = new (string, string)[]
            {
                ("样品编号", t.ProductId),
                ("试验编号", t.TestId),
                ("试验日期", t.TestDate.ToString("yyyy-MM-dd")),
                ("操作员",   t.Operator),
                ("试验依据", t.According),
                ("设备",     $"{t.ApparatusId} {t.ApparatusName}"),
                ("环境温度(℃)", t.AmbTemp.ToString("F1")),
                ("环境湿度(%)", t.AmbHumi.ToString("F1")),
                ("试验前质量(g)", t.PreWeight.ToString("F2")),
                ("试验后质量(g)", t.PostWeight.ToString("F2")),
                ("失重率(%)",  t.LostWeightPer.ToString("F2")),
                ("样品温升(℃)", t.DeltaTf.ToString("F1")),
                ("炉温1温升(℃)", t.DeltaTf1.ToString("F1")),
                ("炉温2温升(℃)", t.DeltaTf2.ToString("F1")),
                ("表面温升(℃)", t.DeltaTs.ToString("F1")),
                ("中心温升(℃)", t.DeltaTc.ToString("F1")),
                ("炉温1最大(℃)", $"{t.MaxTf1:F1} @ {t.MaxTf1Time}s"),
                ("炉温2最大(℃)", $"{t.MaxTf2:F1} @ {t.MaxTf2Time}s"),
                ("表面温最大(℃)", $"{t.MaxTs:F1} @ {t.MaxTsTime}s"),
                ("中心温最大(℃)", $"{t.MaxTc:F1} @ {t.MaxTcTime}s"),
                ("总试验时长(s)", t.TotalTestTime.ToString()),
                ("恒功率", t.ConstPower.ToString()),
                ("火焰持续(s)", t.FlameDuration.ToString()),
                ("判定结论",  Judge(t) ? "通过" : "不通过"),
                ("备注", t.Memo ?? "")
            };
            for (int i = 0; i < rows.Length; i++)
            {
                ws1.Cells[3 + i, 1].Value = rows[i].Item1;
                ws1.Cells[3 + i, 2].Value = rows[i].Item2;
            }
            ws1.Cells[3 + rows.Length, 1].Value = "判定标准：样品温升≤50℃ 且 失重率≤50% 且 火焰持续<5s";
            ws1.Cells.AutoFitColumns();

            // Sheet2：温度数据
            var ws2 = pkg.Workbook.Worksheets.Add("温度数据");
            ws2.Cells[1, 1].Value = "Time";
            ws2.Cells[1, 2].Value = "Temp1";
            ws2.Cells[1, 3].Value = "Temp2";
            ws2.Cells[1, 4].Value = "TempSurface";
            ws2.Cells[1, 5].Value = "TempCenter";
            ws2.Cells[1, 6].Value = "TempCalibration";
            int r = 2;
            var list = samples.ToList();
            foreach (var s in list)
            {
                ws2.Cells[r, 1].Value = s.Time;
                ws2.Cells[r, 2].Value = Math.Round(s.Tf1, 1);
                ws2.Cells[r, 3].Value = Math.Round(s.Tf2, 1);
                ws2.Cells[r, 4].Value = Math.Round(s.Ts, 1);
                ws2.Cells[r, 5].Value = Math.Round(s.Tc, 1);
                ws2.Cells[r, 6].Value = Math.Round(s.TCal, 1);
                r++;
            }

            // Sheet3：曲线图
            if (list.Count > 1)
            {
                var ws3 = pkg.Workbook.Worksheets.Add("温度曲线");
                var chart = ws3.Drawings.AddLineChart("chart", eLineChartType.Line);
                chart.Title.Text = "温度曲线";
                chart.SetPosition(1, 0, 1, 0);
                chart.SetSize(720, 360);
                var xRange = ws2.Cells[2, 1, list.Count + 1, 1];
                chart.Series.Add(ws2.Cells[2, 2, list.Count + 1, 2], xRange).Header = "炉温1";
                chart.Series.Add(ws2.Cells[2, 3, list.Count + 1, 3], xRange).Header = "炉温2";
                chart.Series.Add(ws2.Cells[2, 4, list.Count + 1, 4], xRange).Header = "表面温";
                chart.Series.Add(ws2.Cells[2, 5, list.Count + 1, 5], xRange).Header = "中心温";
                chart.XAxis.Title.Text = "时间(s)";
                chart.YAxis.Title.Text = "温度(℃)";
                chart.YAxis.MinValue = 0;
                chart.YAxis.MaxValue = 800;
            }

            pkg.Save();
            return path;
        }

        // ============================================================
        //  PDF（试验概要 + 温度曲线图片 + 判定结论）
        // ============================================================
        public string ExportPdf(TestMaster t, IEnumerable<TempSample> samples, string chartPngPath = null)
        {
            var path = GetPdfPath(t.TestId);
            if (File.Exists(path))
                File.Delete(path);

            bool pass = Judge(t);

            // ---- 文档与样式 ----
            var doc = new Document();
            var section = doc.AddSection();
            section.PageSetup.TopMargin = Unit.FromCentimeter(2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2);

            const string fontName = WindowsFontResolver.DefaultFontName;
            const string styleTitle = "PdfTitle";
            const string styleSection = "PdfSection";
            const string styleNormal = "PdfNormal";
            const string styleBold = "PdfBold";
            const string styleResult = "PdfResult";

            void DefineStyle(string name, double sizePt, bool bold, MigraDoc.DocumentObjectModel.Color color)
            {
                var style = doc.Styles.AddStyle(name, "Normal");
                style.Font.Name = fontName;
                style.Font.Size = Unit.FromPoint(sizePt);
                style.Font.Bold = bold;
                style.Font.Color = color;
            }

            DefineStyle(styleTitle, 18, true, Colors.Black);
            DefineStyle(styleSection, 12, true, Colors.Black);
            DefineStyle(styleNormal, 10.5, false, Colors.Black);
            DefineStyle(styleBold, 10.5, true, Colors.Black);
            DefineStyle(styleResult, 13, true,
                pass ? new MigraDoc.DocumentObjectModel.Color(0x1E, 0x84, 0x49)
                     : new MigraDoc.DocumentObjectModel.Color(0xBE, 0x41, 0x38));

            // ---- 标题 ----
            var titlePara = section.AddParagraph();
            titlePara.Style = styleTitle;
            titlePara.Format.Alignment = ParagraphAlignment.Center;
            titlePara.Format.SpaceAfter = Unit.FromPoint(6);
            titlePara.AddText("ISO 11820 建材不燃性试验报告");

            var subPara = section.AddParagraph();
            subPara.Style = styleNormal;
            subPara.Format.Alignment = ParagraphAlignment.Center;
            subPara.Format.SpaceAfter = Unit.FromPoint(14);
            subPara.AddText($"试验编号：{t.TestId}    生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // ---- 试验概要（表格）----
            AddSectionHeading(section, "一、试验概要", styleSection);
            section.Add(BuildSummaryTable(t, styleNormal, styleBold, fontName));

            // ---- 判定结论 ----
            AddSectionHeading(section, "二、判定结论", styleSection);
            var criterion = section.AddParagraph();
            criterion.Style = styleNormal;
            criterion.Format.SpaceAfter = Unit.FromPoint(4);
            criterion.AddText("判定标准：样品温升 ≤ 50 ℃  且  失重率 ≤ 50 %  且  火焰持续 < 5 s  →  通过；否则不通过。");

            var detail = section.AddParagraph();
            detail.Style = styleNormal;
            detail.Format.SpaceAfter = Unit.FromPoint(4);
            detail.AddText($"样品温升：{t.DeltaTf:F1} ℃    失重率：{t.LostWeightPer:F2} %    火焰持续：{t.FlameDuration} s");

            var result = section.AddParagraph();
            result.Style = styleResult;
            result.Format.SpaceBefore = Unit.FromPoint(2);
            result.Format.SpaceAfter = Unit.FromPoint(12);
            result.AddText($"综合判定：{(pass ? "通过" : "不通过")}");

            // ---- 温度曲线图片 ----
            AddSectionHeading(section, "三、温度曲线", styleSection);
            if (!string.IsNullOrEmpty(chartPngPath) && File.Exists(chartPngPath))
            {
                // 等比缩放：约束到页面可用宽度，且高度不超过 14cm
                const double maxWidthCm = 17.0;
                const double maxHeightCm = 14.0;
                double drawW = maxWidthCm;
                double drawH = maxHeightCm;
                try
                {
                    using var probe = XImage.FromFile(chartPngPath);
                    double ratio = probe.PointHeight / probe.PointWidth; // 高/宽
                    drawW = maxWidthCm;
                    drawH = maxWidthCm * ratio;
                    if (drawH > maxHeightCm)
                    {
                        drawH = maxHeightCm;
                        drawW = drawH / ratio;
                    }
                }
                catch { /* 取默认尺寸 */ }

                var imgPara = section.AddParagraph();
                imgPara.Format.Alignment = ParagraphAlignment.Center;
                var img = imgPara.AddImage(chartPngPath);
                img.Width = Unit.FromCentimeter(drawW);
                img.Height = Unit.FromCentimeter(drawH);
            }
            else
            {
                var noImg = section.AddParagraph();
                noImg.Style = styleNormal;
                noImg.AddText("（温度曲线图片未生成）");
            }

            // ---- 页脚：生成信息 ----
            var footer = section.Footers.Primary.AddParagraph();
            footer.Style = styleNormal;
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.AddText($"ISO 11820 试验仿真系统  ·  操作员：{t.Operator ?? ""}");

            // ---- 渲染输出 ----
            var renderer = new PdfDocumentRenderer { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Info.Title = "ISO 11820 建材不燃性试验报告";
            renderer.PdfDocument.Info.Author = t.Operator ?? "ISO11820";
            renderer.Save(path);

            return path;
        }

        private static void AddSectionHeading(Section section, string text, string styleName)
        {
            var para = section.AddParagraph();
            para.Style = styleName;
            para.Format.SpaceBefore = Unit.FromPoint(8);
            para.Format.SpaceAfter = Unit.FromPoint(6);
            para.AddText(text);
        }

        private static Table BuildSummaryTable(TestMaster t, string styleNormal, string styleBold, string fontName)
        {
            var table = new Table();
            table.Borders.Width = 0.5;
            table.Borders.Color = new MigraDoc.DocumentObjectModel.Color(0xC9, 0xD3, 0xE0);
            table.Format.Font.Name = fontName;
            table.Format.Font.Size = Unit.FromPoint(10.5);

            // 两列布局：标签 | 值
            var colLabel = table.AddColumn(Unit.FromCentimeter(4.5));
            var colValue = table.AddColumn(Unit.FromCentimeter(11.5));
            colLabel.Format.Alignment = ParagraphAlignment.Left;
            colValue.Format.Alignment = ParagraphAlignment.Left;

            var rows = new (string Label, string Value)[]
            {
                ("样品编号", t.ProductId ?? ""),
                ("试验编号", t.TestId ?? ""),
                ("试验日期", t.TestDate.ToString("yyyy-MM-dd")),
                ("操作员", t.Operator ?? ""),
                ("试验依据", t.According ?? ""),
                ("设备", (t.ApparatusName ?? "").Trim()),
                ("环境温度", $"{t.AmbTemp:F1} ℃"),
                ("环境湿度", $"{t.AmbHumi:F1} %"),
                ("试验前质量", $"{t.PreWeight:F2} g"),
                ("试验后质量", $"{t.PostWeight:F2} g"),
                ("失重量", $"{t.LostWeight:F2} g"),
                ("失重率", $"{t.LostWeightPer:F2} %"),
                ("炉温1最大", $"{t.MaxTf1:F1} ℃ @ {t.MaxTf1Time} s"),
                ("炉温2最大", $"{t.MaxTf2:F1} ℃ @ {t.MaxTf2Time} s"),
                ("表面温最大", $"{t.MaxTs:F1} ℃ @ {t.MaxTsTime} s"),
                ("中心温最大", $"{t.MaxTc:F1} ℃ @ {t.MaxTcTime} s"),
                ("炉温1/2温升", $"{t.DeltaTf1:F1} / {t.DeltaTf2:F1} ℃"),
                ("表面/中心温升", $"{t.DeltaTs:F1} / {t.DeltaTc:F1} ℃"),
                ("总试验时长", $"{t.TotalTestTime} s"),
                ("恒功率", $"{t.ConstPower}"),
                ("火焰持续", t.FlameDuration > 0 ? $"{t.FlameDuration} s" : "无"),
                ("备注", string.IsNullOrWhiteSpace(t.Memo) ? "—" : t.Memo),
            };

            // 标题行
            var header = table.AddRow();
            header.Shading.Color = new MigraDoc.DocumentObjectModel.Color(0xEB, 0xF0, 0xF7);
            header.Cells[0].AddParagraph("项目").Style = styleBold;
            header.Cells[1].AddParagraph("内容").Style = styleBold;

            foreach (var (label, value) in rows)
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph(label).Style = styleNormal;
                row.Cells[1].AddParagraph(value).Style = styleNormal;
            }

            return table;
        }

        public string ExportChartPng(string productId, string testId, IEnumerable<TempSample> samples)
        {
            var list = samples.OrderBy(s => s.Time).ToList();
            if (list.Count < 2) return null;

            var path = GetChartPngPath(productId, testId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            const int width = 1000;
            const int height = 520;
            var plot = new System.Drawing.RectangleF(70, 30, 870, 410);
            int minTime = list.Min(s => s.Time);
            int maxTime = Math.Max(minTime + 1, list.Max(s => s.Time));

            float MapX(int time) => plot.Left + (time - minTime) * plot.Width / (maxTime - minTime);
            float MapY(double temp) => plot.Bottom - (float)(Math.Clamp(temp, 0, 800) / 800.0 * plot.Height);

            using var bmp = new System.Drawing.Bitmap(width, height);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.White);

            using var axisPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 80, 80), 1);
            using var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 220, 220), 1);
            using var labelFont = new System.Drawing.Font("Microsoft YaHei UI", 9);
            using var titleFont = new System.Drawing.Font("Microsoft YaHei UI", 13, System.Drawing.FontStyle.Bold);

            g.DrawString("温度曲线", titleFont, System.Drawing.Brushes.Black, new System.Drawing.PointF(430, 6));

            for (int temp = 0; temp <= 800; temp += 100)
            {
                float y = MapY(temp);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                g.DrawString(temp.ToString(), labelFont, System.Drawing.Brushes.DimGray, 30, y - 7);
            }

            for (int i = 0; i <= 6; i++)
            {
                int time = minTime + (maxTime - minTime) * i / 6;
                float x = MapX(time);
                g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                g.DrawString(time.ToString(), labelFont, System.Drawing.Brushes.DimGray, x - 12, plot.Bottom + 8);
            }

            g.DrawRectangle(axisPen, plot.X, plot.Y, plot.Width, plot.Height);
            g.DrawString("时间(s)", labelFont, System.Drawing.Brushes.Black, plot.Left + plot.Width / 2 - 25, plot.Bottom + 35);
            g.DrawString("温度(℃)", labelFont, System.Drawing.Brushes.Black, 8, plot.Top + plot.Height / 2 - 10);

            void DrawSeries(string name, System.Drawing.PointF[] points, System.Drawing.Color color, int legendX, int legendY)
            {
                if (points.Length < 2) return;
                using var pen = new System.Drawing.Pen(color, 2);
                g.DrawLines(pen, points);
                g.DrawLine(pen, legendX, legendY + 7, legendX + 30, legendY + 7);
                g.DrawString(name, labelFont, System.Drawing.Brushes.Black, legendX + 36, legendY);
            }

            DrawSeries("炉温1", list.Select(s => new System.Drawing.PointF(MapX(s.Time), MapY(s.Tf1))).ToArray(), System.Drawing.Color.FromArgb(220, 50, 50), 760, 70);
            DrawSeries("炉温2", list.Select(s => new System.Drawing.PointF(MapX(s.Time), MapY(s.Tf2))).ToArray(), System.Drawing.Color.FromArgb(230, 130, 0), 760, 95);
            DrawSeries("表面温", list.Select(s => new System.Drawing.PointF(MapX(s.Time), MapY(s.Ts))).ToArray(), System.Drawing.Color.FromArgb(0, 140, 210), 760, 120);
            DrawSeries("中心温", list.Select(s => new System.Drawing.PointF(MapX(s.Time), MapY(s.Tc))).ToArray(), System.Drawing.Color.FromArgb(0, 150, 80), 760, 145);

            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return path;
        }

        // ============================================================
        //  辅助
        // ============================================================
        /// <summary>判定：deltatf≤50 且 lostweight_per≤50 且 flameduration&lt;5 → 通过。</summary>
        public static bool Judge(TestMaster t)
            => t.DeltaTf <= 50 && t.LostWeightPer <= 50 && t.FlameDuration < 5;

        private static string F(double v) => v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>从 CSV 文件回读温度数据（用于历史报告重新导出）。</summary>
        public static List<TempSample> ReadCsv(string csvPath)
        {
            var list = new List<TempSample>();
            if (!File.Exists(csvPath)) return list;
            foreach (var line in File.ReadAllLines(csvPath).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var p = line.Split(',');
                if (p.Length < 6) continue;
                if (!int.TryParse(p[0], out int time)) continue;
                list.Add(new TempSample
                {
                    Time = time,
                    Tf1 = D(p[1]),
                    Tf2 = D(p[2]),
                    Ts = D(p[3]),
                    Tc = D(p[4]),
                    TCal = D(p[5])
                });
            }
            return list;
        }

        private static double D(string s)
            => double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

        private sealed class WindowsFontResolver : IFontResolver
        {
            public const string DefaultFontName = "MicrosoftYaHei";
            private const string ErrorFontName = "CourierNew";

            // 注意：必须用纯 .ttf 单字体文件。
            // .ttc（如 msyh.ttc/simsun.ttc）是 TrueType Collection，
            // PDFsharp 6.2 在 OpenTypeFontFace.CetOrCreateFrom 里无法从 .ttc 原始字节构造字体，会抛 NRE。
            private static readonly string[] RegularCandidates =
            {
                "simhei.ttf",   // 黑体，中文显示良好
                "simkai.ttf",   // 楷体
                "STKAITI.TTF",
                "arial.ttf"
            };

            private static readonly string[] BoldCandidates =
            {
                "simhei.ttf",   // 黑体本身较粗，作粗体兜底
                "simkai.ttf",
                "STKAITI.TTF",
                "arialbd.ttf",
                "arial.ttf"
            };

            // 等宽候选（用于覆盖 MigraDoc 内部 "Courier New" 错误字体）
            private static readonly string[] MonoCandidates =
            {
                "cour.ttf",      // Courier New
                "consola.ttf",   // Consolas
                "lucon.ttf",     // Lucida Console
                "simhei.ttf"
            };

            private static readonly string[] MonoBoldCandidates =
            {
                "courbd.ttf",
                "consolab.ttf",
                "simhei.ttf"
            };

            public byte[] GetFont(string faceName)
            {
                if (string.IsNullOrEmpty(faceName))
                    return ReadFirstAvailable(RegularCandidates);

                bool bold = faceName.EndsWith("#bold", StringComparison.OrdinalIgnoreCase);
                string name = bold ? faceName.Substring(0, faceName.Length - 5) : faceName;

                // MigraDoc 内部错误字体（Courier New）以及任何等宽请求 → 用等宽候选
                if (name.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0
                    || name == ErrorFontName)
                {
                    return ReadFirstAvailable(bold ? MonoBoldCandidates : MonoCandidates);
                }

                return ReadFirstAvailable(bold ? BoldCandidates : RegularCandidates);
            }

            public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                // 任何字体请求都映射为我们能解析的 typeface 名称：
                // 等宽请求单独走 CourierNew（GetFont 会用 Mono 候选），其余统一走雅黑名。
                bool mono = familyName != null
                    && (familyName.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0
                        || familyName.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0);
                string face = mono
                    ? (isBold ? ErrorFontName + "#bold" : ErrorFontName)
                    : (isBold ? DefaultFontName + "#bold" : DefaultFontName);
                return new FontResolverInfo(face);
            }

            private static byte[] ReadFirstAvailable(string[] candidates)
            {
                var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                foreach (var name in candidates)
                {
                    try
                    {
                        var path = Path.Combine(fontsDir, name);
                        if (File.Exists(path))
                            return File.ReadAllBytes(path);
                    }
                    catch { /* 继续尝试下一个候选 */ }
                }
                throw new FileNotFoundException("未找到可用于 PDF 导出的系统字体。");
            }
        }
    }
}
