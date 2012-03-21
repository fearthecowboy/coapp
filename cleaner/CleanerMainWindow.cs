using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CoApp.Cleaner {
    using System.Threading.Tasks;

    public partial class CleanerMainWindow : Form {
        internal bool started;
        public void Invoke(Action action) {
            BeginInvoke(action);
        }

        public CleanerMainWindow() {
            Size = new Size {
                Height = 600,
                Width = 340
            };
            Application.EnableVisualStyles();
            InitializeComponent();

            CenterToScreen();
            if (WindowState == FormWindowState.Maximized) {
                WindowState = FormWindowState.Normal;
            }

            Location = new Point(Location.X, Location.Y + 30);

            checkBox1.Checked = CleanerMain.AllPackages;

            CleanerMain.PropertyChanged += () => {
                statusLabel.Visible = true;
                progress.Visible = true;

                checkBox1.Checked = CleanerMain.AllPackages;
                statusLabel.Text = CleanerMain.StatusText;
                messageLabel.Text = CleanerMain.MessageText;
                progress.Value = CleanerMain.OverallProgress;
            };
        }

        private void CleanerMainWindow_Load(object sender, EventArgs e) {
            if (WindowState == FormWindowState.Maximized) {
                WindowState = FormWindowState.Normal;
                Location = new Point(Location.X, Location.Y + 60);
            }
            Opacity = 1;
        }

        private void cancelButton_Click(object sender, EventArgs e) {
            if( !started ) {
                Close();
            }
        }

        private void okButton_Click(object sender, EventArgs e) {
            if (!started) {
                started = true;
                Task.Factory.StartNew(CleanerMain.Start);
                Height = 250;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
            CleanerMain.AllPackages = checkBox1.Checked;
        }

        private void CleanerMainWindow_FormClosing(object sender, FormClosingEventArgs e) {
            if( started ) {
                e.Cancel = true;
            }
        }
    }
}
