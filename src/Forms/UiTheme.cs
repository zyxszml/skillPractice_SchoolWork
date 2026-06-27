using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Iso11820Simulator.Forms
{
    internal enum UiButtonKind
    {
        Primary,
        Secondary,
        Success,
        Danger
    }

    /// <summary>状态指示灯颜色（与 TestState 对应）。</summary>
    internal enum LampColor
    {
        Gray,    // Idle
        Amber,   // Preparing
        Yellow,  // Ready
        Green,   // Recording
        Blue     // Complete
    }

    internal static class UiTheme
    {
        // ---------- 浅色主题调色板（默认） ----------
        public static Color AppBack = Color.FromArgb(244, 246, 249);
        public static Color Surface = Color.White;
        public static Color SurfaceAlt = Color.FromArgb(250, 251, 253);
        public static Color Border = Color.FromArgb(216, 222, 230);
        public static Color Text = Color.FromArgb(31, 41, 55);
        public static Color MutedText = Color.FromArgb(92, 105, 121);
        public static Color HeaderBack = Color.FromArgb(34, 40, 49);
        public static Color PanelBack = Color.FromArgb(238, 242, 247);
        public static Color Primary = Color.FromArgb(25, 118, 210);
        public static Color Success = Color.FromArgb(30, 132, 73);
        public static Color Danger = Color.FromArgb(190, 65, 56);
        public static Color Warning = Color.FromArgb(180, 116, 30);

        /// <summary>当前是否为深色模式。</summary>
        public static bool DarkMode { get; private set; }

        public static readonly Font BaseFont = new("Microsoft YaHei UI", 9F, FontStyle.Regular);
        public static readonly Font SmallFont = new("Microsoft YaHei UI", 8.25F, FontStyle.Regular);
        public static readonly Font TitleFont = new("Microsoft YaHei UI", 14F, FontStyle.Bold);
        public static readonly Font SectionFont = new("Microsoft YaHei UI", 10F, FontStyle.Bold);
        public static readonly Font MetricFont = new("Consolas", 21F, FontStyle.Bold);
        public static readonly Font MonoFont = new("Consolas", 9.5F, FontStyle.Regular);

        /// <summary>切换主题。会同步更新所有颜色字段；窗体应在切换后自行 Refresh/Invalidate。</summary>
        public static void SetDarkMode(bool enabled)
        {
            DarkMode = enabled;
            if (enabled)
            {
                AppBack = Color.FromArgb(22, 27, 34);
                Surface = Color.FromArgb(33, 40, 50);
                SurfaceAlt = Color.FromArgb(40, 48, 60);
                Border = Color.FromArgb(60, 70, 84);
                Text = Color.FromArgb(230, 236, 244);
                MutedText = Color.FromArgb(150, 162, 178);
                HeaderBack = Color.FromArgb(15, 19, 25);
                PanelBack = Color.FromArgb(28, 34, 42);
                Primary = Color.FromArgb(70, 140, 230);
                Success = Color.FromArgb(56, 152, 96);
                Danger = Color.FromArgb(220, 90, 80);
                Warning = Color.FromArgb(220, 158, 70);
            }
            else
            {
                AppBack = Color.FromArgb(244, 246, 249);
                Surface = Color.White;
                SurfaceAlt = Color.FromArgb(250, 251, 253);
                Border = Color.FromArgb(216, 222, 230);
                Text = Color.FromArgb(31, 41, 55);
                MutedText = Color.FromArgb(92, 105, 121);
                HeaderBack = Color.FromArgb(34, 40, 49);
                PanelBack = Color.FromArgb(238, 242, 247);
                Primary = Color.FromArgb(25, 118, 210);
                Success = Color.FromArgb(30, 132, 73);
                Danger = Color.FromArgb(190, 65, 56);
                Warning = Color.FromArgb(180, 116, 30);
            }
        }

        public static void ApplyForm(Form form)
        {
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.Font = BaseFont;
            form.BackColor = AppBack;
            form.ForeColor = Text;
        }

        public static void StyleTabControl(TabControl tabs)
        {
            tabs.Font = BaseFont;
            tabs.Padding = new Point(18, 6);
        }

        public static Label CreateTitle(string text)
            => new()
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = TitleFont,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                Margin = new Padding(0, 0, 0, 2)
            };

        public static Label CreateSectionLabel(string text)
            => new()
            {
                Text = text,
                AutoSize = false,
                Height = 30,
                Dock = DockStyle.Fill,
                Font = SectionFont,
                ForeColor = Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0),
                Margin = new Padding(0, 14, 0, 6)
            };

        public static Label CreateFieldLabel(string text)
            => new()
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = MutedText,
                Margin = new Padding(0, 3, 12, 3)
            };

        public static Label CreateHeaderValue(string text, Color? foreColor = null)
            => new()
            {
                Text = text,
                AutoSize = true,
                ForeColor = foreColor ?? Color.White,
                BackColor = HeaderBack,
                Margin = new Padding(0, 0, 18, 0),
                Padding = new Padding(0, 2, 0, 0)
            };

        public static Panel CreateSurfacePanel(Padding padding)
            => new()
            {
                BackColor = Surface,
                Padding = padding,
                Margin = new Padding(0),
                BorderStyle = BorderStyle.FixedSingle
            };

        public static FlowLayoutPanel CreateActionBar()
            => new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                BackColor = PanelBack,
                Padding = new Padding(14, 10, 14, 8)
            };

        public static TableLayoutPanel CreateFormTable()
            => new()
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(18, 8, 18, 8)
            };

        public static void StyleButton(Button button, UiButtonKind kind = UiButtonKind.Secondary)
        {
            var (back, fore, border) = kind switch
            {
                UiButtonKind.Primary => (Primary, Color.White, Primary),
                UiButtonKind.Success => (Success, Color.White, Success),
                UiButtonKind.Danger => (Danger, Color.White, Danger),
                _ => (Surface, Text, Border)
            };

            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = back;
            button.ForeColor = fore;
            button.Font = BaseFont;
            // 按内容自动调整宽度并保留最小宽度，避免固定宽度导致最后一个字被裁剪
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.Height = 36;
            button.MinimumSize = new Size(96, 36);
            button.Padding = new Padding(14, 0, 14, 0);
            button.Margin = new Padding(0, 0, 10, 0);
            button.UseCompatibleTextRendering = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = border;
        }

        /// <summary>创建一个竖向分隔条，用于在操作按钮矩阵中将不同功能组视觉分开。</summary>
        public static Control CreateButtonDivider(int height = 28)
        {
            var sep = new Panel
            {
                Size = new Size(1, height),
                BackColor = Border,
                Margin = new Padding(8, 4, 8, 0)
            };
            return sep;
        }

        public static void StyleInput(Control control)
        {
            control.Font = BaseFont;
            control.ForeColor = Text;
            control.BackColor = Color.White;
            control.Margin = new Padding(0, 3, 0, 3);

            if (control is TextBox tb)
            {
                tb.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ComboBox cb)
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.DropDownHeight = 240;
            }
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Surface;
            grid.GridColor = Color.FromArgb(232, 236, 242);
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AllowUserToResizeRows = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
            grid.ColumnHeadersDefaultCellStyle.Font = SectionFont;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 240, 247);
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 235, 255);
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.AlternatingRowsDefaultCellStyle.BackColor = SurfaceAlt;
            grid.RowTemplate.Height = 28;
        }

        public static TableLayoutPanel CreateDialogShell(Control body, FlowLayoutPanel actions)
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = AppBack
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            shell.Controls.Add(body, 0, 0);
            shell.Controls.Add(actions, 0, 1);
            return shell;
        }

        /// <summary>
        /// 简易带圆角与标题的分组容器：模仿 GroupBox 的视觉但使用主题色。
        /// 返回的容器已 Dock=Top；调用方继续向其 Controls 添加内容（首个控件会被置顶）。
        /// </summary>
        public static Panel CreateGroup(string title, int height)
        {
            var box = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = Surface,
                Margin = new Padding(0, 6, 0, 6),
                Padding = new Padding(0)
            };
            var header = new Label
            {
                Text = "  " + title,
                Dock = DockStyle.Top,
                Height = 28,
                Font = SectionFont,
                ForeColor = Text,
                BackColor = SurfaceAlt,
                TextAlign = ContentAlignment.MiddleLeft
            };
            box.Controls.Add(header);
            box.Paint += (s, e) =>
            {
                using var pen = new Pen(Border, 1);
                var r = e.ClipRectangle;
                e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
            };
            return box;
        }

        /// <summary>
        /// 绘制状态指示灯（圆形）。返回可自绘的辅助方法；调用方在 Paint 事件里调用。
        /// </summary>
        public static void DrawStatusLamp(Graphics g, Rectangle area, LampColor color, bool pulse)
        {
            Color core;
            Color glow;
            switch (color)
            {
                case LampColor.Amber:
                    core = pulse ? Color.FromArgb(255, 196, 0) : Color.FromArgb(245, 158, 11);
                    glow = Color.FromArgb(255, 196, 0);
                    break;
                case LampColor.Yellow:
                    core = pulse ? Color.FromArgb(255, 240, 120) : Color.FromArgb(250, 204, 21);
                    glow = Color.FromArgb(250, 204, 21);
                    break;
                case LampColor.Green:
                    core = pulse ? Color.FromArgb(110, 240, 130) : Color.FromArgb(40, 200, 90);
                    glow = Color.FromArgb(40, 200, 90);
                    break;
                case LampColor.Blue:
                    core = Color.FromArgb(80, 160, 240);
                    glow = Color.FromArgb(80, 160, 240);
                    break;
                default: // Gray
                    core = Color.FromArgb(150, 160, 175);
                    glow = Color.FromArgb(150, 160, 175);
                    break;
            }

            var d = Math.Min(area.Width, area.Height);
            var rect = new Rectangle(area.X + (area.Width - d) / 2, area.Y + (area.Height - d) / 2, d, d);
            // 外光晕（脉冲时更亮）
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(rect);
                using var p = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(pulse ? 90 : 40, glow),
                    SurroundColors = new[] { Color.FromArgb(0, glow) }
                };
                g.FillEllipse(p, rect);
            }
            // 实心圆
            using (var b = new SolidBrush(core))
                g.FillEllipse(b, rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
            // 高光
            using (var b2 = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                g.FillEllipse(b2, rect.X + 5, rect.Y + 5, rect.Width / 3, rect.Height / 3);
        }

        /// <summary>
        /// 在目标容器（通常与 DataGridView 同位置）显示一个居中"空状态"覆盖标签。
        /// 返回覆盖控件；调用方在数据非空时 Hide，数据为空时 Show。
        /// </summary>
        public static Label CreateEmptyHint(Control parent, string text)
        {
            var hint = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = MutedText,
                Font = BaseFont,
                BackColor = Color.Transparent
            };
            parent.Controls.Add(hint);
            hint.BringToFront();
            return hint;
        }

        /// <summary>
        /// 创建 Tab 页图标 ImageList：试验控制(火焰)、记录查询(表格)、设备校准(扳手)。
        /// 全部 16x16，用 GDI+ 在运行时绘制，避免引入外部图片资源。
        /// </summary>
        public static ImageList CreateTabImageList()
        {
            var list = new ImageList
            {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit
            };
            list.Images.Add(DrawFlameIcon(16));   // 0 试验控制
            list.Images.Add(DrawTableIcon(16));   // 1 记录查询
            list.Images.Add(DrawWrenchIcon(16));  // 2 设备校准
            return list;
        }

        /// <summary>火焰图标：橙红径向渐变 + 黄色火芯。</summary>
        private static Bitmap DrawFlameIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                // 外焰：橙红路径渐变
                var path = new GraphicsPath();
                path.AddBezier(3, 13, 1, 9, 5, 8, 6, 3);          // 左侧外轮廓
                path.AddBezier(6, 3, 9, 0, 10, 5, 13, 9);         // 顶部尖角 + 右侧上
                path.AddBezier(13, 9, 15, 12, 13, 14, 11, 14);    // 右侧下
                path.AddLine(11, 14, 5, 14);                      // 底部
                path.AddBezier(5, 14, 5, 14, 4, 14, 3, 13);       // 回到起点
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(255, 200, 60);
                    pgb.SurroundColors = new[] { Color.FromArgb(225, 70, 35) };
                    g.FillPath(pgb, path);
                }
                // 内焰火芯：黄色
                var core = new GraphicsPath();
                core.AddBezier(6, 12, 5, 10, 7, 8, 8, 6);
                core.AddBezier(8, 6, 10, 5, 10, 9, 11, 11);
                core.AddBezier(11, 11, 11, 12, 9, 12, 8, 12);
                core.AddLine(8, 12, 6, 12);
                using (var b = new SolidBrush(Color.FromArgb(255, 235, 130)))
                    g.FillPath(b, core);
            }
            return bmp;
        }

        /// <summary>表格图标：外框 + 2 行 1 列分割线。</summary>
        private static Bitmap DrawTableIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var border = new Pen(Primary, 1.6f);
                var line = new Pen(Color.FromArgb(120, 140, 165), 1f);
                var rect = new Rectangle(2, 3, 12, 10);
                g.DrawRectangle(border, rect);
                // 标题行分隔
                g.DrawLine(line, rect.X, rect.Y + 3, rect.Right, rect.Y + 3);
                // 中部行分隔
                g.DrawLine(line, rect.X, rect.Y + 7, rect.Right, rect.Y + 7);
                // 列分隔
                g.DrawLine(line, rect.X + 6, rect.Y, rect.X + 6, rect.Bottom);
            }
            return bmp;
        }

        /// <summary>扳手图标：圆头 + 矩形手柄，斜放。</summary>
        private static Bitmap DrawWrenchIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var metal = new Pen(Color.FromArgb(110, 120, 135), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                // 手柄：左下到右上的粗斜线
                g.DrawLine(metal, 3, 13, 10, 6);
                // 圆头（开口扳手）：右上端画空心圆
                using (var b = new SolidBrush(Color.FromArgb(110, 120, 135)))
                    g.FillEllipse(b, 9, 3, 5, 5);
                g.FillEllipse(Brushes.Transparent, 10, 4, 3, 3);
            }
            return bmp;
        }
    }
}
