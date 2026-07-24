using BetterJoy.Controller;
using BetterJoy.Logging;
using BetterJoy.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static BetterJoy.Controller.Joycon;
using static BetterJoy.Forms._3rdPartyControllers;

namespace BetterJoy.Forms;

public class JoyconConfigForm : Form
{
    private readonly string? _initialControllerSerial;
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
        _controllerSelector.Click += (s,e) => { RefreshControllerList(); };
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

        _pollTimer = new Timer { Interval = 1000/60 }; // 60Hz
        _pollTimer.Tick += PollTimer_Tick;

        Load += JoyconConfigForm_Load;
        FormClosed += (s, e) => _pollTimer.Stop();
    }

    public JoyconConfigForm(Joycon? initialController) : this()
    {
        _initialControllerSerial = initialController?.SerialNumber;
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
            // If an initial controller was provided, select it
            if (!string.IsNullOrEmpty(_initialControllerSerial))
            {
                for (var i = 0; i < _controllerSelector.Items.Count; ++i)
                {
                    if ((_controllerSelector.Items[i] as ComboItem)?.Controller.SerialNumber == _initialControllerSerial)
                    {
                        _controllerSelector.SelectedIndex = i;
                        return;
                    }
                }
            }

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
        DrawStick(_leftStickBox, true);

        // Update right stick box
        DrawStick(_rightStickBox, false);// controller.GetRightStick());
    }

    private void DrawStick(PictureBox box, bool isLeft)//Stick stick)
    {
        var item = _controllerSelector.SelectedItem as ComboItem;
        var controller = item?.Controller;
        if (controller == null)
        {
            return;
        }

        var bmp = new Bitmap(Math.Max(1, box.Width), Math.Max(1, box.Height));
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(box.BackColor);
            // draw axis
            var cx = bmp.Width / 2f;
            var cy = bmp.Height / 2f;
            var radius = Math.Min(bmp.Width, bmp.Height) * 0.4f;
            
            g.DrawLine(Pens.Gray, cx, 0, cx, bmp.Height);
            g.DrawLine(Pens.Gray, 0, cy, bmp.Width, cy);
            g.DrawEllipse(Pens.Black, cx-radius, cy-radius, 2*radius, 2*radius);

            Brush[] s_calibrationBrushes =
            [
                Brushes.OrangeRed, // raw
                Brushes.CornflowerBlue, // user
                Brushes.LimeGreen, // factory
                Brushes.Gold // software
            ]; var i = 0;

            // draw an empty bracket (simple outlined square) for each CalibrationSource value
            foreach (CalibrationSource cs in Enum.GetValues(typeof(CalibrationSource)))
            {
                //if (cs == CalibrationSource.Software) continue;
                Stick stick = isLeft ?
                    controller.GetLeftStick(cs) : controller.GetRightStick(cs);

                // dot position (stick X/Y are in -1..1)
                var x = cx + stick.X * radius;
                var y = cy - stick.Y * radius; // invert Y for display

                // trail circle
                var dotRadius = 2f;
                g.FillEllipse(s_calibrationBrushes[i++], x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2);
                g.DrawEllipse(Pens.Black, x - dotRadius, y - dotRadius, dotRadius * 2, dotRadius * 2);
            }
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

        // Images for controller types
        private readonly Bitmap _imgLeft = Resources.jc_left;
        private readonly Bitmap _imgRight = Resources.jc_right;
        private readonly Bitmap _imgPro = Resources.pro;
        private readonly Bitmap _imgSNES = Resources.snes;
        private readonly Bitmap _imgNES = Resources.nes;
        private readonly Bitmap _imgN64 = Resources.n64;
        private readonly Bitmap _imgFamicomI = Resources.famicom_i;
        private readonly Bitmap _imgFamicomII = Resources.famicom_ii;

        // Region definitions per controller type (normalized to image dimensions)
        // These are tuned to the resource images. Rectangles are X,Y,Width,Height expressed 0..1
        private static Dictionary<Joycon.Button, RectangleF> GetRegionsForType(Joycon.ControllerType type)
        {
            return Joycon.ControllerType.Pro /*type*/ switch
            {
                Joycon.ControllerType.JoyconLeft => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.Stick, new RectangleF(0.12f, 0.54f, 0.22f, 0.22f) },
                    { Joycon.Button.DpadUp, new RectangleF(0.12f, 0.26f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadLeft, new RectangleF(0.06f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadRight, new RectangleF(0.18f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadDown, new RectangleF(0.12f, 0.42f, 0.12f, 0.12f) },
                    { Joycon.Button.Minus, new RectangleF(0.06f, 0.14f, 0.10f, 0.10f) },
                    { Joycon.Button.Shoulder1, new RectangleF(0.04f, 0.02f, 0.26f, 0.10f) },
                    { Joycon.Button.Shoulder2, new RectangleF(0.04f, 0.0f, 0.26f, 0.06f) },
                    { Joycon.Button.Home, new RectangleF(0.72f, 0.38f, 0.12f, 0.12f) } // approximate for left's center buttons
                },
                Joycon.ControllerType.JoyconRight => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.Stick, new RectangleF(0.62f, 0.54f, 0.22f, 0.22f) },
                    { Joycon.Button.A, new RectangleF(0.84f, 0.36f, 0.12f, 0.12f) },
                    { Joycon.Button.B, new RectangleF(0.76f, 0.44f, 0.12f, 0.12f) },
                    { Joycon.Button.X, new RectangleF(0.76f, 0.28f, 0.12f, 0.12f) },
                    { Joycon.Button.Y, new RectangleF(0.68f, 0.36f, 0.12f, 0.12f) },
                    { Joycon.Button.Plus, new RectangleF(0.88f, 0.16f, 0.10f, 0.10f) },
                    { Joycon.Button.Capture, new RectangleF(0.86f, 0.26f, 0.10f, 0.10f) },
                    { Joycon.Button.Shoulder21, new RectangleF(0.72f, 0.02f, 0.26f, 0.10f) },
                    { Joycon.Button.Shoulder22, new RectangleF(0.72f, 0.0f, 0.26f, 0.06f) }
                },
                Joycon.ControllerType.Pro => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.Stick, new RectangleF(0.18f, 0.58f, 0.18f, 0.18f) },
                    { Joycon.Button.Stick2, new RectangleF(0.64f, 0.58f, 0.18f, 0.18f) },
                    { Joycon.Button.DpadUp, new RectangleF(0.12f, 0.28f, 0.10f, 0.10f) },
                    { Joycon.Button.DpadLeft, new RectangleF(0.06f, 0.36f, 0.10f, 0.10f) },
                    { Joycon.Button.DpadRight, new RectangleF(0.18f, 0.36f, 0.10f, 0.10f) },
                    { Joycon.Button.DpadDown, new RectangleF(0.12f, 0.44f, 0.10f, 0.10f) },
                    { Joycon.Button.Plus, new RectangleF(0.60f, 0.28f, 0.06f, 0.06f) },
                    { Joycon.Button.Minus, new RectangleF(0.34f, 0.28f, 0.06f, 0.06f) },
                    { Joycon.Button.Home, new RectangleF(0.56f, 0.36f, 0.06f, 0.06f) },
                    { Joycon.Button.Capture, new RectangleF(0.38f, 0.36f, 0.06f, 0.06f) },
                    { Joycon.Button.A, new RectangleF(0.82f, 0.36f, 0.12f, 0.12f) },
                    { Joycon.Button.B, new RectangleF(0.74f, 0.44f, 0.12f, 0.12f) },
                    { Joycon.Button.X, new RectangleF(0.74f, 0.28f, 0.12f, 0.12f) },
                    { Joycon.Button.Y, new RectangleF(0.66f, 0.36f, 0.12f, 0.12f) },
                    { Joycon.Button.Shoulder2, new RectangleF(0.06f, 0.02f, 0.40f, 0.10f) },
                    { Joycon.Button.Shoulder22, new RectangleF(0.54f, 0.02f, 0.40f, 0.10f) },
                    { Joycon.Button.Shoulder1, new RectangleF(0.06f, 0.12f, 0.18f, 0.08f) },
                    { Joycon.Button.Shoulder21, new RectangleF(0.78f, 0.12f, 0.18f, 0.08f) }
                },
                Joycon.ControllerType.SNES => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.DpadUp, new RectangleF(0.12f, 0.22f, 0.10f, 0.10f) },
                    { Joycon.Button.DpadLeft, new RectangleF(0.06f, 0.30f, 0.10f, 0.10f) },
                    { Joycon.Button.DpadRight, new RectangleF(0.18f, 0.30f, 0.10f, 0.10f) },
                    { Joycon.Button.DpadDown, new RectangleF(0.12f, 0.38f, 0.10f, 0.10f) },
                    { Joycon.Button.X, new RectangleF(0.72f, 0.24f, 0.12f, 0.12f) },
                    { Joycon.Button.Y, new RectangleF(0.64f, 0.32f, 0.12f, 0.12f) },
                    { Joycon.Button.A, new RectangleF(0.80f, 0.32f, 0.12f, 0.12f) },
                    { Joycon.Button.B, new RectangleF(0.72f, 0.40f, 0.12f, 0.12f) }
                },
                Joycon.ControllerType.NES => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.DpadLeft, new RectangleF(0.06f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadRight, new RectangleF(0.18f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadUp, new RectangleF(0.12f, 0.26f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadDown, new RectangleF(0.12f, 0.44f, 0.12f, 0.12f) },
                    { Joycon.Button.A, new RectangleF(0.74f, 0.36f, 0.14f, 0.14f) },
                    { Joycon.Button.B, new RectangleF(0.86f, 0.36f, 0.14f, 0.14f) }
                },
                Joycon.ControllerType.FamicomI => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.DpadLeft, new RectangleF(0.06f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadRight, new RectangleF(0.18f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.A, new RectangleF(0.74f, 0.36f, 0.14f, 0.14f) },
                    { Joycon.Button.B, new RectangleF(0.86f, 0.36f, 0.14f, 0.14f) }
                },
                Joycon.ControllerType.FamicomII => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.DpadLeft, new RectangleF(0.06f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadRight, new RectangleF(0.18f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.A, new RectangleF(0.74f, 0.36f, 0.14f, 0.14f) },
                    { Joycon.Button.B, new RectangleF(0.86f, 0.36f, 0.14f, 0.14f) }
                },
                Joycon.ControllerType.N64 => new Dictionary<Joycon.Button, RectangleF>
                {
                    { Joycon.Button.DpadUp, new RectangleF(0.10f, 0.26f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadLeft, new RectangleF(0.04f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadRight, new RectangleF(0.16f, 0.34f, 0.12f, 0.12f) },
                    { Joycon.Button.DpadDown, new RectangleF(0.10f, 0.42f, 0.12f, 0.12f) },
                    { Joycon.Button.A, new RectangleF(0.72f, 0.36f, 0.12f, 0.12f) },
                    { Joycon.Button.B, new RectangleF(0.64f, 0.44f, 0.12f, 0.12f) },
                    { Joycon.Button.X, new RectangleF(0.64f, 0.28f, 0.12f, 0.12f) },
                    { Joycon.Button.Y, new RectangleF(0.56f, 0.36f, 0.12f, 0.12f) }
                },
                _ => new Dictionary<Joycon.Button, RectangleF>()
            };
        }

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

            // Draw controller image (if selected)
            Bitmap? img = null;
            if (SelectedController != null)
            {
                img = SelectedController.Type switch
                {
                    Joycon.ControllerType.JoyconLeft => _imgLeft,
                    Joycon.ControllerType.JoyconRight => _imgRight,
                    Joycon.ControllerType.Pro => _imgPro,
                    _ => _imgLeft
                };
            }

            Rectangle imgDest = new Rectangle(8, 8, w - 16, h - 16);
            if (img != null)
            {
                // preserve aspect and center
                var imgRatio = (float)img.Width / img.Height;
                var destRatio = (float)imgDest.Width / imgDest.Height;
                Rectangle drawRect;
                if (imgRatio > destRatio)
                {
                    var drawW = imgDest.Width;
                    var drawH = (int)(drawW / imgRatio);
                    drawRect = new Rectangle(imgDest.X, imgDest.Y + (imgDest.Height - drawH) / 2, drawW, drawH);
                }
                else
                {
                    var drawH = imgDest.Height;
                    var drawW = (int)(drawH * imgRatio);
                    drawRect = new Rectangle(imgDest.X + (imgDest.Width - drawW) / 2, imgDest.Y, drawW, drawH);
                }

                g.DrawImage(img, drawRect);

                if (SelectedController == null)
                {
                    return;
                }

                // compute regions based on selected controller type
                var regions = GetRegionsForType(SelectedController.Type);

                foreach (var kv in regions)
                {
                    var btn = kv.Key;
                    var rectNorm = kv.Value;

                    // map normalized rect on image to actual drawn rect
                    var rx = drawRect.X + rectNorm.X * drawRect.Width;
                    var ry = drawRect.Y + rectNorm.Y * drawRect.Height;
                    var rw = rectNorm.Width * drawRect.Width;
                    var rh = rectNorm.Height * drawRect.Height;
                    var rect = new RectangleF(rx, ry, rw, rh);

                    bool pressed = false;
                    try
                    {
                        pressed = SelectedController.IsButtonPressed(btn);
                    }
                    catch { pressed = false; }

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

                // Draw stick labels if present in regions
                if (regions.TryGetValue(Joycon.Button.Stick, out var ls))
                {
                    g.DrawString("L", SystemFonts.DefaultFont, Brushes.Black, drawRect.X + ls.X * drawRect.Width + 4, drawRect.Y + ls.Y * drawRect.Height + 4);
                }

                if (regions.TryGetValue(Joycon.Button.Stick2, out var rs))
                {
                    g.DrawString("R", SystemFonts.DefaultFont, Brushes.Black, drawRect.X + rs.X * drawRect.Width + 4, drawRect.Y + rs.Y * drawRect.Height + 4);
                }

                return;
            }

            // fallback silhouette when no image available
            DrawControllerSilhouette(g, w, h);
            return;
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
