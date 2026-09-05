using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace rans0m
{
    public class DownloadBar : Control
    {
        private int _segmentsCount = 10;
        private int _filledSegments = 0;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SegmentsCount
        {
            get => _segmentsCount;
            set
            {
                _segmentsCount = Math.Max(1, value);
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int FilledSegments
        {
            get => _filledSegments;
            set
            {
                int clamped = Math.Clamp(value, 0, _segmentsCount);
                if (_filledSegments != clamped)
                {
                    _filledSegments = clamped;
                    Invalidate();
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _filledSegments;
            set => FilledSegments = value;
        }

        public DownloadBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            Size = new Size(476, 28);
            BackColor = Color.Black;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            int w = ClientSize.Width;
            int h = ClientSize.Height;

            // Pure black interior
            g.Clear(Color.Black);

            int innerX = 2;
            int innerY = 2;
            int innerW = w - 4;
            int innerH = h - 4;

            if (_segmentsCount > 0 && innerW > 0 && innerH > 0)
            {
                float segW = (float)innerW / _segmentsCount;

                for (int i = 0; i < _segmentsCount; i++)
                {
                    float x = innerX + i * segW;
                    float nextX = innerX + (i + 1) * segW;
                    float segWidth = nextX - x;

                    if (i < _filledSegments)
                    {
                        RectangleF rect = new RectangleF(x, innerY, segWidth, innerH);
                        using (var lgb = new LinearGradientBrush(rect, Color.White, Color.White, LinearGradientMode.Vertical))
                        {
                            var blend = new ColorBlend(3);
                            blend.Colors = new Color[]
                            {
                                Color.FromArgb(228, 35, 35),
                                Color.FromArgb(165, 20, 20),
                                Color.FromArgb(220, 30, 30)
                            };
                            blend.Positions = new float[] { 0.0f, 0.5f, 1.0f };
                            lgb.InterpolationColors = blend;
                            g.FillRectangle(lgb, rect);
                        }
                    }

                    // Vertical divider between segments
                    if (i > 0)
                    {
                        using (var pen = new Pen(Color.FromArgb(15, 0, 0), 2.0f))
                        {
                            g.DrawLine(pen, x, innerY, x, innerY + innerH);
                        }
                    }
                }
            }

            // Outer 2px bright red border
            using (var borderPen = new Pen(Color.FromArgb(235, 22, 22), 2.0f))
            {
                g.DrawRectangle(borderPen, 1, 1, w - 2, h - 2);
            }
        }
    }
}
