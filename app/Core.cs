using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace JarvisDesktop
{
    // JARVIS CORE: who she belongs to, Master mode, and the owner's admin lock.
    internal sealed partial class Companion
    {
        // Her owner, shown in her Mainframe and her CORE sheet, and named in her personality.
        internal const string Owner = "VollLustig";

        // Admin stays unlocked for the rest of this run once the password is given, or until Lock is pressed.
        private bool adminUnlocked;

        internal bool MasterMode { get { return preferences != null && preferences.MasterMode; } }

        internal void ToggleMasterMode()
        {
            preferences.MasterMode = !preferences.MasterMode;
            Brain.MasterMode = preferences.MasterMode;
            SavePreferences();
            mainframe.Sync();
        }

        internal bool HasAdminPassword { get { return preferences != null && !string.IsNullOrEmpty(preferences.AdminHash) && !string.IsNullOrEmpty(preferences.AdminSalt); } }

        internal bool AdminUnlocked { get { return adminUnlocked || !HasAdminPassword; } }

        // With a password set and Lock settings on, Settings and Mods ask for it before they open.
        internal bool SettingsLocked { get { return HasAdminPassword && preferences.AdminLocks; } }

        internal void ToggleSettingsLock()
        {
            if (!AdminUnlocked) return;
            preferences.AdminLocks = !preferences.AdminLocks;
            SavePreferences();
        }

        internal bool Unlock(string password)
        {
            if (!HasAdminPassword) { adminUnlocked = true; return true; }
            if (string.IsNullOrEmpty(password)) return false;
            byte[] salt, want;
            try { salt = Convert.FromBase64String(preferences.AdminSalt); want = Convert.FromBase64String(preferences.AdminHash); }
            catch (FormatException) { return false; }
            byte[] got = Hash(password, salt);
            int differ = got.Length ^ want.Length;
            for (int i = 0; i < Math.Min(got.Length, want.Length); i++) differ |= got[i] ^ want[i];
            adminUnlocked = differ == 0;
            return adminUnlocked;
        }

        internal void LockAdmin() { adminUnlocked = false; }

        // Sets or changes the admin password. Changing it needs admin unlocked first.
        internal bool SetAdminPassword(string password)
        {
            if (!AdminUnlocked || string.IsNullOrEmpty(password) || password.Length < 4) return false;
            byte[] salt = new byte[16];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            preferences.AdminSalt = Convert.ToBase64String(salt);
            preferences.AdminHash = Convert.ToBase64String(Hash(password, salt));
            adminUnlocked = true;
            SavePreferences();
            return true;
        }

        // Takes the password away, and the lock with it.
        internal void ClearAdminPassword()
        {
            if (!AdminUnlocked) return;
            preferences.AdminHash = null;
            preferences.AdminSalt = null;
            preferences.AdminLocks = false;
            adminUnlocked = false;
            SavePreferences();
        }

        private static byte[] Hash(string password, byte[] salt)
        {
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(password, salt, 120000)) return derive.GetBytes(32);
        }
    }

    internal sealed partial class Mainframe
    {
        private CoreSheet core;

        // Her JARVIS CORE window, over the Mainframe like SECURITY.
        internal void ShowCore()
        {
            if (core != null && !core.IsDisposed && core.Zoom != zoom) core.Close();
            if (core == null || core.IsDisposed) core = new CoreSheet(this, zoom);
            core.Sync();
            if (!core.Visible) core.Show(this);
            core.Activate();
        }

        // True when Settings and Mods may open: no lock, admin already unlocked, or the right password now.
        private bool AdminLets(string what)
        {
            if (jarvis == null || !jarvis.SettingsLocked || jarvis.AdminUnlocked) return true;
            using (AdminPrompt prompt = new AdminPrompt(this, zoom, what))
                return prompt.ShowDialog(this) == DialogResult.OK;
        }

        // The badge in her title bar: who she belongs to.
        private static string OwnerLine { get { return "OWNER // " + Companion.Owner.ToUpperInvariant(); } }

        private sealed class CoreSheet : Form, LookWindow
        {
            private readonly Mainframe owner;
            internal readonly float Zoom;
            private const int Tall = 560;
            private bool syncing;
            private readonly Toggle master;
            private readonly Toggle locks;
            private readonly Entry password;
            private readonly Entry again;
            private readonly Key act;
            private readonly Key lockNow;
            private readonly Key forget;
            private readonly Label status;
            private readonly Label lockNote;
            private readonly Label passwordNote;

            internal CoreSheet(Mainframe owner, float zoom)
            {
                SuspendLayout();
                this.owner = owner;
                Zoom = zoom;
                Text = "Jarvis Core";
                Icon = owner.Icon;
                ShowInTaskbar = false;
                TopMost = true;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                AutoScaleMode = AutoScaleMode.None;
                Font = Type(9f * zoom);
                BackColor = Ground;
                ForeColor = White;
                DoubleBuffered = true;
                KeyPreview = true;
                ClientSize = new Size(Z(Wide), Z(Tall));
                Font small = Look.Mono(8.5f * zoom, FontStyle.Regular, GraphicsUnit.Point);
                Font head = Type(10f * zoom, true);
                Heading(this, "JARVIS CORE >", new Point(Z(24), Z(15)), Display(15f * zoom), Rose, 0);
                Key shut = new Key();
                shut.Bounds = Box(Wide - 54, 20, 26, 22);
                shut.Glyph = 2;
                shut.AccessibleName = "Close";
                shut.Click += delegate { Close(); };
                Controls.Add(shut);

                // The owner's badge, drawn in OnPaint behind these two lines.
                Caption("OWNED AND RUN BY", 44, 60, small, Faint, Deep);
                Caption(Companion.Owner, 44, 76, Display(17f * zoom), Cyan, Deep);

                Heading(this, "MASTER MODE >", new Point(Z(28), Z(130)), head, White, Wide - 28);
                master = new Toggle();
                master.Text = "Master mode";
                master.Font = Font;
                master.Switch = 96;
                master.Bounds = Box(28, 156, Wide - 56, 22);
                master.AccessibleName = "Master mode. She turns extra playful and cute, and calls you Master";
                master.CheckedChanged += delegate { if (!syncing) owner.jarvis.ToggleMasterMode(); };
                Controls.Add(master);
                Caption("She turns extra playful and cute, cheers you on and calls you Master. Her safety lines still hold.", 28, 182, small, Faint, Ground, Wide - 56);

                Heading(this, "ADMIN >", new Point(Z(28), Z(240)), head, White, Wide - 28);
                status = Caption("", 28, 266, Type(9.5f * zoom, true), Neon.Amber, Ground, Wide - 56);
                password = new Entry();
                password.Bounds = Box(28, 292, Wide - 56, 26);
                password.Box.UseSystemPasswordChar = true;
                password.Box.Font = Font;
                password.AccessibleName = "Admin password";
                password.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Act(); } };
                Controls.Add(password);
                again = new Entry();
                again.Bounds = Box(28, 326, Wide - 56, 26);
                again.Box.UseSystemPasswordChar = true;
                again.Box.Font = Font;
                again.AccessibleName = "Admin password again";
                again.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Act(); } };
                Controls.Add(again);
                act = new Key();
                act.Font = Type(9f * zoom, true);
                act.Accent = Cyan;
                act.Loud = true;
                act.Bounds = Box(28, 362, 150, 26);
                act.Click += delegate { Act(); };
                Controls.Add(act);
                lockNow = new Key();
                lockNow.Text = "Lock admin";
                lockNow.Font = Type(9f * zoom, true);
                lockNow.Bounds = Box(186, 362, Wide - 214, 26);
                lockNow.Click += delegate { owner.jarvis.LockAdmin(); Sync(); };
                Controls.Add(lockNow);
                locks = new Toggle();
                locks.Text = "Lock Settings and Mods";
                locks.Font = Font;
                locks.Switch = 96;
                locks.Bounds = Box(28, 404, Wide - 56, 22);
                locks.AccessibleName = "Lock Settings and Mods. They ask for the admin password before they open";
                locks.CheckedChanged += delegate { if (!syncing) { owner.jarvis.ToggleSettingsLock(); Sync(); } };
                Controls.Add(locks);
                lockNote = Caption("Settings and Mods ask for the admin password before they open.", 28, 430, small, Faint, Ground, Wide - 56);
                forget = new Key();
                forget.Text = "Remove password";
                forget.Font = Type(9f * zoom, true);
                forget.Accent = Hot;
                forget.Bounds = Box(28, 458, 150, 24);
                forget.Click += delegate
                {
                    if (MessageBox.Show(this, "Remove the admin password? Settings and Mods unlock for everyone on this PC.", "Jarvis Core", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    owner.jarvis.ClearAdminPassword();
                    Sync();
                };
                Controls.Add(forget);
                passwordNote = Caption("A lock for this PC, not encryption: anyone who can edit her settings file can reset it.", 28, 494, small, Faint, Ground, Wide - 56);

                Key done = new Key();
                done.Text = "Done";
                done.Font = Type(9f * zoom, true);
                done.Accent = Cyan;
                done.Loud = true;
                done.Bounds = Box(Wide - 128, Tall - 40, 100, 26);
                done.Click += delegate { Close(); };
                Controls.Add(done);
                owner.Fit(this);
                ResumeLayout();
                Sync();
            }

            // Set the password, change it, or unlock with it, depending on where admin is.
            private void Act()
            {
                Companion jarvis = owner.jarvis;
                if (!jarvis.HasAdminPassword || (jarvis.AdminUnlocked && changing))
                {
                    if (password.Box.Text.Length < 4) { Say("Use at least 4 characters."); return; }
                    if (password.Box.Text != again.Box.Text) { Say("Those two passwords don't match."); return; }
                    jarvis.SetAdminPassword(password.Box.Text);
                    password.Box.Text = again.Box.Text = "";
                    changing = false;
                    Sync();
                    Say("Admin password saved. Admin is unlocked.");
                    return;
                }
                if (jarvis.AdminUnlocked) { changing = true; Sync(); password.Box.Focus(); return; }
                if (jarvis.Unlock(password.Box.Text)) { password.Box.Text = ""; Sync(); Say("Welcome back, " + Companion.Owner + "."); }
                else { password.Box.SelectAll(); Say("Wrong password."); }
            }

            private bool changing;

            private void Say(string line) { status.Text = line; }

            internal void Sync()
            {
                syncing = true;
                Companion jarvis = owner.jarvis;
                master.Checked = jarvis.MasterMode;
                bool has = jarvis.HasAdminPassword, open = jarvis.AdminUnlocked;
                bool typing = !has || !open || changing;
                status.Text = !has ? "No admin password yet. Set one to lock her settings." : !open ? "Admin is locked." : changing ? "Type the new password twice." : "Admin is unlocked.";
                password.Visible = typing;
                again.Visible = !has || changing;
                password.Cue = !has || changing ? "New password" : "Password";
                again.Cue = "Same password again";
                act.Text = !has ? "Set password" : !open ? "Unlock" : changing ? "Save password" : "Change password";
                act.Top = Z(again.Visible ? 362 : typing ? 328 : 292);
                lockNow.Top = act.Top;
                lockNow.Visible = has && open && !changing;
                locks.Visible = lockNote.Visible = forget.Visible = has && open && !changing;
                locks.Checked = jarvis.SettingsLocked;
                syncing = false;
            }

            private int Z(int value) { return (int)Math.Round(value * Zoom); }

            private Rectangle Box(int x, int y, int width, int height) { return new Rectangle(Z(x), Z(y), Z(width), Z(height)); }

            private Label Caption(string text, int x, int y, Font font, Color colour, Color back, int wide = 0)
            {
                Label label = back == Ground ? (Label)new Note() : new Label();
                label.Text = text;
                label.Font = font;
                label.ForeColor = colour;
                label.BackColor = back;
                label.UseMnemonic = false;
                label.Location = new Point(Z(x), Z(y));
                if (wide > 0) label.MaximumSize = new Size(Z(wide), 0);
                label.AutoSize = true;
                Controls.Add(label);
                return label;
            }

            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                Rectangle area = Screen.FromControl(owner).WorkingArea;
                int x = owner.Left + (owner.Width - Width) / 2, y = owner.Top + (int)(48 * ScaleOf(this));
                Location = new Point(Math.Max(area.Left, Math.Min(area.Right - Width, x)), Math.Max(area.Top, Math.Min(area.Bottom - Height, y)));
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                Clip(this, ScaleOf(this));
                Invalidate(true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                float s = ScaleOf(this);
                Graphics graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Backdrop(graphics, this, s);
                Edging(graphics, this, s);
                OwnerBadge(graphics, new RectangleF(28 * s, 50 * s, (Wide - 56) * s, 64 * s), s);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); return; }
                base.OnKeyDown(e);
            }

            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if (message.Msg == 0x0084 && message.Result == new IntPtr(1)) message.Result = new IntPtr(2);
            }
        }

        // A clipped panel with a lit edge and a small reactor ring at its right: the owner's badge.
        private static void OwnerBadge(Graphics graphics, RectangleF box, float s)
        {
            using (GraphicsPath plate = Neon.Cut(box, 10 * s, 0, 10 * s, 0))
            {
                using (Brush fill = new SolidBrush(Deep)) graphics.FillPath(fill, plate);
                using (Pen rim = new Pen(DeepEdge, Math.Max(1, s))) graphics.DrawPath(rim, plate);
            }
            using (Pen lit = new Pen(Cyan, 2 * s)) graphics.DrawLine(lit, box.Left + 10 * s, box.Top + 1 * s, box.Left + 70 * s, box.Top + 1 * s);
            Reactor(graphics, new PointF(box.Right - box.Height / 2 - 4 * s, box.Top + box.Height / 2), box.Height * 0.32f, s);
        }

        // Her arc-reactor ring: an outer ring split into segments, an inner ring and a lit core.
        internal static void Reactor(Graphics graphics, PointF centre, float radius, float s)
        {
            SmoothingMode was = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen ring = new Pen(Cyan, Math.Max(1.5f, 2.2f * s)))
                for (int i = 0; i < 10; i++)
                    graphics.DrawArc(ring, centre.X - radius, centre.Y - radius, radius * 2, radius * 2, i * 36 + 4, 26);
            float inner = radius * 0.62f;
            using (Pen ring = new Pen(Color.FromArgb(150, Cyan), Math.Max(1, s))) graphics.DrawEllipse(ring, centre.X - inner, centre.Y - inner, inner * 2, inner * 2);
            float core = radius * 0.3f;
            using (GraphicsPath glow = new GraphicsPath())
            {
                glow.AddEllipse(centre.X - core * 1.8f, centre.Y - core * 1.8f, core * 3.6f, core * 3.6f);
                using (PathGradientBrush light = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(180, Cyan), SurroundColors = new[] { Color.FromArgb(0, Cyan) } })
                    graphics.FillPath(light, glow);
            }
            using (Brush lit = new SolidBrush(White)) graphics.FillEllipse(lit, centre.X - core / 2, centre.Y - core / 2, core, core);
            graphics.SmoothingMode = was;
        }

        // Asks for the admin password before a locked Settings or Mods opens.
        private sealed class AdminPrompt : Form, LookWindow
        {
            private readonly Mainframe owner;
            private readonly float zoom;
            internal float Zoom { get { return zoom; } }
            private readonly Entry password;
            private readonly Label wrong;

            internal AdminPrompt(Mainframe owner, float zoom, string what)
            {
                this.owner = owner;
                this.zoom = zoom;
                Text = "Admin";
                Icon = owner.Icon;
                ShowInTaskbar = false;
                TopMost = true;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.CenterParent;
                AutoScaleMode = AutoScaleMode.None;
                Font = Type(9f * zoom);
                BackColor = Ground;
                ForeColor = White;
                DoubleBuffered = true;
                KeyPreview = true;
                ClientSize = new Size(Z(Wide), Z(196));
                Heading(this, "ADMIN >", new Point(Z(24), Z(15)), Display(15f * zoom), Rose, 0);
                Label line = new Note { Text = what + " is locked by " + Companion.Owner + ". Type the admin password.", Font = Look.Mono(8.5f * zoom, FontStyle.Regular, GraphicsUnit.Point), ForeColor = Faint, BackColor = Ground, Location = new Point(Z(28), Z(54)), MaximumSize = new Size(Z(Wide - 56), 0), AutoSize = true, UseMnemonic = false };
                Controls.Add(line);
                password = new Entry { Bounds = new Rectangle(Z(28), Z(92), Z(Wide - 56), Z(26)) };
                password.Box.UseSystemPasswordChar = true;
                password.Box.Font = Font;
                password.Cue = "Password";
                password.AccessibleName = "Admin password";
                Controls.Add(password);
                wrong = new Note { Text = "", Font = Type(9f * zoom, true), ForeColor = Hot, BackColor = Ground, Location = new Point(Z(28), Z(124)), AutoSize = true, UseMnemonic = false };
                Controls.Add(wrong);
                Key cancel = new Key { Text = "Cancel", Font = Type(9f * zoom, true), Bounds = new Rectangle(Z(Wide - 236), Z(150), Z(100), Z(26)) };
                cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
                Controls.Add(cancel);
                Key unlock = new Key { Text = "Unlock", Font = Type(9f * zoom, true), Accent = Cyan, Loud = true, Bounds = new Rectangle(Z(Wide - 128), Z(150), Z(100), Z(26)) };
                unlock.Click += delegate { Try(); };
                Controls.Add(unlock);
                password.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Try(); } };
                Shown += delegate { password.Box.Focus(); };
            }

            private void Try()
            {
                if (owner.jarvis.Unlock(password.Box.Text)) { DialogResult = DialogResult.OK; return; }
                wrong.Text = "Wrong password.";
                password.Box.SelectAll();
                password.Box.Focus();
            }

            private int Z(int value) { return (int)Math.Round(value * zoom); }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                Clip(this, ScaleOf(this));
                Invalidate(true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                float s = ScaleOf(this);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Backdrop(e.Graphics, this, s);
                Edging(e.Graphics, this, s);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { e.Handled = true; DialogResult = DialogResult.Cancel; return; }
                base.OnKeyDown(e);
            }

            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if (message.Msg == 0x0084 && message.Result == new IntPtr(1)) message.Result = new IntPtr(2);
            }
        }
    }
}
