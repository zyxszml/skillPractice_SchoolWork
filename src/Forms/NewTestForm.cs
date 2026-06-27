using System;
using System.Drawing;
using System.Windows.Forms;
using Iso11820Simulator.Global;
using Iso11820Simulator.Models;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 新建试验窗体：填写样品信息、试验参数、初始质量；设备信息自动带入。
    /// </summary>
    public class NewTestForm : Form
    {
        // 环境信息
        private readonly TextBox _txtAmbTemp = new();
        private readonly TextBox _txtAmbHumi = new();
        // 样品信息
        private readonly TextBox _txtProductId = new();
        private readonly TextBox _txtTestId = new();
        private readonly TextBox _txtProductName = new();
        private readonly TextBox _txtSpecific = new();
        private readonly TextBox _txtDiameter = new();
        private readonly TextBox _txtHeight = new();
        // 试验参数
        private readonly ComboBox _cmbOperator = new();
        private readonly RadioButton _rbStandard = new() { Text = "标准 60 分钟", Checked = true };
        private readonly RadioButton _rbCustom = new() { Text = "自定义（分钟）" };
        private readonly NumericUpDown _numCustomMin = new() { Minimum = 1, Maximum = 600, Value = 60 };
        private readonly TextBox _txtPreWeight = new();
        // 设备（只读）
        private readonly TextBox _txtApparatusId = new() { ReadOnly = true };
        private readonly TextBox _txtApparatusName = new() { ReadOnly = true };
        private readonly TextBox _txtApparatusChk = new() { ReadOnly = true };
        private readonly TextBox _txtConstPower = new() { ReadOnly = true };

        public TestMaster CreatedTest { get; private set; }

        public NewTestForm()
        {
            UiTheme.ApplyForm(this);
            Text = "新建试验";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(660, 700);

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

            AddSection(form, "环境信息");
            AddRow(form, "环境温度(℃)", _txtAmbTemp, "25");
            AddRow(form, "环境湿度(%)", _txtAmbHumi, "50");

            AddSection(form, "样品信息");
            AddRow(form, "样品编号", _txtProductId, DateTime.Now.ToString("yyyyMMdd") + "-001");
            _txtTestId.Text = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            AddRow(form, "试验标识", _txtTestId, "");
            AddRow(form, "样品名称", _txtProductName, "");
            AddRow(form, "规格型号", _txtSpecific, "");
            AddRow(form, "直径(mm)", _txtDiameter, "45");
            AddRow(form, "高度(mm)", _txtHeight, "50");

            AddSection(form, "试验参数");
            _cmbOperator.DropDownStyle = ComboBoxStyle.DropDownList;
            AddRow(form, "操作员", _cmbOperator, "");

            var pnlMode = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = UiTheme.AppBack,
                Margin = new Padding(0, 3, 0, 3)
            };
            _rbStandard.AutoSize = true;
            _rbCustom.AutoSize = true;
            _rbStandard.Margin = new Padding(0, 6, 24, 0);
            _rbCustom.Margin = new Padding(0, 6, 10, 0);
            _numCustomMin.Width = 82;
            _numCustomMin.Enabled = false;
            _rbStandard.CheckedChanged += (s, e) => _numCustomMin.Enabled = _rbCustom.Checked;
            _rbCustom.CheckedChanged += (s, e) => _numCustomMin.Enabled = _rbCustom.Checked;
            pnlMode.Controls.AddRange(new Control[] { _rbStandard, _rbCustom, _numCustomMin });
            AddRow(form, "试验时长", pnlMode, "");
            AddRow(form, "试验前质量(g)", _txtPreWeight, "");

            AddSection(form, "设备信息（自动带入）");
            var app = AppHost.DefaultApparatus;
            if (app != null)
            {
                _txtApparatusId.Text = app.ApparatusId.ToString();
                _txtApparatusName.Text = app.ApparatusName;
                _txtApparatusChk.Text = app.CheckDateF.ToString("yyyy-MM-dd") + " ~ " + app.CheckDateT.ToString("yyyy-MM-dd");
                _txtConstPower.Text = (app.ConstPower ?? AppHost.Settings.Hardware.ConstPower).ToString();
            }
            AddRow(form, "设备编号", _txtApparatusId, "");
            AddRow(form, "设备名称", _txtApparatusName, "");
            AddRow(form, "检定有效期", _txtApparatusChk, "");
            AddRow(form, "恒功率", _txtConstPower, "");

            var btnCreate = new Button { Text = "创建试验" };
            var btnCancel = new Button { Text = "取消" };
            UiTheme.StyleButton(btnCreate, UiButtonKind.Primary);
            UiTheme.StyleButton(btnCancel);
            btnCreate.Click += (s, e) => DoCreate();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UiTheme.AppBack,
                Padding = new Padding(16, 12, 16, 0)
            };
            actions.Controls.Add(btnCreate);
            actions.Controls.Add(btnCancel);

            Controls.Add(UiTheme.CreateDialogShell(body, actions));
            AcceptButton = btnCreate;
            CancelButton = btnCancel;

            Load += (s, e) => LoadOperators();
        }

        private void LoadOperators()
        {
            try
            {
                _cmbOperator.Items.Clear();
                var ops = AppHost.Db.GetOperators();
                foreach (var op in ops)
                    _cmbOperator.Items.Add(op.UserName);
                _cmbOperator.SelectedItem = AppHost.CurrentUser?.UserName ?? "admin";
            }
            catch { }
        }

        private void AddSection(TableLayoutPanel form, string title)
        {
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            var lbl = UiTheme.CreateSectionLabel(title);
            form.Controls.Add(lbl, 0, row);
            form.SetColumnSpan(lbl, 2);
        }

        private void AddRow(TableLayoutPanel form, string label, Control ctrl, string defaultValue)
        {
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var lbl = UiTheme.CreateFieldLabel(label);
            ctrl.Dock = DockStyle.Fill;
            if (!string.IsNullOrEmpty(defaultValue) && ctrl is TextBox tb && string.IsNullOrEmpty(tb.Text))
                tb.Text = defaultValue;
            if (ctrl is TextBox or ComboBox)
                UiTheme.StyleInput(ctrl);
            form.Controls.Add(lbl, 0, row);
            form.Controls.Add(ctrl, 1, row);
        }

        private void DoCreate()
        {
            // 校验
            if (string.IsNullOrWhiteSpace(_txtProductId.Text)) { MessageBox.Show("请输入样品编号"); return; }
            if (string.IsNullOrWhiteSpace(_txtPreWeight.Text) || !double.TryParse(_txtPreWeight.Text, out double prewt) || prewt <= 0)
            { MessageBox.Show("请输入有效的试验前质量"); return; }
            if (!double.TryParse(_txtAmbTemp.Text, out double ambT)) { MessageBox.Show("环境温度无效"); return; }
            if (!double.TryParse(_txtAmbHumi.Text, out double ambH)) { MessageBox.Show("环境湿度无效"); return; }
            if (!double.TryParse(_txtDiameter.Text, out _)) { MessageBox.Show("直径无效"); return; }
            if (!double.TryParse(_txtHeight.Text, out _)) { MessageBox.Show("高度无效"); return; }

            var testId = string.IsNullOrWhiteSpace(_txtTestId.Text)
                ? DateTime.Now.ToString("yyyyMMdd-HHmmss")
                : _txtTestId.Text.Trim();

            // 写入样品表（不存在则插入）
            var product = new ProductMaster
            {
                ProductId = _txtProductId.Text.Trim(),
                ProductName = _txtProductName.Text.Trim(),
                Specific = _txtSpecific.Text.Trim(),
                Diameter = double.Parse(_txtDiameter.Text),
                Height = double.Parse(_txtHeight.Text),
                Flag = ""
            };
            AppHost.Db.UpsertProduct(product);

            var t = new TestMaster
            {
                ProductId = product.ProductId,
                TestId = testId,
                TestDate = DateTime.Today,
                AmbTemp = ambT,
                AmbHumi = ambH,
                According = "ISO 11820:2022",
                Operator = (_cmbOperator.SelectedItem ?? AppHost.CurrentUser?.UserName ?? "admin").ToString(),
                ApparatusId = _txtApparatusId.Text,
                ApparatusName = _txtApparatusName.Text,
                ApparatusChkDate = AppHost.DefaultApparatus?.CheckDateF ?? DateTime.Today,
                RptNo = product.ProductId,
                PreWeight = prewt,
                ConstPower = int.TryParse(_txtConstPower.Text, out int cp) ? cp : AppHost.Settings.Hardware.ConstPower
            };

            try
            {
                AppHost.Db.InsertTest(t);
                // 同步试验模式到控制器
                AppHost.Controller.StandardMode = _rbStandard.Checked;
                AppHost.Controller.TargetDurationSeconds = (int)(_numCustomMin.Value * 60);
                AppHost.Controller.InitialAmbTemp = ambT;

                CreatedTest = t;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message + "\r\n（可能是样品编号已存在或数据库未连通）",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
