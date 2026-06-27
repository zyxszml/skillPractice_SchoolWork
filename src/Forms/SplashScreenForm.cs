using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Iso11820Simulator.Forms
{
    /// <summary>
    /// 启动画面：无边框居中卡片，覆盖 AppHost.Initialize(数据库初始化)阶段。
    /// 完成后调用 FadeOutAndClose() 淡出。
    /// </summary>
    public class SplashScreenForm : Form
    {
        private string _status = "正在初始化…";
        private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 30 };
        /// <summary>进度条当前百分比 0‑1，用于底部加载指示。</summary>
        private float _progress;

        public SplashScreenForm()
        {
            UiTheme.ApplyForm(this);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 360);
            DoubleBuffered = true;
            BackColor = Color.White;

            _fadeTimer.Tick += OnFadeTick;
        }

        /// <summary>更新底部加载状态文字。</summary>
        public void SetStatus(string text)
        {
            _status = text;
            Invalidate();
            Application.DoEvents();
        }

        /// <summary>设置底部进度条百分比（0‑1），可视更新。</summary>
        public void SetProgress(float pct)
        {
            _progress = Math.Clamp(pct, 0f, 1f);
            Invalidate();
            Application.DoEvents();
        }

        /// <summary>淡出动画：Opacity 从 1.0 步进 0.1 到 0，结束后关闭。</summary>
        public void FadeOutAndClose()
        {
            // 略作停留后再淡出，避免一闪而过
            var t = new System.Windows.Forms.Timer { Interval = 250 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                _fadeTimer.Start();
            };
            t.Start();
        }

        private void OnFadeTick(object sender, EventArgs e)
        {
            if (Opacity <= 0.05)
            {
                _fadeTimer.Stop();
                Close();
                return;
            }
            Opacity -= 0.08;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var rc = ClientRectangle;

            // ── 1. 柔和阴影 ──
            for (int i = 4; i >= 1; i--)
            {
                using var shadowPath = MakeRoundedRect(rc.X + i, rc.Y + i + 2, rc.Width - i * 2, rc.Height - i * 2, 16);
                using var shadowBrush = new SolidBrush(Color.FromArgb(18, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }

            // ── 2. 卡片白底 ──
            var card = new Rectangle(rc.X + 2, rc.Y + 2, rc.Width - 4, rc.Height - 4);
            using (var cardPath = MakeRoundedRect(card.X, card.Y, card.Width, card.Height, 16))
            {
                g.SetClip(cardPath);
                using (var b = new SolidBrush(Color.White))
                    g.FillPath(b, cardPath);

                // ── 3. 顶部渐变色带（深蓝→浅蓝，带圆角） ──
                var bandH = 160;
                var band = new Rectangle(card.X, card.Y, card.Width, bandH);
                using (var bandBrush = new LinearGradientBrush(band,
                    Color.FromArgb(15, 50, 100),
                    Color.FromArgb(35, 110, 200),
                    LinearGradientMode.ForwardDiagonal))
                {
                    g.FillRectangle(bandBrush, band);
                }

                // 色带底部柔和渐变过渡到白色
                using (var fadeBrush = new LinearGradientBrush(
                    new Rectangle(card.X, card.Y + bandH - 40, card.Width, 40),
                    Color.Transparent,
                    Color.White,
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(fadeBrush, new Rectangle(card.X, card.Y + bandH - 40, card.Width, 40));
                }

                g.ResetClip();
                // ── 4. 卡片边框 ──
                using var borderPen = new Pen(Color.FromArgb(200, 210, 225), 0.8f);
                g.DrawPath(borderPen, cardPath);
            }

            // ── 5. 火焰 Logo（渐变填充 + 白色描边，居中于色带） ──
            float logoSize = 64f;
            float logoX = card.X + (card.Width - logoSize) / 2;
            float logoY = card.Y + 20;
            DrawFlameLogo(g, logoX, logoY, logoSize);

            // ── 6. 标题 ──
            var titleFont = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
            using (var tb = new SolidBrush(Color.White))
            {
                var t = "ISO 11820";
                var sz = g.MeasureString(t, titleFont);
                g.DrawString(t, titleFont, tb,
                    card.X + (card.Width - sz.Width) / 2, card.Y + 88);
            }
            // 中文副标题行 1
            var subFont1 = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            using (var sb1 = new SolidBrush(Color.FromArgb(230, 240, 255)))
            {
                var s1 = "建材不燃性试验仿真系统";
                var sz = g.MeasureString(s1, subFont1);
                g.DrawString(s1, subFont1, sb1,
                    card.X + (card.Width - sz.Width) / 2, card.Y + 122);
            }

            // ── 7. 英文副标题（色带下方，灰色） ──
            var subFont2 = new Font("Segoe UI", 9F, FontStyle.Regular);
            using (var sb2 = new SolidBrush(Color.FromArgb(160, 175, 195)))
            {
                var s2 = "Building Material Non-Combustibility Test Simulator";
                var sz = g.MeasureString(s2, subFont2);
                g.DrawString(s2, subFont2, sb2,
                    card.X + (card.Width - sz.Width) / 2, card.Y + 178);
            }

            // ── 8. 分隔线 ──
            var sepY = card.Y + 204;
            using (var sepPen = new Pen(Color.FromArgb(225, 230, 240), 0.6f))
                g.DrawLine(sepPen, card.X + 40, sepY, card.Right - 40, sepY);

            // ── 9. 状态文字 ──
            var statusFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            using (var stb = new SolidBrush(UiTheme.MutedText))
            {
                var sz = g.MeasureString(_status, statusFont);
                g.DrawString(_status, statusFont, stb,
                    card.X + (card.Width - sz.Width) / 2, card.Y + 218);
            }

            // ── 10. 进度条 ──
            var barX = card.X + 60f;
            var barW = card.Width - 120f;
            var barY = card.Y + 244f;
            var barH = 4f;
            var barR = barH / 2;
            // 轨道（圆角矩形路径 + 实色填充）
            using (var trackPath = MakeRoundedRect(barX, barY, barW, barH, barR))
            using (var trackBrush = new SolidBrush(Color.FromArgb(230, 235, 242)))
                g.FillPath(trackBrush, trackPath);
            // 已填充（渐变，先裁剪再填充矩形）
            if (_progress > 0.01f)
            {
                var fillW = Math.Max(barH, barW * _progress);
                using (var fillPath = MakeRoundedRect(barX, barY, fillW, barH, barR))
                using (var fillBrush = new LinearGradientBrush(
                    new RectangleF(barX, barY, fillW, barH),
                    Color.FromArgb(35, 110, 200),
                    Color.FromArgb(80, 170, 255),
                    LinearGradientMode.Horizontal))
                {
                    g.SetClip(fillPath);
                    g.FillRectangle(fillBrush, barX, barY, fillW, barH);
                    g.ResetClip();
                }
            }

            // ── 11. 底部版本号 ──
            var verFont = new Font("Segoe UI", 7.5F, FontStyle.Regular);
            using (var vb = new SolidBrush(Color.FromArgb(185, 195, 210)))
            {
                var v = "v1.0.0";
                var sz = g.MeasureString(v, verFont);
                g.DrawString(v, verFont, vb,
                    card.Right - 42 - sz.Width / 2, card.Bottom - 22);
            }
        }

        /// <summary>
        /// 绘制一个精致的火焰 Logo：径向渐变填充（外焰橙红→内焰金黄），
        /// 白色半透明描边，附带微妙的外发光。
        /// </summary>
        private void DrawFlameLogo(Graphics g, float x, float y, float size)
        {
            var cx = x + size / 2;
            var cy = y + size / 2;
            var s = size;

            // —— 外发光（柔和光晕） ——
            using (var glowPath = new GraphicsPath())
            {
                var gr = s * 0.55f;
                glowPath.AddEllipse(cx - gr, cy - gr * 0.3f, gr * 2, gr * 1.6f);
                using (var pgb = new PathGradientBrush(glowPath)
                {
                    CenterColor = Color.FromArgb(45, 255, 160, 40),
                    SurroundColors = new[] { Color.FromArgb(0, 255, 120, 20) }
                })
                {
                    g.FillEllipse(pgb, cx - gr, cy - gr * 0.3f, gr * 2, gr * 1.6f);
                }
            }

            // —— 外焰路径（橙红渐变填充） ——
            var outerFlame = new GraphicsPath();
            outerFlame.StartFigure();
            // 左下角开始，沿左侧上升到尖端，再沿右侧下来到右下角，闭合底部
            outerFlame.AddBezier(cx - s * 0.18f, cy + s * 0.38f,       // 底部左
                                 cx - s * 0.34f, cy + s * 0.08f,       // 左侧中
                                 cx - s * 0.16f, cy - s * 0.28f,       // 左上弯
                                 cx - s * 0.02f, cy - s * 0.42f);      // 尖端左
            outerFlame.AddBezier(cx - s * 0.02f, cy - s * 0.42f,       // 尖端左
                                 cx + s * 0.04f, cy - s * 0.44f,       // 尖端
                                 cx + s * 0.06f, cy - s * 0.36f,       // 尖端右
                                 cx + s * 0.16f, cy - s * 0.20f);      // 右上
            outerFlame.AddBezier(cx + s * 0.16f, cy - s * 0.20f,       // 右上
                                 cx + s * 0.30f, cy + s * 0.02f,       // 右侧中
                                 cx + s * 0.34f, cy + s * 0.28f,        // 右下弯
                                 cx + s * 0.22f, cy + s * 0.38f);       // 底部右
            outerFlame.AddBezier(cx + s * 0.22f, cy + s * 0.38f,       // 底部右
                                 cx + s * 0.12f, cy + s * 0.42f,       // 底部右内
                                 cx - s * 0.08f, cy + s * 0.42f,       // 底部左内
                                 cx - s * 0.18f, cy + s * 0.38f);       // 底部左
            outerFlame.CloseFigure();

            // 外焰渐变
            using (var outerBrush = new PathGradientBrush(outerFlame)
            {
                CenterPoint = new PointF(cx + s * 0.01f, cy - s * 0.05f),
                CenterColor = Color.FromArgb(255, 230, 80),
                SurroundColors = new[] { Color.FromArgb(230, 75, 30) }
            })
            {
                g.FillPath(outerBrush, outerFlame);
            }
            // 外焰白色描边
            using (var outlinePen = new Pen(Color.FromArgb(180, 220, 255), 1.2f) { LineJoin = LineJoin.Round })
            {
                g.DrawPath(outlinePen, outerFlame);
            }

            // —— 内焰（金黄色，比外焰小一圈） ——
            var innerFlame = new GraphicsPath();
            innerFlame.StartFigure();
            innerFlame.AddBezier(cx - s * 0.08f, cy + s * 0.30f,
                                 cx - s * 0.16f, cy + s * 0.10f,
                                 cx - s * 0.08f, cy - s * 0.10f,
                                 cx + s * 0.02f, cy - s * 0.22f);
            innerFlame.AddBezier(cx + s * 0.02f, cy - s * 0.22f,
                                 cx + s * 0.08f, cy - s * 0.14f,
                                 cx + s * 0.16f, cy + s * 0.10f,
                                 cx + s * 0.10f, cy + s * 0.30f);
            innerFlame.AddBezier(cx + s * 0.10f, cy + s * 0.30f,
                                 cx + s * 0.04f, cy + s * 0.34f,
                                 cx - s * 0.04f, cy + s * 0.34f,
                                 cx - s * 0.08f, cy + s * 0.30f);
            innerFlame.CloseFigure();

            using (var innerBrush = new PathGradientBrush(innerFlame)
            {
                CenterPoint = new PointF(cx, cy - s * 0.02f),
                CenterColor = Color.FromArgb(255, 255, 220),
                SurroundColors = new[] { Color.FromArgb(255, 200, 60) }
            })
            {
                g.FillPath(innerBrush, innerFlame);
            }
        }

        private static GraphicsPath MakeRoundedRect(float x, float y, float w, float h, float r)
        {
            var d = r * 2;
            var path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _fadeTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
