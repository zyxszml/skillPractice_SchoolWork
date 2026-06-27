using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Iso11820Simulator.Global;
using Iso11820Simulator.Models;
using Iso11820Simulator.Services;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 试验现象记录窗体：火焰 / 试验后质量 / 备注。
    /// 保存后自动计算失重率与各通道温升，并导出 Excel/PDF。
    /// </summary>
    public class TestRecordForm : Form
    {
        private readonly TestMaster _test;
        private readonly List<TempSample> _samples;

        private readonly CheckBox _chkFlame = new() { Text = "出现持续火焰", AutoSize = true };
        private readonly NumericUpDown _numFlameTime = new() { Minimum = 0, Maximum = 36000, Enabled = false };
        private readonly NumericUpDown _numFlameDur = new() { Minimum = 0, Maximum = 36000, Enabled = false };
        private readonly TextBox _txtPostWeight = new();
        private readonly TextBox _txtMemo = new() { Multiline = true, Height = 60 };
        private readonly Label _lblCalc = new() { AutoSize = true, ForeColor = Color.Blue };

        public TestMaster SavedTest { get; private set; }

        public TestRecordForm(TestMaster test, List<TempSample> samples)
        {
            _test = test;
            _samples = samples ?? new List<TempSample>();

            UiTheme.ApplyForm(this);
            Text = "试验记录 - " + test.ProductId + "/" + test.TestId;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(640, 520);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.AppBack
            };
            var form = UiTheme.CreateFormTable();
            form.Dock = DockStyle.Top;
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.Controls.Add(form);

            AddSection(form, "燃烧现象");
            var pnlFlame = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                BackColor = UiTheme.AppBack,
                Margin = new Padding(0, 3, 0, 3)
            };
            _chkFlame.Margin = new Padding(0, 8, 24, 0);
            _chkFlame.CheckedChanged += (s, e) =>
            {
                _numFlameTime.Enabled = _chkFlame.Checked;
                _numFlameDur.Enabled = _chkFlame.Checked;
            };
            var lblFlameTime = CreateInlineLabel("发生时刻(s)");
            _numFlameTime.Width = 90;
            var lblFlameDur = CreateInlineLabel("持续(s)");
            _numFlameDur.Width = 90;
            pnlFlame.Controls.AddRange(new Control[] { _chkFlame, lblFlameTime, _numFlameTime, lblFlameDur, _numFlameDur });
            AddRow(form, "火焰情况", pnlFlame);

            AddSection(form, "质量与备注");
            AddRow(form, "试验前质量(g)", new Label { Text = test.PreWeight.ToString("F2"), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
            AddRow(form, "试验后质量(g) *", _txtPostWeight);
            _txtMemo.Height = 72;
            AddRow(form, "备注", _txtMemo, 82);

            AddSection(form, "计算预览");
            _lblCalc.AutoSize = false;
            _lblCalc.Dock = DockStyle.Fill;
            _lblCalc.ForeColor = UiTheme.Text;
            _lblCalc.BackColor = UiTheme.SurfaceAlt;
            _lblCalc.BorderStyle = BorderStyle.FixedSingle;
            _lblCalc.Padding = new Padding(10);
            AddWideRow(form, _lblCalc, 96);

            // 实时计算预览
            _txtPostWeight.TextChanged += (s, e) => CalcPreview();

            var btnSave = new Button { Text = "保存并生成报告" };
            var btnCancel = new Button { Text = "取消" };
            btnSave.Width = 142;
            UiTheme.StyleButton(btnSave, UiButtonKind.Primary);
            UiTheme.StyleButton(btnCancel);
            btnSave.Click += (s, e) => DoSave();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UiTheme.AppBack,
                Padding = new Padding(16, 12, 16, 0)
            };
            actions.Controls.Add(btnSave);
            actions.Controls.Add(btnCancel);

            Controls.Add(UiTheme.CreateDialogShell(body, actions));
            AcceptButton = btnSave;
            CancelButton = btnCancel;

            // 默认填值
            _txtPostWeight.Text = test.PreWeight.ToString("F2");
        }

        private static Label CreateInlineLabel(string text)
            => new()
            {
                Text = text,
                AutoSize = true,
                ForeColor = UiTheme.MutedText,
                Margin = new Padding(0, 9, 6, 0)
            };

        private void AddSection(TableLayoutPanel form, string title)
        {
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            var lbl = UiTheme.CreateSectionLabel(title);
            form.Controls.Add(lbl, 0, row);
            form.SetColumnSpan(lbl, 2);
        }

        private void AddRow(TableLayoutPanel form, string label, Control ctrl, int height = 38)
        {
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            ctrl.Dock = DockStyle.Fill;
            if (ctrl is TextBox or ComboBox)
                UiTheme.StyleInput(ctrl);
            form.Controls.Add(UiTheme.CreateFieldLabel(label), 0, row);
            form.Controls.Add(ctrl, 1, row);
        }

        private void AddWideRow(TableLayoutPanel form, Control ctrl, int height)
        {
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            ctrl.Dock = DockStyle.Fill;
            form.Controls.Add(ctrl, 0, row);
            form.SetColumnSpan(ctrl, 2);
        }

        private void CalcPreview()
        {
            if (!double.TryParse(_txtPostWeight.Text, out double post)) { _lblCalc.Text = ""; return; }
            double pre = _test.PreWeight;
            double lost = pre - post;
            double lostPer = pre > 0 ? lost / pre * 100 : 0;

            // 从样本取最大值/最终值
            double mTf1 = 0, mTf2 = 0, mTs = 0, mTc = 0;
            int mTf1T = 0, mTf2T = 0, mTsT = 0, mTcT = 0;
            double fTf1 = 0, fTf2 = 0, fTs = 0, fTc = 0;
            int totalTime = 0;
            if (_samples.Count > 0)
            {
                var last = _samples.Last();
                fTf1 = last.Tf1; fTf2 = last.Tf2; fTs = last.Ts; fTc = last.Tc;
                totalTime = last.Time;
                foreach (var s in _samples)
                {
                    if (s.Tf1 > mTf1) { mTf1 = s.Tf1; mTf1T = s.Time; }
                    if (s.Tf2 > mTf2) { mTf2 = s.Tf2; mTf2T = s.Time; }
                    if (s.Ts > mTs) { mTs = s.Ts; mTsT = s.Time; }
                    if (s.Tc > mTc) { mTc = s.Tc; mTcT = s.Time; }
                }
            }

            double amb = _test.AmbTemp;
            _lblCalc.Text =
                $"预览：失重量 {lost:F2} g   失重率 {lostPer:F2} %\r\n" +
                $"炉温1温升 {fTf1 - amb:F1} ℃   炉温2温升 {fTf2 - amb:F1} ℃\r\n" +
                $"表面温升 {fTs - amb:F1} ℃   中心温升 {fTc - amb:F1} ℃\r\n" +
                $"判定：{(ExportService.Judge(new TestMaster { DeltaTf = fTs - amb, LostWeightPer = lostPer, FlameDuration = (int)_numFlameDur.Value }) ? "通过" : "不通过")}";
        }

        private void DoSave()
        {
            if (_samples.Count == 0)
            {
                MessageBox.Show("没有温度采样数据，不能保存试验记录。请确认 CSV 已生成或重新完成一次记录。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!double.TryParse(_txtPostWeight.Text, out double post) || post <= 0)
            { MessageBox.Show("请输入有效的试验后质量"); return; }

            double pre = _test.PreWeight;
            double lost = pre - post;
            double lostPer = pre > 0 ? lost / pre * 100 : 0;

            // 计算温度统计
            double mTf1 = 0, mTf2 = 0, mTs = 0, mTc = 0;
            int mTf1T = 0, mTf2T = 0, mTsT = 0, mTcT = 0;
            double fTf1 = 0, fTf2 = 0, fTs = 0, fTc = 0;
            int totalTime = 0;
            int fTime = 0;
            if (_samples.Count > 0)
            {
                var last = _samples.Last();
                fTf1 = last.Tf1; fTf2 = last.Tf2; fTs = last.Ts; fTc = last.Tc;
                fTime = last.Time; totalTime = last.Time;
                foreach (var s in _samples)
                {
                    if (s.Tf1 > mTf1) { mTf1 = s.Tf1; mTf1T = s.Time; }
                    if (s.Tf2 > mTf2) { mTf2 = s.Tf2; mTf2T = s.Time; }
                    if (s.Ts > mTs) { mTs = s.Ts; mTsT = s.Time; }
                    if (s.Tc > mTc) { mTc = s.Tc; mTcT = s.Time; }
                }
            }

            double amb = _test.AmbTemp;
            _test.PostWeight = post;
            _test.LostWeight = lost;
            _test.LostWeightPer = lostPer;
            _test.TotalTestTime = totalTime;
            _test.ConstPower = AppHost.Controller.ConstPowerAverage > 0
                ? AppHost.Controller.ConstPowerAverage
                : _test.ConstPower;
            _test.PhenoCode = _chkFlame.Checked ? "FLAME" : "";
            _test.FlameTime = _chkFlame.Checked ? (int)_numFlameTime.Value : 0;
            _test.FlameDuration = _chkFlame.Checked ? (int)_numFlameDur.Value : 0;
            _test.MaxTf1 = mTf1; _test.MaxTf2 = mTf2; _test.MaxTs = mTs; _test.MaxTc = mTc;
            _test.MaxTf1Time = mTf1T; _test.MaxTf2Time = mTf2T; _test.MaxTsTime = mTsT; _test.MaxTcTime = mTcT;
            _test.FinalTf1 = fTf1; _test.FinalTf2 = fTf2; _test.FinalTs = fTs; _test.FinalTc = fTc;
            _test.FinalTf1Time = fTime; _test.FinalTf2Time = fTime; _test.FinalTsTime = fTime; _test.FinalTcTime = fTime;
            _test.DeltaTf1 = fTf1 - amb;
            _test.DeltaTf2 = fTf2 - amb;
            _test.DeltaTs = fTs - amb;
            _test.DeltaTc = fTc - amb;
            // 综合温升 deltatf 取表面温升（文档约定）
            _test.DeltaTf = fTs - amb;
            _test.Memo = _txtMemo.Text;
            _test.Flag = "10000000";

            try
            {
                AppHost.Db.UpdateTestResult(_test);

                // 导出报告
                string excelPath = null, pdfPath = null;
                try
                {
                    excelPath = AppHost.Exporter.ExportExcel(_test, _samples);
                }
                catch (Exception ex) { Serilog.Log.Warning(ex, "Excel 导出失败"); }
                if (AppHost.Settings.Report.EnablePdfExport)
                {
                    try
                    {
                        var chartPath = AppHost.Exporter.ExportChartPng(_test.ProductId, _test.TestId, _samples);
                        pdfPath = AppHost.Exporter.ExportPdf(_test, _samples, chartPath);
                    }
                    catch (Exception ex) { Serilog.Log.Warning(ex, "PDF 导出失败"); }
                }

                SavedTest = _test;
                DialogResult = DialogResult.OK;

                MessageBox.Show(
                    "保存成功！\r\n" +
                    (excelPath != null ? $"Excel：{excelPath}\r\n" : "") +
                    (pdfPath != null ? $"PDF：{pdfPath}\r\n" : ""),
                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
