using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Iso11820Simulator.Core;
using Iso11820Simulator.Global;
using Iso11820Simulator.Models;
using Iso11820Simulator.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 主窗体：状态机驱动 UI、实时 4 通道曲线、消息区、按钮矩阵。
    /// </summary>
    public class MainForm : Form
    {
        // ---------- 顶部状态 ----------
        private readonly Label _lblState = new();
        private readonly Label _lblProductId = new();
        private readonly Label _lblTimer = new();
        private readonly Label _lblDrift = new();
        private readonly Label _lblOperator = new();

        // 状态指示灯（自绘 Panel）
        private Panel _lamp;
        private LampColor _lampColor = LampColor.Gray;
        private bool _lampPulse = false;
        private readonly System.Windows.Forms.Timer _lampTimer = new() { Interval = 600 };

        // 记录计时进度条（标准 30 分钟）
        private ProgressBar _timerBar;

        // ---------- 数据库连接状态横幅 ----------
        private readonly Label _dbBanner = new()
        {
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UiTheme.BaseFont
        };
        private readonly System.Windows.Forms.Timer _dbCheckTimer = new() { Interval = 15000 };
        // 横幅闪烁动画（离线时切换深红/亮红）
        private readonly System.Windows.Forms.Timer _dbBannerTimer = new() { Interval = 700 };
        private bool _dbBannerOn = true;

        // ---------- LED 温度面板 ----------
        private readonly Label[] _lblTemps = new Label[5];
        private readonly ProgressBar[] _tempBars = new ProgressBar[5];
        private static readonly string[] _tempNames = { "炉温1", "炉温2", "表面温", "中心温", "校准温" };
        private static readonly Color[] _tempColors =
        {
            Color.FromArgb(255, 80, 80),
            Color.FromArgb(255, 140, 0),
            Color.FromArgb(80, 200, 255),
            Color.FromArgb(80, 255, 160),
            Color.FromArgb(200, 200, 200)
        };

        // ---------- 曲线 ----------
        private PlotView _plotView;
        private PlotModel _plotModel;
        private LineSeries _sTf1, _sTf2, _sTs, _sTc;
        private LinearAxis _xAxis;
        private LinearAxis _yAxis;
        /// <summary>是否启用 Y 轴自动聚焦（开始升温后开启），让曲线放大到实际数据范围。</summary>
        private bool _autoFocusY = false;
        private const int MaxPoints = 750; // 10 分钟，每 0.8 秒 1 点

        // ---------- 消息区 ----------
        private readonly RichTextBox _rtbLog = new();

        // ---------- 按钮 ----------
        private readonly Button _btnNewTest = new() { Text = "新建试验" };
        private readonly Button _btnStartHeat = new() { Text = "开始升温" };
        private readonly Button _btnStopHeat = new() { Text = "停止升温" };
        private readonly Button _btnStartRec = new() { Text = "开始记录" };
        private readonly Button _btnStopRec = new() { Text = "停止记录" };
        private readonly Button _btnTestRecord = new() { Text = "试验记录" };
        private readonly Button _btnSettings = new() { Text = "参数设置" };

        // ---------- Tab ----------
        private readonly TabControl _tabs = new();
        private readonly TabPage _tabControl = new("试验控制");
        private readonly TabPage _tabHistory = new("记录查询");
        private readonly TabPage _tabCalib = new("设备校准");

        // ---------- 子页面 ----------
        private HistoryForm _historyForm;
        private CalibrationForm _calibForm;

        // ---------- 当前试验缓存 ----------
        private TestMaster _currentTest;
        private bool _testRecordSaved = true;

        public MainForm()
        {
            UiTheme.ApplyForm(this);
            Text = "ISO 11820 建材不燃性试验仿真系统";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(1080, 720);

            BuildControlTab();
            BuildHistoryTab();
            BuildCalibTab();

            _tabs.Dock = DockStyle.Fill;
            UiTheme.StyleTabControl(_tabs);
            // Tab 图标：试验控制/记录查询/设备校准
            _tabs.ImageList = UiTheme.CreateTabImageList();
            _tabControl.ImageIndex = 0;
            _tabHistory.ImageIndex = 1;
            _tabCalib.ImageIndex = 2;
            _tabs.TabPages.AddRange(new[] { _tabControl, _tabHistory, _tabCalib });
            _tabs.SelectedIndexChanged += (s, e) =>
            {
                if (_tabs.SelectedTab == _tabHistory) _historyForm?.RefreshData();
                if (_tabs.SelectedTab == _tabCalib) _calibForm?.RefreshData();
            };

            Controls.Add(_tabs);

            Load += OnLoad;
            FormClosing += OnClosing;
        }

        // ============================================================
        //  控制页布局
        // ============================================================
        private void BuildControlTab()
        {
            _tabControl.BackColor = UiTheme.AppBack;

            // ===== 顶部状态栏 =====
            // 两行布局：第一行标题（居中），第二行状态信息（横向排列、不换行、不省略）。
            var pnlTop = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 86,
                BackColor = UiTheme.HeaderBack,
                Padding = new Padding(18, 8, 18, 8),
                ColumnCount = 1,
                RowCount = 2
            };
            pnlTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            pnlTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var lblTitle = UiTheme.CreateTitle("ISO 11820 建材不燃性试验仿真系统");
            pnlTop.Controls.Add(lblTitle, 0, 0);

            // 第二行：状态项横向排列，AutoSize 自适应宽度，WrapContents=false 不换行
            var statusRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = UiTheme.HeaderBack,
                Margin = new Padding(0)
            };

            _lblOperator.Text = "操作员：未登录";
            _lblState.Text = "状态：-";
            _lblProductId.Text = "样品编号：-";
            _lblTimer.Text = "计时：0 s";
            _lblDrift.Text = "温漂：- ℃/10min";

            StyleHeaderLabel(_lblOperator, Color.FromArgb(220, 226, 235));
            StyleHeaderLabel(_lblState, Color.FromArgb(255, 217, 102));
            StyleHeaderLabel(_lblProductId, Color.FromArgb(220, 226, 235));
            StyleHeaderLabel(_lblTimer, Color.FromArgb(124, 218, 161));
            StyleHeaderLabel(_lblDrift, Color.FromArgb(126, 203, 255));

            // 状态指示灯：自绘 14x14 圆形灯，跟随状态变色
            _lamp = new Panel
            {
                Size = new Size(16, 16),
                Margin = new Padding(0, 9, 8, 0),
                BackColor = UiTheme.HeaderBack
            };
            _lamp.Paint += (s, e) =>
                UiTheme.DrawStatusLamp(e.Graphics, new Rectangle(0, 0, 16, 16), _lampColor, _lampPulse);
            _lampTimer.Tick += (s, e) =>
            {
                // 仅 Recording 状态下脉冲；其余状态静态显示
                if (_lampColor != LampColor.Green) return;
                _lampPulse = !_lampPulse;
                _lamp.Invalidate();
            };

            // 记录计时进度条：标准 30 分钟（1800 s）
            _timerBar = new ProgressBar
            {
                Width = 110,
                Height = 12,
                Minimum = 0,
                Maximum = 1800,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                Margin = new Padding(0, 12, 6, 0),
                ForeColor = Color.FromArgb(124, 218, 161)
            };

            statusRow.Controls.Add(_lamp);
            statusRow.Controls.Add(_lblOperator);
            statusRow.Controls.Add(_lblState);
            statusRow.Controls.Add(_lblProductId);
            statusRow.Controls.Add(_lblTimer);
            statusRow.Controls.Add(_timerBar);
            statusRow.Controls.Add(_lblDrift);
            pnlTop.Controls.Add(statusRow, 0, 1);

            // ===== LED 温度面板（左侧）=====
            var pnlLed = new TableLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = UiTheme.PanelBack,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 5
            };
            for (int i = 0; i < 5; i++)
            {
                pnlLed.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
                pnlLed.Controls.Add(CreateTemperatureCard(i), 0, i);
            }

            // ===== 曲线（中部）=====
            BuildPlot();

            // ===== 按钮区（底部）=====
            var pnlBtnHost = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = UiTheme.PanelBack, Padding = new Padding(0) };
            var pnlBtn = UiTheme.CreateActionBar();
            var btns = new[] { _btnNewTest, _btnStartHeat, _btnStopHeat, _btnStartRec, _btnStopRec, _btnTestRecord, _btnSettings };
            foreach (var b in btns)
            {
                b.Width = 112;
                UiTheme.StyleButton(b);
            }
            UiTheme.StyleButton(_btnNewTest, UiButtonKind.Primary);
            UiTheme.StyleButton(_btnStartHeat, UiButtonKind.Success);
            UiTheme.StyleButton(_btnStartRec, UiButtonKind.Success);
            UiTheme.StyleButton(_btnStopHeat, UiButtonKind.Danger);
            UiTheme.StyleButton(_btnStopRec, UiButtonKind.Danger);

            // 视觉分组：[新建] | [开始升温 停止升温] | [开始记录 停止记录] | [试验记录 参数设置]
            pnlBtn.Controls.Add(_btnNewTest);
            pnlBtn.Controls.Add(UiTheme.CreateButtonDivider(28));
            pnlBtn.Controls.Add(_btnStartHeat);
            pnlBtn.Controls.Add(_btnStopHeat);
            pnlBtn.Controls.Add(UiTheme.CreateButtonDivider(28));
            pnlBtn.Controls.Add(_btnStartRec);
            pnlBtn.Controls.Add(_btnStopRec);
            pnlBtn.Controls.Add(UiTheme.CreateButtonDivider(28));
            pnlBtn.Controls.Add(_btnTestRecord);
            pnlBtn.Controls.Add(_btnSettings);
            pnlBtnHost.Controls.Add(pnlBtn);
            _btnNewTest.Click += (s, e) => OnNewTest();
            _btnStartHeat.Click += (s, e) => OnStartHeat();
            _btnStopHeat.Click += (s, e) => OnStopHeat();
            _btnStartRec.Click += (s, e) => OnStartRec();
            _btnStopRec.Click += (s, e) => OnStopRec();
            _btnTestRecord.Click += (s, e) => OnTestRecord();
            _btnSettings.Click += (s, e) =>
            {
                using var f = new SettingsForm();
                f.ShowDialog(this);
            };

            // ===== 消息区（右下）=====
            _rtbLog.Dock = DockStyle.Fill;
            _rtbLog.BackColor = Color.FromArgb(18, 24, 32);
            _rtbLog.ForeColor = Color.FromArgb(230, 235, 242);
            _rtbLog.ReadOnly = true;
            _rtbLog.BorderStyle = BorderStyle.None;
            _rtbLog.Font = UiTheme.MonoFont;
            _rtbLog.Padding = new Padding(8);

            var pnlMid = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.AppBack, Padding = new Padding(12) };
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 8,
                FixedPanel = FixedPanel.Panel2,
                BackColor = UiTheme.AppBack
            };
            split.Layout += (s, e) =>
            {
                const int panel1Min = 520;
                const int panel2Min = 260;
                if (split.Width <= panel1Min + panel2Min + split.SplitterWidth)
                    return;

                var desiredRight = Math.Min(380, Math.Max(300, split.Width / 3));
                var desiredDistance = split.Width - desiredRight - split.SplitterWidth;
                desiredDistance = Math.Max(panel1Min, Math.Min(desiredDistance, split.Width - panel2Min - split.SplitterWidth));
                if (desiredDistance != split.SplitterDistance)
                    split.SplitterDistance = desiredDistance;
            };

            var pnlChart = UiTheme.CreateSurfacePanel(new Padding(10));
            pnlChart.Dock = DockStyle.Fill;
            _plotView.Dock = DockStyle.Fill;

            var pnlMsg = UiTheme.CreateSurfacePanel(new Padding(0));
            pnlMsg.Dock = DockStyle.Fill;
            var lblMsg = new Label
            {
                Text = "系统消息",
                Dock = DockStyle.Top,
                ForeColor = UiTheme.Text,
                BackColor = Color.FromArgb(235, 240, 247),
                Height = 34,
                Font = UiTheme.SectionFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            pnlMsg.Controls.Add(_rtbLog);
            pnlMsg.Controls.Add(lblMsg);

            pnlChart.Controls.Add(_plotView);
            // 曲线空状态提示：未开始试验时显示，有数据后隐藏
            _plotEmptyHint = UiTheme.CreateEmptyHint(pnlChart,
                "暂无曲线数据\r\n点击「新建试验」→「开始升温」开始");
            _plotEmptyHint.BringToFront();
            split.Panel1.Controls.Add(pnlChart);
            split.Panel2.Controls.Add(pnlMsg);
            pnlMid.Controls.Add(split);

            _tabControl.Controls.Add(_dbBanner);
            _tabControl.Controls.Add(pnlTop);
            _tabControl.Controls.Add(pnlMid);
            _tabControl.Controls.Add(pnlLed);
            _tabControl.Controls.Add(pnlBtnHost);

            _tabControl.Controls.SetChildIndex(pnlLed, 0);
            _tabControl.Controls.SetChildIndex(pnlBtnHost, 1);
            _tabControl.Controls.SetChildIndex(pnlTop, 2);
            _tabControl.Controls.SetChildIndex(pnlMid, 3);
            // 横幅放在最上层（最大索引），使其停靠在最顶部
            _tabControl.Controls.SetChildIndex(_dbBanner, 4);
        }

        private static void StyleHeaderLabel(Label label, Color color)
        {
            // AutoSize：按文字实际宽度自适应，绝不省略/截断
            label.AutoSize = true;
            label.ForeColor = color;
            label.BackColor = UiTheme.HeaderBack;
            label.Margin = new Padding(0, 8, 22, 0);
            label.Padding = new Padding(0, 2, 0, 0);
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        /// <summary>同步更新某通道的温度数值与进度条（量程=目标炉温）。</summary>
        private void UpdateTemp(int index, double value)
        {
            _lblTemps[index].Text = value.ToString("F1");
            var bar = _tempBars[index];
            if (bar != null)
            {
                int max = bar.Maximum > 0 ? bar.Maximum : 1;
                bar.Value = Math.Max(0, Math.Min(max, (int)Math.Round(value)));
            }
        }

        private Control CreateTemperatureCard(int index)
        {
            var card = UiTheme.CreateSurfacePanel(new Padding(12, 10, 12, 10));
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, index == 0 ? 0 : 8, 0, 0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Surface
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));   // 进度条
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblName = new Label
            {
                Text = _tempNames[index],
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.MutedText,
                Font = UiTheme.SmallFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            // 温度进度条：满量程=目标炉温，让升温过程可视化
            var bar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = (int)Math.Max(100, AppHost.Settings.Simulation.TargetFurnaceTemp),
                Value = 0,
                Height = 10,
                Margin = new Padding(0, 2, 0, 4),
                ForeColor = _tempColors[index]
            };
            _tempBars[index] = bar;
            _lblTemps[index] = new Label
            {
                Text = "0.0",
                Dock = DockStyle.Fill,
                ForeColor = _tempColors[index],
                Font = UiTheme.MetricFont,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = false,
                Margin = new Padding(0)
            };
            var valueRow = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0)
            };
            var unit = new Label
            {
                Text = "°C",
                Dock = DockStyle.Right,
                Width = 52,
                ForeColor = _tempColors[index],
                Font = UiTheme.SectionFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };

            layout.Controls.Add(lblName, 0, 0);
            layout.Controls.Add(bar, 0, 1);
            valueRow.Controls.Add(_lblTemps[index]);
            valueRow.Controls.Add(unit);
            layout.Controls.Add(valueRow, 0, 2);
            card.Controls.Add(layout);
            return card;
        }

        private void BuildPlot()
        {
            _plotView = new PlotView { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            _plotModel = new PlotModel
            {
                Background = OxyColor.FromRgb(248, 250, 252),
                PlotAreaBackground = OxyColor.FromRgb(241, 245, 249),
                TextColor = OxyColor.FromRgb(31, 41, 55),
                PlotAreaBorderColor = OxyColor.FromRgb(148, 163, 184),
                // 嵌入式图例：贴右侧竖排，并留出上边距，避免第一行（炉温1）贴顶被遮挡
                // OxyPlot 2.1 起，图例属性迁移到独立的 Legend 对象（PlotModel.Legends 集合）
                Legends =
                {
                    new Legend
                    {
                        LegendPlacement = LegendPlacement.Inside,
                        LegendPosition = LegendPosition.RightTop,
                        LegendBackground = OxyColor.FromArgb(220, 255, 255, 255),
                        LegendBorder = OxyColor.FromRgb(203, 213, 225),
                        LegendBorderThickness = 1,
                        LegendFontSize = 11,
                        LegendTextColor = OxyColor.FromRgb(31, 41, 55),
                        LegendItemAlignment = OxyPlot.HorizontalAlignment.Left,
                        LegendMargin = 58,
                        LegendPadding = 10
                    }
                }
            };
            _xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "时间(s)",
                Minimum = 0,
                Maximum = 600,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromRgb(203, 213, 225),
                MinorGridlineColor = OxyColor.FromRgb(226, 232, 240),
                AxislineColor = OxyColor.FromRgb(100, 116, 139),
                TicklineColor = OxyColor.FromRgb(100, 116, 139)
            };
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "温度(℃)",
                Minimum = 0,
                Maximum = 800,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromRgb(203, 213, 225),
                AxislineColor = OxyColor.FromRgb(100, 116, 139),
                TicklineColor = OxyColor.FromRgb(100, 116, 139)
            };
            _yAxis = yAxis;
            _plotModel.Axes.Add(_xAxis);
            _plotModel.Axes.Add(yAxis);

            _sTf1 = MkSeries("炉温1", OxyColor.FromRgb(220, 38, 38));
            _sTf2 = MkSeries("炉温2", OxyColor.FromRgb(217, 119, 6));
            _sTs = MkSeries("表面温", OxyColor.FromRgb(37, 99, 235));
            _sTc = MkSeries("中心温", OxyColor.FromRgb(22, 163, 74));
            _plotModel.Series.Add(_sTf1);
            _plotModel.Series.Add(_sTf2);
            _plotModel.Series.Add(_sTs);
            _plotModel.Series.Add(_sTc);
            _plotView.Model = _plotModel;

            // 用户手动操作曲线图时暂停自动聚焦（让出控制权给用户缩放/平移）。
            // 双击恢复自动聚焦。
            _plotView.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    _autoFocusY = !_autoFocusY;
                    AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                        _autoFocusY ? "曲线视角：自动跟随已开启" : "曲线视角：自动跟随已暂停（可手动缩放/平移）",
                        MessageColor.Normal);
                    if (_autoFocusY) { _focusFrameCounter = 5; AutoFocusYTick(); _plotModel.InvalidatePlot(true); }
                    return;
                }
                if (_autoFocusY)
                {
                    _autoFocusY = false;
                    AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                        "检测到手动操作，曲线视角自动跟随已暂停（右键恢复）", MessageColor.Normal);
                }
            };
        }

        private static LineSeries MkSeries(string title, OxyColor color)
            => new()
            {
                Title = title,
                Color = color,
                StrokeThickness = 2.4,
                MarkerType = MarkerType.None
            };

        private void BuildHistoryTab()
        {
            _historyForm = new HistoryForm { Dock = DockStyle.Fill, TopLevel = false, FormBorderStyle = FormBorderStyle.None };
            _tabHistory.Controls.Add(_historyForm);
            _historyForm.Show();
        }

        private void BuildCalibTab()
        {
            _calibForm = new CalibrationForm { Dock = DockStyle.Fill, TopLevel = false, FormBorderStyle = FormBorderStyle.None };
            _tabCalib.Controls.Add(_calibForm);
            _calibForm.Show();
        }

        // ============================================================
        //  生命周期
        // ============================================================
        private void OnLoad(object sender, EventArgs e)
        {
            _lblOperator.Text = "操作员：" + AppHost.CurrentUser.UserName;

            // 启动后台采集
            AppHost.Controller.DataBroadcast += OnDataBroadcast;
            AppHost.Daq.Start();

            AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                $"系统初始化，操作员：{AppHost.CurrentUser.UserName}", MessageColor.Normal);

            // 启动时检测未保存试验
            var unsaved = AppHost.Db.GetLastUnsavedTest();
            if (unsaved != null)
            {
                _currentTest = unsaved;
                _testRecordSaved = false;
                LoadSamplesForCurrentTest();
                AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                    $"检测到未保存的试验记录：{unsaved.ProductId}/{unsaved.TestId}，请先保存。",
                    MessageColor.Warning);
            }

            UpdateButtonStates();
            UpdateStatusBar();

            // 数据库连接状态横幅：启动即检测，之后每 15 秒复检
            RefreshDbBanner();
            _dbCheckTimer.Tick += (s, e) => RefreshDbBanner();
            _dbCheckTimer.Start();
            // 离线横幅闪烁动画（在 RefreshDbBanner 中按可见性启停）
            _dbBannerTimer.Tick += (s, e) =>
            {
                _dbBannerOn = !_dbBannerOn;
                _dbBanner.BackColor = _dbBannerOn
                    ? Color.FromArgb(190, 65, 56)   // 深红
                    : Color.FromArgb(232, 92, 80);  // 亮红
            };
            // 状态灯动画
            _lampTimer.Start();
        }

        /// <summary>检测数据库连接并刷新顶部横幅（在线隐藏，离线红色提示）。</summary>
        private void RefreshDbBanner()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                var (ok, err) = AppHost.Db.CheckConnection();
                if (ok)
                {
                    _dbBanner.Visible = false;
                    _dbBanner.Height = 0;
                    _dbBannerTimer.Stop();
                }
                else
                {
                    _dbBanner.Visible = true;
                    _dbBanner.Height = 26;
                    _dbBanner.Text = "⚠ 数据库连接异常：" + (err ?? "未知原因") + "  ——  历史查询/试验保存等数据库功能将不可用，请检查程序目录读写权限及 appsettings.json 连接字符串";
                    _dbBanner.BackColor = Color.FromArgb(190, 65, 56);
                    _dbBanner.ForeColor = Color.White;
                    _dbBannerOn = true;
                    _dbBannerTimer.Start();
                }
            }
            catch
            {
                // 探测自身异常不影响主流程
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _dbCheckTimer.Stop();
                _dbBannerTimer.Stop();
                _lampTimer.Stop();
                AppHost.Controller.DataBroadcast -= OnDataBroadcast;
                AppHost.Daq.Stop();
            }
            catch { }
        }

        // ============================================================
        //  跨线程事件回调（必须 Invoke）
        // ============================================================
        private void OnDataBroadcast(object sender, DataBroadcastEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                Invoke(new Action(() =>
                {
                    var s = e.Snapshot;
                    if (s == null) return;

                    // 更新 LED 数值与进度条
                    UpdateTemp(0, s.Tf1);
                    UpdateTemp(1, s.Tf2);
                    UpdateTemp(2, s.Ts);
                    UpdateTemp(3, s.Tc);
                    UpdateTemp(4, s.TCal);
                    _calibForm?.TickCalibTemp(s.TCal);

                    // 更新状态栏
                    _lblState.Text = "状态：" + s.State.ToChinese();
                    _lblTimer.Text = "计时：" + s.RecordSeconds + " s";
                    _lblDrift.Text = "温漂：" + (s.DriftTf1 == null ? "-" : s.DriftTf1.Value.ToString("F2") + " ℃/10min");

                    // 状态指示灯 + 计时进度条同步
                    var newLamp = s.State switch
                    {
                        TestState.Idle => LampColor.Gray,
                        TestState.Preparing => LampColor.Amber,
                        TestState.Ready => LampColor.Yellow,
                        TestState.Recording => LampColor.Green,
                        TestState.Complete => LampColor.Blue,
                        _ => LampColor.Gray
                    };
                    if (newLamp != _lampColor)
                    {
                        _lampColor = newLamp;
                        _lampPulse = false;
                        _lamp.Invalidate();
                    }
                    _timerBar.Value = Math.Min(_timerBar.Maximum, s.RecordSeconds);

                    // 追加消息
                    foreach (var msg in e.Messages)
                    {
                        AppendMessage(msg.Time, msg.Message, msg.Color);
                    }

                    // Recording 状态写入实时 CSV 缓存（每秒一行）；Complete 快照补上结束瞬间最后一秒。
                    if ((s.State == TestState.Recording || s.State == TestState.Complete) && _currentTest != null && !_testRecordSaved)
                    {
                        // 仅在整秒边界记录
                        if (s.RecordSeconds != _lastRecordSec)
                        {
                            _lastRecordSec = s.RecordSeconds;
                            AppHost.LiveSamples.Enqueue(new TempSample
                            {
                                Time = s.RecordSeconds,
                                Tf1 = s.Tf1, Tf2 = s.Tf2, Ts = s.Ts, Tc = s.Tc, TCal = s.TCal
                            });
                        }
                    }

                    // 状态变化时刷新按钮
                    if (s.State != _lastState)
                    {
                        _lastState = s.State;
                        OnStateChanged(s.State);
                    }

                    // 曲线展示从升温开始到停止升温降温的连续实时过程；记录数据仍单独使用 RecordSeconds。
                    // 包含 Idle：停止升温后炉温缓慢冷却，降温曲线需继续绘制。
                    if (s.State == TestState.Idle
                        || s.State == TestState.Preparing
                        || s.State == TestState.Ready
                        || s.State == TestState.Recording
                        || s.State == TestState.Complete)
                    {
                        AppendRealtimePoint(s);
                    }

                    UpdateButtonStates();
                }));
            }
            catch (InvalidOperationException)
            {
                // 窗体已关闭，忽略
            }
        }
        private TestState _lastState = TestState.Idle;
        private int _lastRecordSec = -1;
        private double _lastPlotTime = -1;
        private Label _plotEmptyHint;

        private void OnStateChanged(TestState newState)
        {
            if (newState == TestState.Complete && _currentTest != null)
            {
                _currentTest.TotalTestTime = AppHost.Controller.RecordSeconds;
                if (AppHost.Controller.ConstPowerAverage > 0)
                    _currentTest.ConstPower = AppHost.Controller.ConstPowerAverage;
                _currentTest.Flag = null;

                try
                {
                    AppHost.Db.MarkTestCompletedPending(
                        _currentTest.ProductId,
                        _currentTest.TestId,
                        _currentTest.TotalTestTime,
                        _currentTest.ConstPower);
                }
                catch (Exception ex)
                {
                    AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                        "完成状态写入数据库失败：" + ex.Message, MessageColor.Error);
                }

                // 自动写 CSV
                try
                {
                    var samples = GetOrderedLiveSamples();
                    var csvPath = AppHost.Exporter.ExportCsv(
                        _currentTest.ProductId, _currentTest.TestId, samples);
                    AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                        $"CSV 已保存：{csvPath}", MessageColor.Normal);
                }
                catch (Exception ex)
                {
                    AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                        "CSV 写入失败：" + ex.Message, MessageColor.Error);
                }
            }
        }

        private static void AppendPoint(LineSeries s, double x, double y)
        {
            s.Points.Add(new DataPoint(x, y));
            while (s.Points.Count > MaxPoints) s.Points.RemoveAt(0);
        }

        private void AppendRealtimePoint(SensorSnapshot s)
        {
            var x = s.SimElapsedSeconds;
            if (x < 0 || x <= _lastPlotTime + 0.001) return;

            _lastPlotTime = x;
            AppendPoint(_sTf1, x, s.Tf1);
            AppendPoint(_sTf2, x, s.Tf2);
            AppendPoint(_sTs, x, s.Ts);
            AppendPoint(_sTc, x, s.Tc);

            // 有数据后隐藏空状态提示
            if (_plotEmptyHint != null && _plotEmptyHint.Visible)
                _plotEmptyHint.Hide();

            // X 轴跟随：数据未满 10 分钟时，按当前数据量适当放大横轴（预留右侧空间），
            // 让升温初期的曲线也能横向展开；超过 600 秒后改为滚动窗口。
            if (x <= 600)
            {
                double desiredMax = Math.Max(60, x * 1.25 + 10); // 留 25% 余量，最少 60s
                if (desiredMax > _xAxis.Maximum + 1 || desiredMax < _xAxis.Maximum * 0.6)
                {
                    _xAxis.Minimum = 0;
                    _xAxis.Maximum = Math.Min(600, desiredMax);
                }
            }
            else if (x > _xAxis.Maximum)
            {
                _xAxis.Minimum = Math.Max(0, x - 600);
                _xAxis.Maximum = x;
            }

            // 视角自动聚焦：Y 轴平滑地向数据范围靠拢，曲线放大居中显示
            if (_autoFocusY)
                AutoFocusYTick();

            _plotModel.InvalidatePlot(true);
        }

        // 自动聚焦的当前显示范围与节流计数
        private double _focusCurMin = 0, _focusCurMax = 800;
        private int _focusFrameCounter = 0;

        /// <summary>
        /// 计算数据的目标 Y 范围，并把当前显示范围平滑地向目标过渡，
        /// 同时节流（每若干 tick 才重算目标），避免逐帧抖动。
        /// </summary>
        private void AutoFocusYTick()
        {
            // 每 5 个 tick（约 4 秒）重算一次目标范围，期间只做平滑过渡
            if (++_focusFrameCounter < 5) return;
            _focusFrameCounter = 0;

            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (var s in new[] { _sTf1, _sTf2, _sTs, _sTc })
            {
                foreach (var p in s.Points)
                {
                    if (p.Y < min) min = p.Y;
                    if (p.Y > max) max = p.Y;
                }
            }
            if (min > max) return; // 无数据

            // 目标范围：上下各留 10% 余量，最小可视跨度 40℃
            double span = Math.Max(40, max - min);
            double pad = span * 0.10;
            double targetMin = Math.Max(0, min - pad);
            double targetMax = Math.Min(900, max + pad);
            if (targetMax - targetMin < 40) targetMax = targetMin + 40;

            // 平滑过渡：每次朝目标移动 25%，避免突变跳动
            _focusCurMin += (targetMin - _focusCurMin) * 0.25;
            _focusCurMax += (targetMax - _focusCurMax) * 0.25;

            _yAxis.Minimum = _focusCurMin;
            _yAxis.Maximum = _focusCurMax;
        }

        /// <summary>开启 Y 轴自动聚焦，立即按当前数据放大一次。</summary>
        private void EnableAutoFocusY()
        {
            _autoFocusY = true;
            _focusFrameCounter = 5; // 立即触发一次
            AutoFocusYTick();
            _plotModel.InvalidatePlot(true);
        }

        private void DisableAutoFocusY()
        {
            _autoFocusY = false;
            _focusCurMin = 0;
            _focusCurMax = 800;
            _yAxis.Minimum = 0;
            _yAxis.Maximum = 800;
            _plotModel.InvalidatePlot(true);
        }

        private void ClearPlot()
        {
            _lastPlotTime = -1;
            _autoFocusY = false;
            _sTf1.Points.Clear();
            _sTf2.Points.Clear();
            _sTs.Points.Clear();
            _sTc.Points.Clear();
            _xAxis.Minimum = 0;
            _xAxis.Maximum = 600;
            _yAxis.Minimum = 0;
            _yAxis.Maximum = 800;
            _plotModel.InvalidatePlot(true);
            // 曲线清空后恢复空状态提示
            if (_plotEmptyHint != null) _plotEmptyHint.Show();
        }

        private void AppendMessage(string time, string msg, MessageColor color)
        {
            _rtbLog.SelectionColor = color.ToColor();
            _rtbLog.AppendText($"{time}  {msg}\n");
            _rtbLog.ScrollToCaret();
        }

        // ============================================================
        //  按钮事件
        // ============================================================
        private void OnNewTest()
        {
            // 未保存的已完成试验：升温阶段必须先保存；但停止升温回到 Idle 后允许放弃并新建。
            if (_hasUnsavedCompleted && AppHost.Controller.State != TestState.Idle)
            {
                MessageBox.Show("存在未保存的试验记录，请先点击「试验记录」保存，或先「停止升温」。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_hasUnsavedCompleted && AppHost.Controller.State == TestState.Idle)
            {
                var dr = MessageBox.Show(
                    "当前存在未保存的试验记录（已完成但未保存现象/质量）。\r\n继续新建将放弃该试验的结果，且无法恢复。\r\n\r\n是否继续？",
                    "确认放弃未保存的试验",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes) return;

                // 放弃当前未保存试验：删除数据库中的待保存记录及相关文件
                try
                {
                    AppHost.Db.DeleteTest(_currentTest.ProductId, _currentTest.TestId);
                    DeleteRecordFiles(_currentTest.ProductId, _currentTest.TestId);
                    AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                        $"已放弃未保存的试验：{_currentTest.ProductId}/{_currentTest.TestId}", MessageColor.Warning);
                }
                catch (Exception ex)
                {
                    AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                        "放弃未保存试验时出错：" + ex.Message, MessageColor.Error);
                }
                _currentTest = null;
                _testRecordSaved = true;
            }

            using var f = new NewTestForm();
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                _currentTest = f.CreatedTest;
                _testRecordSaved = false;
                AppHost.LiveSamples = new System.Collections.Concurrent.ConcurrentQueue<TempSample>();
                _lastRecordSec = -1;
                // 新试验 = 换新样品：样品温重置回冷态，炉温保持；曲线清空重画
                AppHost.Controller.Sensors.ResetForNewSample(_currentTest.AmbTemp);
                ClearPlot();
                _lblProductId.Text = "样品编号：" + _currentTest.ProductId;
                AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                    $"新建试验：{_currentTest.ProductId}/{_currentTest.TestId}", MessageColor.Normal);
            }
            UpdateButtonStates();
            UpdateStatusBar();
        }

        private void OnStartHeat()
        {
            if (!CanStartHeat()) return;
            ClearPlot();
            AppHost.Controller.StartHeating();
            // 开始升温后自动放大视角到曲线，无需用户手动缩放
            EnableAutoFocusY();
            UpdateButtonStates();
        }

        private void OnStopHeat()
        {
            AppHost.Controller.StopHeating();
            UpdateButtonStates();
        }

        private void OnStartRec()
        {
            if (!CanStartRec()) return;
            AppHost.Controller.StartRecording();
            UpdateButtonStates();
        }

        private void OnStopRec()
        {
            AppHost.Controller.StopRecording();
            UpdateButtonStates();
        }

        private void OnTestRecord()
        {
            if (_currentTest == null)
            {
                MessageBox.Show("当前没有进行中的试验。", "提示");
                return;
            }
            if (_testRecordSaved)
            {
                MessageBox.Show("当前试验记录已保存。", "提示");
                return;
            }
            using var f = new TestRecordForm(_currentTest, GetOrderedLiveSamples());
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                _testRecordSaved = true;
                _currentTest = f.SavedTest;
                // 控制器回到 Preparing 等待下次试验
                AppHost.Controller.MarkSavedAndPrepare();
                AppendMessage(DateTime.Now.ToString("HH:mm:ss"),
                    "试验记录已保存，可继续新建试验。", MessageColor.Normal);
            }
            UpdateButtonStates();
            UpdateStatusBar();
        }

        // ============================================================
        //  按钮 Enable 矩阵
        // ============================================================
        private bool _hasActiveTest => _currentTest != null && !_testRecordSaved;
        private bool _hasUnsavedCompleted =>
            _currentTest != null && !_testRecordSaved && _currentTest.IsCompletedNotSaved;

        private bool CanNewTest()
        {
            var st = AppHost.Controller.State;
            // Recording → 禁止
            if (st == TestState.Recording) return false;
            // 未保存的已完成试验：升温/就绪/完成阶段必须先保存；停止升温回到 Idle 后允许（OnNewTest 内确认放弃）
            if (_hasUnsavedCompleted && st != TestState.Idle) return false;
            // 有进行中（未完成）试验时禁止覆盖
            if (_hasActiveTest && !_hasUnsavedCompleted) return false;
            return true;
        }

        private bool CanStartHeat()
        {
            var st = AppHost.Controller.State;
            // Idle 且有当前试验、未完成保存才可升温
            return st == TestState.Idle && _currentTest != null && !_testRecordSaved && !_hasUnsavedCompleted;
        }

        private bool CanStartRec()
        {
            var st = AppHost.Controller.State;
            return st == TestState.Ready && _currentTest != null && !_testRecordSaved && !_hasUnsavedCompleted;
        }

        private void UpdateButtonStates()
        {
            var st = AppHost.Controller.State;
            _btnNewTest.Enabled = CanNewTest();
            _btnStartHeat.Enabled = CanStartHeat();
            _btnStopHeat.Enabled = st == TestState.Preparing || st == TestState.Ready || st == TestState.Complete;
            _btnStartRec.Enabled = CanStartRec();
            _btnStopRec.Enabled = st == TestState.Recording;
            _btnTestRecord.Enabled = _currentTest != null && !_testRecordSaved
                && (st == TestState.Complete || _currentTest.TotalTestTime > 0);
            _btnSettings.Enabled = st != TestState.Recording;
        }

        private void UpdateStatusBar()
        {
            if (_currentTest != null)
                _lblProductId.Text = "样品编号：" + _currentTest.ProductId;
            else
                _lblProductId.Text = "样品编号：-";
        }

        private List<TempSample> GetOrderedLiveSamples()
            => AppHost.LiveSamples
                .OrderBy(s => s.Time)
                .GroupBy(s => s.Time)
                .Select(g => g.Last())
                .ToList();

        private void LoadSamplesForCurrentTest()
        {
            AppHost.LiveSamples = new System.Collections.Concurrent.ConcurrentQueue<TempSample>();
            if (_currentTest == null) return;

            var csvPath = AppHost.Exporter.GetCsvPath(_currentTest.ProductId, _currentTest.TestId);
            foreach (var sample in ExportService.ReadCsv(csvPath).OrderBy(s => s.Time))
                AppHost.LiveSamples.Enqueue(sample);

            var last = AppHost.LiveSamples.LastOrDefault();
            _lastRecordSec = last?.Time ?? -1;
        }

        /// <summary>删除某次试验相关的本地文件（数据目录、曲线图、报告）。委托给共享的 RecordFileCleanup。</summary>
        private void DeleteRecordFiles(string productId, string testId)
            => Iso11820Simulator.Services.RecordFileCleanup.DeleteAll(productId, testId);
    }
}
