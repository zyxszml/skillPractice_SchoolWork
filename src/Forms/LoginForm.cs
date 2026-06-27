using System;
using System.Drawing;
using System.Windows.Forms;
using Iso11820Simulator.Global;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 登录窗体：角色单选 + 密码登录。
    /// 注意：没有用户名输入框，角色决定用户名（管理员=admin / 试验员=experimenter）。
    /// </summary>
    public class LoginForm : Form
    {
        private readonly RadioButton _rbAdmin = new() { Text = "管理员", Checked = true };
        private readonly RadioButton _rbExperimenter = new() { Text = "试验员" };
        private readonly TextBox _txtPwd = new() { UseSystemPasswordChar = true };
        private readonly Button _btnLogin = new() { Text = "登录" };
        private readonly Button _btnCancel = new() { Text = "取消" };
        private readonly Label _lblHint = new();

        public bool LoginSucceeded { get; private set; }

        public LoginForm()
        {
            UiTheme.ApplyForm(this);
            Text = "登录 - ISO 11820 试验仿真系统";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 260);

            UiTheme.StyleInput(_txtPwd);

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.AppBack,
                Padding = new Padding(22, 18, 22, 16)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            var title = new Label
            {
                Text = "ISO 11820 试验仿真系统",
                Dock = DockStyle.Fill,
                Font = UiTheme.TitleFont,
                ForeColor = UiTheme.Text,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var body = UiTheme.CreateSurfacePanel(new Padding(18));
            body.Dock = DockStyle.Fill;
            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = UiTheme.Surface
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var rolePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0)
            };
            _rbAdmin.AutoSize = true;
            _rbExperimenter.AutoSize = true;
            _rbAdmin.Margin = new Padding(0, 8, 28, 0);
            _rbExperimenter.Margin = new Padding(0, 8, 0, 0);
            rolePanel.Controls.AddRange(new Control[] { _rbAdmin, _rbExperimenter });

            _txtPwd.Dock = DockStyle.Fill;
            _txtPwd.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoLogin(); };

            _lblHint.Dock = DockStyle.Fill;
            _lblHint.ForeColor = UiTheme.Danger;
            _lblHint.TextAlign = ContentAlignment.MiddleLeft;
            _lblHint.AutoEllipsis = true;

            form.Controls.Add(UiTheme.CreateFieldLabel("角色"), 0, 0);
            form.Controls.Add(rolePanel, 1, 0);
            form.Controls.Add(UiTheme.CreateFieldLabel("密码"), 0, 1);
            form.Controls.Add(_txtPwd, 1, 1);
            form.Controls.Add(_lblHint, 0, 2);
            form.SetColumnSpan(_lblHint, 2);
            body.Controls.Add(form);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UiTheme.AppBack,
                Padding = new Padding(0, 10, 0, 0)
            };
            UiTheme.StyleButton(_btnLogin, UiButtonKind.Primary);
            UiTheme.StyleButton(_btnCancel);
            _btnLogin.Click += (s, e) => DoLogin();
            _btnCancel.Click += (s, e) => { LoginSucceeded = false; Close(); };
            actions.Controls.Add(_btnLogin);
            actions.Controls.Add(_btnCancel);

            shell.Controls.Add(title, 0, 0);
            shell.Controls.Add(body, 0, 1);
            shell.Controls.Add(actions, 0, 2);
            Controls.Add(shell);

            AcceptButton = _btnLogin;
            Shown += (s, e) => _txtPwd.Focus();
        }

        private void DoLogin()
        {
            string username = _rbAdmin.Checked ? "admin" : "experimenter";
            string pwd = _txtPwd.Text;

            if (string.IsNullOrEmpty(pwd))
            {
                _lblHint.Text = "请输入密码";
                return;
            }

            try
            {
                if (AppHost.Db.Login(username, pwd, out var op))
                {
                    AppHost.CurrentUser = op;
                    LoginSucceeded = true;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    _lblHint.Text = "密码错误，请重新输入";
                    _txtPwd.SelectAll();
                    _txtPwd.Focus();
                }
            }
            catch (Exception ex)
            {
                _lblHint.Text = "数据库错误：" + ex.Message;
            }
        }
    }
}
