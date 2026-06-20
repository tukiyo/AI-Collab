using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowTopmostKeeper
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private ComboBox _processComboBox;
        private Button _refreshButton;
        private Button _pauseButton;
        private Label _statusLabel;
        private Timer _keepAliveTimer;
        private int _selectedProcessId;
        private bool _isPaused;

        public MainForm()
        {
            if (this == null) throw new ArgumentNullException("this");
            InitializeComponent();
            InitializeCustomComponents();
            ApplyDarkMode();
            RefreshProcessList();
        }

        private void InitializeCustomComponents()
        {
            this.Text = "Window Topmost Keeper";
            this.Size = new Size(570, 200);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            _processComboBox = new ComboBox();
            _processComboBox.Location = new Point(20, 20);
            _processComboBox.Size = new Size(280, 25);
            _processComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _processComboBox.SelectedIndexChanged += new EventHandler(OnProcessSelected);
            this.Controls.Add(_processComboBox);

            _refreshButton = new Button();
            _refreshButton.Location = new Point(310, 18);
            _refreshButton.Size = new Size(110, 28);
            _refreshButton.Text = "リスト更新";
            _refreshButton.FlatStyle = FlatStyle.Flat;
            _refreshButton.Click += new EventHandler(OnRefreshButtonClick);
            this.Controls.Add(_refreshButton);

            _pauseButton = new Button();
            _pauseButton.Location = new Point(430, 18);
            _pauseButton.Size = new Size(110, 28);
            _pauseButton.Text = "一時停止";
            _pauseButton.FlatStyle = FlatStyle.Flat;
            _pauseButton.Click += new EventHandler(OnPauseButtonClick);
            this.Controls.Add(_pauseButton);

            _statusLabel = new Label();
            _statusLabel.Location = new Point(20, 70);
            _statusLabel.Size = new Size(520, 50);
            _statusLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            _statusLabel.Text = "ピン留めするプロセスを選択してください。";
            this.Controls.Add(_statusLabel);

            _keepAliveTimer = new Timer();
            _keepAliveTimer.Interval = 5000;
            _keepAliveTimer.Tick += new EventHandler(OnTimerTick);
            _keepAliveTimer.Start();
        }

        private void ApplyDarkMode()
        {
            Color backColor = Color.FromArgb(32, 32, 32);
            Color foreColor = Color.FromArgb(240, 240, 240);
            Color buttonControlColor = Color.FromArgb(50, 50, 50);

            this.BackColor = backColor;
            this.ForeColor = foreColor;

            _processComboBox.BackColor = buttonControlColor;
            _processComboBox.ForeColor = foreColor;

            _refreshButton.BackColor = buttonControlColor;
            _refreshButton.ForeColor = foreColor;
            _refreshButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

            _pauseButton.BackColor = buttonControlColor;
            _pauseButton.ForeColor = foreColor;
            _pauseButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

            _statusLabel.ForeColor = foreColor;
        }

        private void RefreshProcessList()
        {
            _processComboBox.Items.Clear();
            _selectedProcessId = 0;

            var processes = Process.GetProcesses()
                .Where(p => p != null && (p.MainWindowHandle != IntPtr.Zero || !String.IsNullOrEmpty(p.MainWindowTitle)))
                .OrderBy(p => p.ProcessName)
                .ToList();

            foreach (Process p in processes)
            {
                using (p)
                {
                    string itemText = String.Format("{0} (PID: {1}) - {2}", p.ProcessName, p.Id, p.MainWindowTitle);
                    _processComboBox.Items.Add(new ProcessItem(p.Id, itemText));
                }
            }

            if (_processComboBox.Items.Count > 0)
            {
                _processComboBox.SelectedIndex = 0;
            }
        }

        private void OnProcessSelected(object sender, EventArgs e)
        {
            ProcessItem selected = _processComboBox.SelectedItem as ProcessItem;
            if (selected != null)
            {
                _selectedProcessId = selected.Id;
                if (_isPaused)
                {
                    _statusLabel.Text = String.Format("選択中: PID {0} (現在一時停止中)", _selectedProcessId);
                }
                else
                {
                    _statusLabel.Text = String.Format("選択中: PID {0}\n5秒周期で最前面化を開始します...", _selectedProcessId);
                }
            }
        }

        private void OnRefreshButtonClick(object sender, EventArgs e)
        {
            RefreshProcessList();
        }

        private void OnPauseButtonClick(object sender, EventArgs e)
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                _pauseButton.Text = "再開";
                _pauseButton.BackColor = Color.FromArgb(70, 40, 40);
                _statusLabel.Text = "最前面化処理を一時停止しています。";
            }
            else
            {
                _pauseButton.Text = "一時停止";
                _pauseButton.BackColor = Color.FromArgb(50, 50, 50);
                if (_selectedProcessId != 0)
                {
                    _statusLabel.Text = String.Format("最前面化処理を再開しました。(PID: {0})", _selectedProcessId);
                }
                else
                {
                    _statusLabel.Text = "プロセスを選択してください。";
                }
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (_isPaused || _selectedProcessId == 0) return;

            try
            {
                Process targetProcess = Process.GetProcessById(_selectedProcessId);
                if (targetProcess == null) throw new ArgumentNullException("targetProcess");

                using (targetProcess)
                {
                    IntPtr hWnd = targetProcess.MainWindowHandle;

                    if (hWnd == IntPtr.Zero)
                    {
                        _statusLabel.Text = String.Format("PID: {0} は有効なウィンドウハンドルを返しません。", _selectedProcessId);
                        return;
                    }

                    if (IsWindow(hWnd))
                    {
                        bool success = SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                        if (success)
                        {
                            _statusLabel.Text = String.Format("固定中: {0} (PID: {1})\n5秒周期で最前面を維持しています。", targetProcess.ProcessName, targetProcess.Id);
                        }
                        else
                        {
                            _statusLabel.Text = "最前面の固定処理に失敗しました。";
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                _statusLabel.Text = "対象のプロセスが終了しました。一覧を更新してください。";
                _selectedProcessId = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(String.Format("例外発生: {0}", ex.Message));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_keepAliveTimer != null)
                {
                    _keepAliveTimer.Stop();
                    _keepAliveTimer.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(554, 161);
            this.Name = "MainForm";
            this.ResumeLayout(false);
        }

        private class ProcessItem
        {
            public int Id { get; private set; }
            public string Text { get; private set; }

            public ProcessItem(int id, string text)
            {
                this.Id = id;
                this.Text = text;
            }

            public override string ToString()
            {
                return this.Text;
            }
        }
    }
}