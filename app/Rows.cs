using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SotfModLoader
{
    public interface IArrangeable
    {
        void Arrange(int width);
    }

    public class HeaderPanel : Panel, IArrangeable
    {
        private readonly Label _title;
        private readonly Control _action;
        private readonly float _scale;
        private readonly Color _line;

        public HeaderPanel(string title, Control action, Font font, Color line, float scale)
        {
            _scale = scale;
            _line = line;
            _action = action;
            DoubleBuffered = true;
            Margin = Padding.Empty;
            _title = new Label { Text = title, Font = font, AutoSize = true, BackColor = Color.Transparent };
            Controls.Add(_title);
            if (action != null)
                Controls.Add(action);
        }

        private int S(float v)
        {
            return (int)Math.Round(v * _scale);
        }

        public void Arrange(int width)
        {
            Width = width;
            _title.Location = new Point(0, S(22));
            var bottom = _title.Bottom;
            if (_action != null)
            {
                _action.Location = new Point(width - _action.Width, S(18));
                bottom = Math.Max(bottom, _action.Bottom);
            }
            Height = bottom + S(10);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(_line, Math.Max(1, S(2))))
                e.Graphics.DrawLine(pen, 0, Height - S(1), Width, Height - S(1));
        }
    }

    public class RowPanel : Panel, IArrangeable
    {
        private readonly LinkLabel _title;
        private readonly Label _desc;
        private readonly Label _meta;
        private readonly FlowLayoutPanel _actions;
        private readonly float _scale;
        private readonly Color _line;

        public RowPanel(string title, string url, string description, string meta, Color metaColor,
            IEnumerable<Control> buttons, Font titleFont, Font bodyFont, Font metaFont,
            Color textColor, Color mutedColor, Color line, float scale)
        {
            _scale = scale;
            _line = line;
            DoubleBuffered = true;
            Margin = Padding.Empty;

            _title = new LinkLabel
            {
                Text = title,
                Font = titleFont,
                AutoSize = true,
                LinkColor = textColor,
                ActiveLinkColor = textColor,
                VisitedLinkColor = textColor,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.Transparent
            };
            if (!string.IsNullOrEmpty(url))
                _title.LinkClicked += (s, e) => MainForm.OpenUrl(url);

            _desc = new Label { Text = description ?? string.Empty, Font = bodyFont, AutoSize = true, ForeColor = textColor, BackColor = Color.Transparent };
            _meta = new Label { Text = meta ?? string.Empty, Font = metaFont, AutoSize = true, ForeColor = metaColor, BackColor = Color.Transparent };

            _actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            foreach (var button in buttons)
                _actions.Controls.Add(button);

            Controls.Add(_title);
            Controls.Add(_desc);
            Controls.Add(_meta);
            Controls.Add(_actions);
        }

        private int S(float v)
        {
            return (int)Math.Round(v * _scale);
        }

        public void Arrange(int width)
        {
            Width = width;
            var actionsWidth = _actions.Controls.Count > 0 ? _actions.PreferredSize.Width : 0;
            _actions.Location = new Point(width - actionsWidth, S(16));
            var textWidth = Math.Max(S(160), width - actionsWidth - S(24));

            _title.Location = new Point(0, S(14));
            _desc.MaximumSize = new Size(textWidth, 0);
            _desc.Location = new Point(0, _title.Bottom + S(2));
            _desc.Visible = _desc.Text.Length > 0;
            var y = _desc.Visible ? _desc.Bottom : _title.Bottom;
            _meta.MaximumSize = new Size(textWidth, 0);
            _meta.Location = new Point(0, y + S(6));

            var bottom = Math.Max(_meta.Bottom, _actions.Controls.Count > 0 ? _actions.Bottom : 0);
            Height = bottom + S(14);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(_line))
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
