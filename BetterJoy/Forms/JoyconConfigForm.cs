using BetterJoy.Controller;
using BetterJoy.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.ComponentModel;
using System.Windows.Forms;

namespace BetterJoy.Forms;

public class JoyconConfigForm : Form
{
    private readonly Timer _pollTimer;
    private readonly ComboBox _controllerSelector;
    private readonly ControllerVisualizer _visualizer;
    private readonly PictureBox _leftStickBox;
    private readonly PictureBox _rightStickBox;

    public JoyconConfigForm()
    {
        Text = "Joycon Configuration";
        Size = new Size(800, 520);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;

        _controllerSelector = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        _controllerSelector.SelectedIndexChanged += ControllerSelector_SelectedIndexChanged;

        _visualizer = new ControllerVisualizer { Dock = DockStyle.Left, Width = 420 };

        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var sticksLabel = new Label { Text = "Joysticks (X/Y)", Dock = DockStyle.Top, Height = 20 };

        _leftStickBox = new PictureBox { Dock = DockStyle.Top, Height = 200, BorderStyle = BorderStyle.FixedSingle };
        _rightStickBox = new PictureBox { Dock = DockStyle.Top, Height = 200, BorderStyle = BorderStyle.FixedSingle, Top = 220 };

        rightPanel.Controls.Add(_rightStickBox);
        rightPanel.Controls.Add(_leftStickBox);
        rightPanel.Controls.Add(sticksLabel);

        Controls.Add(rightPanel);
        Controls.Add(_visualizer);
        Controls.Add(_controllerSelector);

        _pollTimer = new Timer { Interval = 50 }; // 20Hz
        _pollTimer.Tick += PollTimer_Tick;

        Load += JoyconConfigForm_Load;
        FormClosed += (s, e) => _pollTimer.Stop();
    }

    private void JoyconConfigForm_Load(object? sender, EventArgs e)
    {
        RefreshControllerList();
        _pollTimer.Start();
    }

    private void RefreshControllerList()
    {
        _controllerSelector.Items.Clear();
        if (Program.Mgr == null)
        {
            return;
        }

        for (var i = 0; i < Program.Mgr.Controllers.Count; ++i)
        {
            var c = Program.Mgr.Controllers[i];
            var name = $"P{c.PadId + 1} - {c.Type} - {c.SerialNumber}";
            _controllerSelector.Items.Add(new ComboItem(name, c));
        }

        if (_controllerSelector.Items.Count > 0)
        {
            _controllerSelector.SelectedIndex = 0;
        }
    }

    private void ControllerSelector_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _visualizer.SelectedController = (_controllerSelector.SelectedItem as ComboItem)?.Controller;
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        var item = _controllerSelector.SelectedItem as ComboItem;
        var controller = item?.Controller;
        if (controller == null)
        {
            return;
        }

        // Update visualizer
        _visualizer.Invalidate();

        // Update left stick box
        DrawStick(_leftStickBox, controller.GetLeftStick());

        // Update right stick box
        DrawStick(_rightStickBox, controller.GetRightStick());
    }

    private void DrawStick(PictureBox box, Stick stick)
    {
        var bmp = new Bitmap(Math.Max(1, box.Width), Math.Max(1, box.Height));
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(box.BackColor);
            // draw axis
            var cx = bmp.Width / 2f;
            var cy = bmp.Height / 2f;
            g.DrawLine(Pens.Gray, cx, 0, cx, bmp.Height);
            g.DrawLine(Pens.Gray, 0, cy, bmp.Width, cy);

            // dot position (stick X/Y are in -1..1)
            var radius = Math.Min(bmp.Width, bmp.Height) * 0.4f;
            var x = cx + stick.X * radius;
            var y = cy - stick.Y * radius; // invert Y for display

            // trail circle
            var dotRadius = 6f;
            g.FillEllipse(Brushes.OrangeRed, x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2);
            g.DrawEllipse(Pens.Black, x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2);
        }

        var old = box.Image;
        box.Image = bmp;
        old?.Dispose();
    }

    private class ComboItem
    {
        public string Name { get; }
        public Joycon Controller { get; }

        public ComboItem(string name, Joycon controller)
        {
            Name = name;
            Controller = controller;
        }

        public override string ToString() => Name;
    }

    private class ControllerVisualizer : Control
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Joycon? SelectedController { get; set; }

        // Normalized button regions expressed as rectangles relative to control size
        private readonly Dictionary<Joycon.Button, RectangleF> _regions = new()
        {
            // layout tuned for a horizontally-oriented controller image drawn left
            { Joycon.Button.Stick, new RectangleF(0.08f, 0.58f, 0.22f, 0.22f) }, // left stick
            { Joycon.Button.Stick2, new RectangleF(0.70f, 0.58f, 0.22f, 0.22f) }, // right stick
            { Joycon.Button.DpadUp, new RectangleF(0.08f, 0.28f, 0.12f, 0.12f) },
            { Joycon.Button.DpadLeft, new RectangleF(0.02f, 0.36f, 0.12f, 0.12f) },
            { Joycon.Button.DpadRight, new RectangleF(0.14f, 0.36f, 0.12f, 0.12f) },
            { Joycon.Button.DpadDown, new RectangleF(0.08f, 0.44f, 0.12f, 0.12f) },
            { Joycon.Button.Plus, new RectangleF(0.86f, 0.18f, 0.10f, 0.10f) },
            { Joycon.Button.Minus, new RectangleF(0.04f, 0.18f, 0.10f, 0.10f) },
            { Joycon.Button.Home, new RectangleF(0.84f, 0.40f, 0.10f, 0.10f) },
            { Joycon.Button.Capture, new RectangleF(0.84f, 0.28f, 0.10f, 0.10f) },
            { Joycon.Button.Shoulder1, new RectangleF(0.08f, 0.14f, 0.18f, 0.08f) },
            { Joycon.Button.Shoulder2, new RectangleF(0.08f, 0.02f, 0.18f, 0.12f) },
            { Joycon.Button.A, new RectangleF(0.82f, 0.36f, 0.12f, 0.12f) },
            { Joycon.Button.B, new RectangleF(0.74f, 0.44f, 0.12f, 0.12f) },
            { Joycon.Button.X, new RectangleF(0.74f, 0.28f, 0.12f, 0.12f) },
            { Joycon.Button.Y, new RectangleF(0.66f, 0.36f, 0.12f, 0.12f) },
            { Joycon.Button.Shoulder21, new RectangleF(0.78f, 0.14f, 0.18f, 0.08f) },
            { Joycon.Button.Shoulder22, new RectangleF(0.78f, 0.02f, 0.18f, 0.12f) },
        };

        public ControllerVisualizer()
        {
            DoubleBuffered = true;
            BackColor = Color.WhiteSmoke;
            Padding = new Padding(8);
            MinimumSize = new Size(300, 380);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            var w = ClientSize.Width;
            var h = ClientSize.Height;

            // Draw simple controller silhouette
            DrawControllerSilhouette(g, w, h);

            if (SelectedController == null)
            {
                return;
            }

            // highlight regions based on button state
            foreach (var kv in _regions)
            {
                var btn = kv.Key;
                var rectNorm = kv.Value;
                var rect = new RectangleF(rectNorm.X * w, rectNorm.Y * h, rectNorm.Width * w, rectNorm.Height * h);

                bool pressed = false;
                try
                {
                    pressed = SelectedController.IsButtonPressed(btn);
                }
                catch
                {
                    pressed = false;
                }

                if (pressed)
                {
                    using var brush = new SolidBrush(Color.FromArgb(180, Color.OrangeRed));
                    g.FillEllipse(brush, rect);
                }
                else
                {
                    g.DrawEllipse(Pens.DimGray, rect);
                }
            }

            // Draw labels for sticks
            var leftStickRect = _regions[Joycon.Button.Stick];
            var rightStickRect = _regions[Joycon.Button.Stick2];
            g.DrawString("L", SystemFonts.DefaultFont, Brushes.Black, leftStickRect.X * w + 4, leftStickRect.Y * h + 4);
            g.DrawString("R", SystemFonts.DefaultFont, Brushes.Black, rightStickRect.X * w + 4, rightStickRect.Y * h + 4);
        }

        private static void DrawControllerSilhouette(Graphics g, int w, int h)
        {
            // simple rounded rectangle silhouette
            var margin = 10;
            var rect = new Rectangle(margin, margin, w - margin * 2, h - margin * 2);
            using var pen = new Pen(Color.Gray, 2);
            g.DrawArc(pen, rect.X, rect.Y, 80, 80, 90, 180);
            g.DrawArc(pen, rect.Right - 80, rect.Y, 80, 80, 270, 180);
            g.DrawRectangle(pen, rect.X + 40, rect.Y + 20, rect.Width - 80, rect.Height - 40);
        }
    }
}
