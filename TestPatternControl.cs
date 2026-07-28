using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinGamma
{
    public sealed class TestPatternControl : Control
    {
        private double _targetGamma = 2.2;

        public double TargetGamma
        {
            get { return _targetGamma; }
            set
            {
                _targetGamma = Math.Max(1.0, Math.Min(3.0, value));
                Invalidate();
            }
        }

        public TestPatternControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.FromArgb(36, 36, 40);
            ForeColor = Color.White;
            MinimumSize = new Size(360, 300);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

            string[] names = Localizer.Language == "en"
                ? new[] { "Gray", "Red", "Green", "Blue" }
                : new[] { "Сірий", "Червоний", "Зелений", "Синій" };
            Color[] channels = new[]
            {
                Color.White, Color.Red, Color.Lime, Color.Blue
            };

            int margin = 16;
            int labelWidth = 72;
            int gap = 12;
            int rowHeight = Math.Max(52,
                (ClientSize.Height - (margin * 2) - (gap * 3)) / 4);
            int patternLeft = margin + labelWidth;
            int patternWidth = Math.Max(20, ClientSize.Width - patternLeft - margin);
            int halfWidth = patternWidth / 2;
            int solidCode = (int)Math.Round(
                Math.Pow(0.5, 1.0 / TargetGamma) * 255.0);

            using (Font labelFont = new Font(Font.FontFamily, 10.0f,
                FontStyle.Bold, GraphicsUnit.Point))
            {
                for (int row = 0; row < 4; row++)
                {
                    int top = margin + row * (rowHeight + gap);
                    Rectangle labelRect = new Rectangle(margin, top,
                        labelWidth - 8, rowHeight);
                    TextRenderer.DrawText(e.Graphics, names[row], labelFont,
                        labelRect, ForeColor, TextFormatFlags.VerticalCenter
                        | TextFormatFlags.Left);

                    Rectangle stripes = new Rectangle(patternLeft, top,
                        halfWidth, rowHeight);
                    Rectangle solid = new Rectangle(patternLeft + halfWidth, top,
                        patternWidth - halfWidth, rowHeight);
                    DrawStripes(e.Graphics, stripes, channels[row]);
                    Color solidColor = ScaleChannel(channels[row], solidCode);
                    using (Brush brush = new SolidBrush(solidColor))
                        e.Graphics.FillRectangle(brush, solid);
                    using (Pen border = new Pen(Color.FromArgb(110, 255, 255, 255)))
                        e.Graphics.DrawRectangle(border, patternLeft, top,
                            patternWidth - 1, rowHeight - 1);
                }
            }
        }

        private static void DrawStripes(Graphics graphics, Rectangle bounds,
            Color channel)
        {
            graphics.FillRectangle(Brushes.Black, bounds);
            using (Pen pen = new Pen(channel))
            {
                for (int x = bounds.Left; x < bounds.Right; x += 2)
                    graphics.DrawLine(pen, x, bounds.Top, x, bounds.Bottom - 1);
            }
        }

        private static Color ScaleChannel(Color channel, int value)
        {
            return Color.FromArgb(
                channel.R == 0 ? 0 : value,
                channel.G == 0 ? 0 : value,
                channel.B == 0 ? 0 : value);
        }
    }

    internal sealed class FullScreenTestForm : Form
    {
        public FullScreenTestForm(DisplayMonitor monitor, double targetGamma)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = monitor.Bounds;
            TopMost = true;
            BackColor = Color.Black;
            KeyPreview = true;

            TestPatternControl pattern = new TestPatternControl();
            pattern.Dock = DockStyle.Fill;
            pattern.TargetGamma = targetGamma;
            Controls.Add(pattern);

            Label hint = new Label();
            hint.AutoSize = true;
            hint.BackColor = Color.FromArgb(180, 0, 0, 0);
            hint.ForeColor = Color.White;
            hint.Padding = new Padding(12, 8, 12, 8);
            hint.Text = "Esc — " + (Localizer.Language == "en" ? "close" : "закрити");
            hint.Location = new Point(12, 12);
            Controls.Add(hint);
            hint.BringToFront();

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                    Close();
            };
            Deactivate += delegate { Close(); };
        }
    }
}
