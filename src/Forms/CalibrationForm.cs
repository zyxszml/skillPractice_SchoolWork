using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Iso11820Simulator.Global;
using Iso11820Simulator.Models;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 设备校准窗体：实时显示校准温、记录校准数据点、保存到 CalibrationRecords、查看历史。
    /// 注意：本窗体作为非顶级窗体嵌入到 TabPage，Load 事件不会自动触发，
    /// 因此初始化逻辑在构造函数末尾直接调用。
    /// </summary>
    public class CalibrationForm : Form
    {
        private readonly Label _lblCalibTemp = new()
        {
            Text = "0.0",
            Font = new Font("Consolas", 28, FontStyle.Bold),
            ForeColor = Color.FromArgb(200, 200, 200),
            AutoSize = true
        };

        private readonly DataGridView _grid = new() { AllowUserToAddRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Dock = DockStyle.Fill };
        private readonly NumericUpDown _numStdTemp = new() { Minimum = 0, Maximum = 1000, Value = 750 };
        private readonly TextBox _txtMeasured = new();
        private readonly Button _btnAddPoint = new() { Text = "记录点" };
        private readonly Button _btnSave = new() { Text = "保存校准" };
        private readonly TextBox _txtRemarks = new();
        private readonly ComboBox _cmbType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _lblPoints = new()
        {
            Name = "lblPoints",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(28, 132, 82),
            Font = UiTheme.MonoFont,
            Text = "(无)"
        };

        // 临时记录点列表（每次保存后清空）
        private readonly List<(double std, double measured)> _points = new();

        public CalibrationForm()
        {
            UiTheme.ApplyForm(this);
            Text = "设备校准";
            ClientSize = new Size(1100, 600);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.AppBack
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 226));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var pnlTop = UiTheme.CreateSurfacePanel(new Padding(16));
            pnlTop.Dock = DockStyle.Fill;
            pnlTop.Margin = new Padding(12, 12, 12, 6);

            var topLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface
            };
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var tempPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Surface
            };
            tempPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            tempPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            tempPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var lblTitle = new Label
            {
                Text = "校准温度（第5通道）",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.MutedText,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _lblCalibTemp.Dock = DockStyle.Fill;
            _lblCalibTemp.AutoSize = false;
            _lblCalibTemp.ForeColor = UiTheme.Text;
            _lblCalibTemp.TextAlign = ContentAlignment.MiddleLeft;
            var lblUnit = new Label
            {
                Text = "℃",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.MutedText,
                TextAlign = ContentAlignment.TopLeft
            };
            tempPanel.Controls.Add(lblTitle, 0, 0);
            tempPanel.Controls.Add(_lblCalibTemp, 0, 1);
            tempPanel.Controls.Add(lblUnit, 0, 2);

            var editLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 0, 0, 0)
            };
            editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _numStdTemp.Dock = DockStyle.Fill;
            UiTheme.StyleInput(_txtMeasured);
            UiTheme.StyleInput(_txtRemarks);
            UiTheme.StyleInput(_cmbType);
            UiTheme.StyleButton(_btnAddPoint, UiButtonKind.Primary);
            UiTheme.StyleButton(_btnSave, UiButtonKind.Success);
            _btnAddPoint.Click += (s, e) => AddPoint();

            _cmbType.Items.AddRange(new object[] { "Surface", "Center" });
            _cmbType.SelectedIndex = 0;
            _btnSave.Click += (s, e) => DoSave();

            var lblHint = new Label
            {
                Text = "本次记录的点（标准 / 实测 / 偏差）",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.MutedText,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var pnlPoints = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SurfaceAlt,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };
            pnlPoints.Controls.Add(_lblPoints);

            editLayout.Controls.Add(UiTheme.CreateFieldLabel("标准温度(℃)"), 0, 0);
            editLayout.Controls.Add(_numStdTemp, 1, 0);
            editLayout.Controls.Add(UiTheme.CreateFieldLabel("实测温度(℃)"), 2, 0);
            editLayout.Controls.Add(_txtMeasured, 3, 0);
            editLayout.Controls.Add(UiTheme.CreateFieldLabel("类型"), 0, 1);
            editLayout.Controls.Add(_cmbType, 1, 1);
            editLayout.Controls.Add(UiTheme.CreateFieldLabel("备注"), 2, 1);
            editLayout.Controls.Add(_txtRemarks, 3, 1);
            editLayout.Controls.Add(_btnAddPoint, 1, 2);
            editLayout.Controls.Add(_btnSave, 3, 2);
            editLayout.Controls.Add(lblHint, 0, 3);
            editLayout.SetColumnSpan(lblHint, 2);
            editLayout.Controls.Add(pnlPoints, 2, 3);
            editLayout.SetColumnSpan(pnlPoints, 2);

            topLayout.Controls.Add(tempPanel, 0, 0);
            topLayout.Controls.Add(editLayout, 1, 0);
            pnlTop.Controls.Add(topLayout);

            var gridPanel = UiTheme.CreateSurfacePanel(new Padding(0));
            gridPanel.Dock = DockStyle.Fill;
            gridPanel.Margin = new Padding(12, 6, 12, 12);
            UiTheme.StyleGrid(_grid);
            gridPanel.Controls.Add(_grid);

            layout.Controls.Add(pnlTop, 0, 0);
            layout.Controls.Add(gridPanel, 0, 1);
            Controls.Add(layout);

            // 嵌入 TabPage 时 Load 事件不触发，构造函数末尾直接初始化
            RefreshData();
        }

        public void RefreshData()
        {
            try
            {
                var list = AppHost.Db.QueryCalibrations(DateTime.Today.AddMonths(-6), DateTime.Today);
                _grid.DataSource = list.Select(c => new
                {
                    校准日期 = c.CalibrationDate,
                    类型 = c.CalibrationType,
                    操作员 = c.Operator,
                    设备ID = c.ApparatusId,
                    均温 = c.AverageTemperature?.ToString("F1") ?? "-",
                    最大偏差 = c.MaxDeviation?.ToString("F2") ?? "-",
                    均匀性 = c.UniformityResult?.ToString("F2") ?? "-",
                    是否通过 = c.PassedCriteria == 1 ? "通过" : "未通过",
                    备注 = c.Remarks
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RefreshData: " + ex.Message);
            }
        }

        private void AddPoint()
        {
            double std = (double)_numStdTemp.Value;
            if (!double.TryParse(_txtMeasured.Text, out double measured))
            {
                // 用当前校准温作为默认实测值
                measured = AppHost.Simulator?.TCal ?? std;
            }
            _points.Add((std, measured));
            UpdatePointsLabel();
            _txtMeasured.Clear();
        }

        private void UpdatePointsLabel()
        {
            if (_points.Count == 0) { _lblPoints.Text = "(无)"; return; }
            _lblPoints.Text = string.Join("\r\n", _points.Select(p => $"标准 {p.std:F1} ℃  实测 {p.measured:F1} ℃  偏差 {p.measured - p.std:+0.00;-0.00;0.00} ℃"));
        }

        private void DoSave()
        {
            if (_points.Count == 0) { MessageBox.Show("请先记录至少一个点"); return; }
            var devs = _points.Select(p => p.measured - p.std).ToList();
            double avg = _points.Average(p => p.measured);
            double maxDev = devs.Max(d => Math.Abs(d));
            double uniformity = 0;

            var rec = new CalibrationRecord
            {
                Id = Guid.NewGuid().ToString(),
                CalibrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                CalibrationType = _cmbType.SelectedItem?.ToString() ?? "Surface",
                ApparatusId = AppHost.DefaultApparatus?.ApparatusId ?? 1,
                Operator = AppHost.CurrentUser?.UserName ?? "admin",
                TemperatureData = System.Text.Json.JsonSerializer.Serialize(_points.Select(p => new
                {
                    StandardTemperature = p.std,
                    MeasuredTemperature = p.measured,
                    Deviation = p.measured - p.std
                })),
                UniformityResult = uniformity,
                MaxDeviation = maxDev,
                AverageTemperature = avg,
                PassedCriteria = maxDev <= 5 ? 1 : 0,
                Remarks = _txtRemarks.Text,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            try
            {
                AppHost.Db.InsertCalibration(rec);
                MessageBox.Show("校准记录已保存。\r\n最大偏差：" + maxDev.ToString("F2") + " ℃", "完成");
                _points.Clear();
                UpdatePointsLabel();
                _txtRemarks.Clear();
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message, "错误");
            }
        }

        /// <summary>主界面切换到此 Tab 时调用，刷新校准温显示。</summary>
        public void TickCalibTemp(double tcal)
        {
            _lblCalibTemp.Text = tcal.ToString("F1");
        }
    }
}
