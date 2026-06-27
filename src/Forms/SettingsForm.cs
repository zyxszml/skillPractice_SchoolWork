using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Iso11820Simulator.Global;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 参数设置窗体：查看/调整仿真参数与文件路径（运行时只读展示，部分参数实时生效）。
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly TextBox _txtTarget = new();
        private readonly TextBox _txtHeatRate = new();
        private readonly TextBox _txtFluct = new();
        private readonly TextBox _txtStable = new();
        private readonly TextBox _txtDrift = new();
        private readonly TextBox _txtConstPower = new();
        private readonly TextBox _txtBaseDir = new();
        private readonly TextBox _txtReportDir = new();
        private readonly CheckBox _chkPdf = new() { Text = "启用 PDF 导出", AutoSize = true };
        private readonly Button _btnSave = new() { Text = "保存" };
        private readonly Button _btnCancel = new() { Text = "取消" };

        public SettingsForm()
        {
            UiTheme.ApplyForm(this);
            Text = "参数设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(620, 470);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.AppBack
            };

            // ===== 仿真参数 分组 =====
            var grpSim = UiTheme.CreateGroup("仿真参数", 28 + 5 * 38 + 12);
            var formSim = NewInnerForm();
            grpSim.Controls.Add(formSim);
            grpSim.Controls.SetChildIndex(formSim, 0);
            AddRow(formSim, "目标炉温(℃)", _txtTarget); _txtTarget.Text = AppHost.Settings.Simulation.TargetFurnaceTemp.ToString();
            AddRow(formSim, "升温速度(℃/s)", _txtHeatRate); _txtHeatRate.Text = AppHost.Settings.Simulation.HeatingRatePerSecond.ToString();
            AddRow(formSim, "温度波动(℃)", _txtFluct); _txtFluct.Text = AppHost.Settings.Simulation.TempFluctuation.ToString();
            AddRow(formSim, "稳定阈值(℃)", _txtStable); _txtStable.Text = AppHost.Settings.Simulation.StableThreshold.ToString();
            AddRow(formSim, "10分钟最大温漂(℃)", _txtDrift); _txtDrift.Text = AppHost.Settings.Simulation.MaxTemperatureDriftPerTenMinutes.ToString();

            // ===== 设备与报告 分组 =====
            var grpDev = UiTheme.CreateGroup("设备与报告", 28 + 4 * 38 + 12);
            var formDev = NewInnerForm();
            grpDev.Controls.Add(formDev);
            grpDev.Controls.SetChildIndex(formDev, 0);
            AddRow(formDev, "恒功率(0~25600)", _txtConstPower); _txtConstPower.Text = AppHost.Settings.Hardware.ConstPower.ToString();
            AddRow(formDev, "数据基础目录", _txtBaseDir); _txtBaseDir.Text = AppHost.Settings.FileStorage.BaseDirectory;
            AddRow(formDev, "报告输出目录", _txtReportDir); _txtReportDir.Text = AppHost.Settings.Report.OutputDirectory;
            _chkPdf.Checked = AppHost.Settings.Report.EnablePdfExport;
            AddRow(formDev, "报告格式", _chkPdf);

            // Dock=Top：后添加的在上方，使"仿真参数"显示在最上
            body.Controls.Add(grpDev);
            body.Controls.Add(grpSim);

            UiTheme.StyleButton(_btnSave, UiButtonKind.Primary);
            UiTheme.StyleButton(_btnCancel);
            _btnSave.Click += (s, e) => DoSave();
            _btnCancel.Click += (s, e) => Close();
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UiTheme.AppBack,
                Padding = new Padding(16, 12, 16, 0)
            };
            actions.Controls.Add(_btnSave);
            actions.Controls.Add(_btnCancel);

            Controls.Add(UiTheme.CreateDialogShell(body, actions));
            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        /// <summary>分组内部用的两列表单布局（列宽 170 / 100%）。</summary>
        private static TableLayoutPanel NewInnerForm()
        {
            var form = UiTheme.CreateFormTable();
            form.Dock = DockStyle.Fill;
            form.Padding = new Padding(14, 6, 14, 8);
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return form;
        }

        private static void AddRow(TableLayoutPanel form, string label, Control ctrl)
        {
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            ctrl.Dock = DockStyle.Fill;
            if (ctrl is TextBox or ComboBox)
                UiTheme.StyleInput(ctrl);
            if (ctrl is CheckBox chk)
                chk.Margin = new Padding(0, 8, 0, 0);
            form.Controls.Add(UiTheme.CreateFieldLabel(label), 0, row);
            form.Controls.Add(ctrl, 1, row);
        }

        private void DoSave()
        {
            if (!TryReadDouble(_txtTarget, "目标炉温", out double t, min: 0)) return;
            if (!TryReadDouble(_txtHeatRate, "升温速度", out double hr, min: 0.01)) return;
            if (!TryReadDouble(_txtFluct, "温度波动", out double fl, min: 0)) return;
            if (!TryReadDouble(_txtStable, "稳定阈值", out double st, min: 0)) return;
            if (!TryReadDouble(_txtDrift, "10分钟最大温漂", out double dr, min: 0)) return;
            if (!int.TryParse(_txtConstPower.Text, out int cp) || cp < 0 || cp > 25600)
            {
                MessageBox.Show("恒功率必须是 0~25600 的整数。", "参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtConstPower.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtBaseDir.Text))
            {
                MessageBox.Show("数据基础目录不能为空。", "参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtBaseDir.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtReportDir.Text))
            {
                MessageBox.Show("报告输出目录不能为空。", "参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtReportDir.Focus();
                return;
            }

            AppHost.Settings.Simulation.TargetFurnaceTemp = t;
            AppHost.Settings.Simulation.HeatingRatePerSecond = hr;
            AppHost.Settings.Simulation.TempFluctuation = fl;
            AppHost.Settings.Simulation.StableThreshold = st;
            AppHost.Settings.Simulation.MaxTemperatureDriftPerTenMinutes = dr;
            AppHost.Settings.Hardware.ConstPower = cp;
            AppHost.Settings.FileStorage.BaseDirectory = _txtBaseDir.Text;
            AppHost.Settings.FileStorage.TestDataDirectory = Path.Combine(_txtBaseDir.Text, "TestData");
            AppHost.Settings.Report.OutputDirectory = _txtReportDir.Text;
            AppHost.Settings.Report.EnablePdfExport = _chkPdf.Checked;

            try
            {
                Directory.CreateDirectory(AppHost.Settings.FileStorage.TestDataDirectory);
                Directory.CreateDirectory(AppHost.Settings.Report.OutputDirectory);
                AppHost.Settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("配置保存到 appsettings.json 失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 说明：目标炉温、升温速度、温度波动、稳定阈值、最大温漂、恒功率、目录等
            // 因为 Simulator/Controller 持有的是同一个 SimulationSection 对象引用，保存后立即生效；
            // 仅"采样间隔(TickIntervalMs)"由 DaqWorker 在启动时固定，需重启程序才生效。
            MessageBox.Show(
                "参数已保存。\r\n" +
                "• 仿真参数（目标炉温/升温速度/波动/稳定阈值/最大温漂/恒功率/目录）：当前会话立即生效\r\n" +
                "• 采样间隔(TickIntervalMs)：需重启程序后生效",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private static bool TryReadDouble(TextBox textBox, string name, out double value, double min)
        {
            if (!double.TryParse(textBox.Text, out value) || value < min)
            {
                MessageBox.Show($"{name} 必须是不小于 {min} 的数字。", "参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox.Focus();
                return false;
            }
            return true;
        }
    }
}
