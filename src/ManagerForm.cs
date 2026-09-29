using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WorkspaceLauncher
{
    public class ManagerForm : Form
    {
        private AppConfig _cfg;
        private bool _suppressCheck;

        // 목록 탭
        private ListView _list;
        private TextBox _search;
        private Label _status;
        private Button _btnUp, _btnDown, _btnTop, _btnBottom;

        // 설정 탭
        private TextBox _txtCode, _txtRoots, _txtCategory, _txtLeftPath;
        private NumericUpDown _numDepth, _numMax, _numFolderDepth;
        private CheckBox _chkAutoScan, _chkGroup, _chkScanFolders, _chkRequireMarker;
        private RadioButton _rbNew, _rbFolder;

        // 설정 탭에서 "입력칸 + 찾아보기 버튼" 한 줄을 창 너비에 맞춰 다시 배치하기 위한 목록.
        // Anchor 만으로는 고DPI 에서 버튼이 창 밖으로 밀려나서 직접 계산한다.
        private readonly List<KeyValuePair<TextBox, Button>> _settingsRows =
            new List<KeyValuePair<TextBox, Button>>();
        private Panel _settingsPanel;
        private Label _noteLabel;

        public ManagerForm()
        {
            _cfg = ConfigStore.Load();

            Text = "VS Code 워크스페이스 런처";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 620);
            MinimumSize = new Size(760, 460);
            Font = new Font("Segoe UI", 9F);
            TryLoadIcon();

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 6);

            TabPage tpList = new TabPage("워크스페이스");
            TabPage tpCfg = new TabPage("설정");
            BuildListTab(tpList);
            BuildSettingsTab(tpCfg);
            tabs.TabPages.Add(tpList);
            tabs.TabPages.Add(tpCfg);
            Controls.Add(tabs);

            Load += ManagerForm_Load;
        }

        private void TryLoadIcon()
        {
            try
            {
                string p = Program.IconPath();
                if (p.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) && File.Exists(p))
                    Icon = new Icon(p);
                else
                    Icon = Icon.ExtractAssociatedIcon(ConfigStore.ExePath);
            }
            catch { }
        }

        // ------------------------------------------------------------------
        //  워크스페이스 탭
        // ------------------------------------------------------------------
        private void BuildListTab(TabPage tp)
        {
            // 1) Fill 컨트롤을 먼저 추가해야 나머지 도킹 후 남은 영역을 차지한다
            _list = new ListView();
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.CheckBoxes = true;
            _list.HideSelection = false;
            _list.AllowDrop = true;
            _list.GridLines = false;
            _list.Columns.Add("표시 이름", 210);
            _list.Columns.Add("종류", 115);
            _list.Columns.Add("그룹", 100);
            _list.Columns.Add("경로", 430);
            _list.ItemChecked += List_ItemChecked;
            _list.DoubleClick += delegate { OpenSelected(); };
            _list.ItemDrag += List_ItemDrag;
            _list.DragEnter += delegate (object s, DragEventArgs e) { e.Effect = DragDropEffects.Move; };
            _list.DragOver += List_DragOver;
            _list.DragDrop += List_DragDrop;
            _list.DragLeave += delegate { _list.InsertionMark.Index = -1; };
            _list.KeyDown += List_KeyDown;
            tp.Controls.Add(_list);

            // 2) 상단 검색
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 40;
            Label lblS = new Label();
            lblS.Text = "검색";
            lblS.AutoSize = true;
            lblS.Location = new Point(4, 12);
            _search = new TextBox();
            _search.Location = new Point(44, 8);
            _search.Width = 300;
            _search.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _search.TextChanged += delegate { RefreshList(null); };
            _search.KeyDown += Search_KeyDown;
            Label hint = new Label();
            hint.AutoSize = true;
            hint.ForeColor = Color.DimGray;
            hint.Location = new Point(356, 12);
            hint.Text = "체크 = 점프 목록에 표시 · 드래그 또는 ▲▼ 로 순서 변경 · 더블클릭으로 열기";
            top.Controls.Add(lblS);
            top.Controls.Add(_search);
            top.Controls.Add(hint);
            tp.Controls.Add(top);

            // 3) 우측 버튼
            Panel right = new Panel();
            right.Dock = DockStyle.Right;
            right.Width = 190;
            int y = 4;
            _btnTop = AddBtn(right, "맨 위로", ref y, delegate { MoveToEdge(true); });
            _btnUp = AddBtn(right, "▲ 위로", ref y, delegate { MoveSelected(-1); });
            _btnDown = AddBtn(right, "▼ 아래로", ref y, delegate { MoveSelected(1); });
            _btnBottom = AddBtn(right, "맨 아래로", ref y, delegate { MoveToEdge(false); });
            y += 12;
            AddBtn(right, "워크스페이스 추가…", ref y, AddWorkspaceFiles);
            AddBtn(right, "폴더 추가…", ref y, AddFolder);
            AddBtn(right, "폴더 다시 스캔", ref y, RescanNow);
            y += 12;
            AddBtn(right, "이름 변경…", ref y, RenameSelected);
            AddBtn(right, "그룹 지정…", ref y, SetGroupSelected);
            AddBtn(right, "목록에서 제거", ref y, RemoveSelected);
            y += 12;
            AddBtn(right, "전체 선택", ref y, delegate { SetAllChecked(true); });
            AddBtn(right, "전체 해제", ref y, delegate { SetAllChecked(false); });
            y += 12;
            AddBtn(right, "선택 항목 열기", ref y, delegate { OpenSelected(); });
            tp.Controls.Add(right);

            // 4) 하단
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 44;

            _status = new Label();
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Padding = new Padding(4, 0, 0, 0);
            bottom.Controls.Add(_status);

            Panel btnHost = new Panel();
            btnHost.Dock = DockStyle.Right;
            btnHost.Width = 300;
            Button bClose = new Button();
            bClose.Text = "닫기";
            bClose.Size = new Size(96, 30);
            bClose.Location = new Point(196, 7);
            bClose.Click += delegate { Close(); };
            Button bApply = new Button();
            bApply.Text = "저장 후 적용";
            bApply.Size = new Size(130, 30);
            bApply.Location = new Point(58, 7);
            bApply.Click += delegate { SaveAndApply(true); };
            btnHost.Controls.Add(bClose);
            btnHost.Controls.Add(bApply);
            bottom.Controls.Add(btnHost);

            tp.Controls.Add(bottom);
        }

        private Button AddBtn(Panel host, string text, ref int y, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.Size = new Size(178, 28);
            b.Location = new Point(6, y);
            b.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            b.Click += onClick;
            host.Controls.Add(b);
            y += 32;
            return b;
        }

        // ------------------------------------------------------------------
        //  설정 탭
        // ------------------------------------------------------------------
        private void BuildSettingsTab(TabPage tp)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.AutoScroll = true;
            tp.Controls.Add(p);

            _settingsPanel = p;

            int y = 14;
            const int labelW = 240;
            const int fieldX = 256;

            AddLabel(p, "VS Code 실행 파일", 12, y + 3, labelW);
            _txtCode = new TextBox();
            _txtCode.Location = new Point(fieldX, y);
            _txtCode.Width = 480;
            string resolved = ConfigStore.ResolveCodeExe(_cfg);
            _txtCode.Text = !string.IsNullOrEmpty(_cfg.CodePath) ? _cfg.CodePath
                          : (resolved == null ? "" : resolved);
            p.Controls.Add(_txtCode);
            Button bCode = new Button();
            bCode.Text = "찾아보기…";
            bCode.Location = new Point(fieldX + 490, y - 1);
            bCode.Size = new Size(110, 25);
            bCode.Click += delegate
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "Code.exe|Code*.exe|실행 파일 (*.exe)|*.exe";
                if (d.ShowDialog(this) == DialogResult.OK) _txtCode.Text = d.FileName;
            };
            p.Controls.Add(bCode);
            _settingsRows.Add(new KeyValuePair<TextBox, Button>(_txtCode, bCode));
            y += 38;

            AddLabel(p, "스캔 폴더 (한 줄에 하나)", 12, y + 3, labelW);
            _txtRoots = new TextBox();
            _txtRoots.Location = new Point(fieldX, y);
            _txtRoots.Width = 480;
            _txtRoots.Height = 74;
            _txtRoots.Multiline = true;
            _txtRoots.ScrollBars = ScrollBars.Vertical;
            _txtRoots.Text = string.Join("\r\n", _cfg.ScanRoots.ToArray());
            p.Controls.Add(_txtRoots);
            Button bRoot = new Button();
            bRoot.Text = "폴더 추가…";
            bRoot.Location = new Point(fieldX + 490, y - 1);
            bRoot.Size = new Size(110, 25);
            bRoot.Click += delegate
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                d.Description = "스캔할 폴더를 선택하세요";
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    if (_txtRoots.Text.Length > 0 && !_txtRoots.Text.EndsWith("\n"))
                        _txtRoots.AppendText("\r\n");
                    _txtRoots.AppendText(d.SelectedPath);
                }
            };
            p.Controls.Add(bRoot);
            _settingsRows.Add(new KeyValuePair<TextBox, Button>(_txtRoots, bRoot));
            y += 86;

            AddLabel(p, "워크스페이스 파일 탐색 깊이", 12, y + 3, labelW);
            _numDepth = new NumericUpDown();
            _numDepth.Location = new Point(fieldX, y);
            _numDepth.Width = 70;
            _numDepth.Minimum = 1;
            _numDepth.Maximum = 12;
            _numDepth.Value = Math.Min(12, Math.Max(1, _cfg.ScanDepth));
            p.Controls.Add(_numDepth);

            _chkAutoScan = new CheckBox();
            _chkAutoScan.Text = "실행할 때마다 자동으로 다시 스캔";
            _chkAutoScan.Location = new Point(fieldX + 90, y + 2);
            _chkAutoScan.AutoSize = true;
            _chkAutoScan.Checked = _cfg.AutoScan;
            p.Controls.Add(_chkAutoScan);
            y += 34;

            _chkScanFolders = new CheckBox();
            _chkScanFolders.Text = "프로젝트 폴더도 등록 (.code-workspace 가 없는 폴더)";
            _chkScanFolders.Location = new Point(fieldX, y);
            _chkScanFolders.AutoSize = true;
            _chkScanFolders.Checked = _cfg.ScanFolders;
            p.Controls.Add(_chkScanFolders);
            y += 28;

            AddLabel(p, "폴더 등록 깊이", 12, y + 3, labelW);
            _numFolderDepth = new NumericUpDown();
            _numFolderDepth.Location = new Point(fieldX, y);
            _numFolderDepth.Width = 70;
            _numFolderDepth.Minimum = 1;
            _numFolderDepth.Maximum = 6;
            _numFolderDepth.Value = Math.Min(6, Math.Max(1, _cfg.FolderScanDepth));
            p.Controls.Add(_numFolderDepth);
            AddLabel(p, "1 = 스캔 폴더 바로 아래 폴더만 등록", fieldX + 80, y + 3, 400, Color.DimGray);
            y += 32;

            _chkRequireMarker = new CheckBox();
            _chkRequireMarker.Text = ".git · .vscode · package.json 같은 프로젝트 표식이 있는 폴더만";
            _chkRequireMarker.Location = new Point(fieldX, y);
            _chkRequireMarker.AutoSize = true;
            _chkRequireMarker.Checked = _cfg.RequireProjectMarker;
            p.Controls.Add(_chkRequireMarker);
            y += 38;

            EventHandler syncFolderOpts = delegate
            {
                _numFolderDepth.Enabled = _chkScanFolders.Checked;
                _chkRequireMarker.Enabled = _chkScanFolders.Checked;
            };
            _chkScanFolders.CheckedChanged += syncFolderOpts;
            syncFolderOpts(null, EventArgs.Empty);

            AddLabel(p, "점프 목록 최대 표시 개수", 12, y + 3, labelW);
            _numMax = new NumericUpDown();
            _numMax.Location = new Point(fieldX, y);
            _numMax.Width = 70;
            _numMax.Minimum = 0;
            _numMax.Maximum = 200;
            _numMax.Value = Math.Min(200, Math.Max(0, _cfg.MaxJumpItems));
            p.Controls.Add(_numMax);
            AddLabel(p, "0 = 윈도우가 허용하는 최대치까지 자동", fieldX + 80, y + 3, 400, Color.DimGray);
            y += 36;

            _chkGroup = new CheckBox();
            _chkGroup.Text = "그룹별로 나눠서 표시";
            _chkGroup.Location = new Point(fieldX, y);
            _chkGroup.AutoSize = true;
            _chkGroup.Checked = _cfg.GroupByCategory;
            p.Controls.Add(_chkGroup);
            y += 30;

            AddLabel(p, "기본 그룹(카테고리) 제목", 12, y + 3, labelW);
            _txtCategory = new TextBox();
            _txtCategory.Location = new Point(fieldX, y);
            _txtCategory.Width = 220;
            _txtCategory.Text = _cfg.CategoryTitle;
            p.Controls.Add(_txtCategory);
            y += 44;

            AddLabel(p, "왼쪽 클릭 동작", 12, y + 3, labelW, Color.Black, true);
            y += 26;
            _rbNew = new RadioButton();
            _rbNew.Text = "빈 새 창 열기 (code -n)";
            _rbNew.Location = new Point(fieldX, y);
            _rbNew.AutoSize = true;
            _rbNew.Checked = !string.Equals(_cfg.LeftClickMode, "folder", StringComparison.OrdinalIgnoreCase);
            p.Controls.Add(_rbNew);
            y += 26;
            _rbFolder = new RadioButton();
            _rbFolder.Text = "지정한 폴더/워크스페이스 열기";
            _rbFolder.Location = new Point(fieldX, y);
            _rbFolder.AutoSize = true;
            _rbFolder.Checked = !_rbNew.Checked;
            p.Controls.Add(_rbFolder);
            y += 26;
            _txtLeftPath = new TextBox();
            _txtLeftPath.Location = new Point(fieldX + 20, y);
            _txtLeftPath.Width = 460;
            _txtLeftPath.Text = _cfg.LeftClickPath == null ? "" : _cfg.LeftClickPath;
            p.Controls.Add(_txtLeftPath);
            Button bLeft = new Button();
            bLeft.Text = "찾아보기…";
            bLeft.Location = new Point(fieldX + 490, y - 1);
            bLeft.Size = new Size(110, 25);
            bLeft.Click += delegate
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                d.Description = "왼쪽 클릭 시 열 폴더";
                if (d.ShowDialog(this) == DialogResult.OK) _txtLeftPath.Text = d.SelectedPath;
            };
            p.Controls.Add(bLeft);
            _settingsRows.Add(new KeyValuePair<TextBox, Button>(_txtLeftPath, bLeft));
            y += 48;

            AddLabel(p, "설치 / 윈도우 설정", 12, y + 3, labelW, Color.Black, true);
            y += 28;

            Button bShortcut = new Button();
            bShortcut.Text = "시작 메뉴 바로가기 만들기 (작업 표시줄 고정용)";
            bShortcut.Location = new Point(fieldX, y);
            bShortcut.Size = new Size(330, 30);
            bShortcut.Click += CreateShortcut_Click;
            p.Controls.Add(bShortcut);
            y += 36;

            Button bLimit = new Button();
            bLimit.Text = "윈도우 점프 목록 표시 개수 늘리기…";
            bLimit.Location = new Point(fieldX, y);
            bLimit.Size = new Size(330, 30);
            bLimit.Click += RaiseJumpLimit_Click;
            p.Controls.Add(bLimit);
            y += 36;

            Button bCfgFile = new Button();
            bCfgFile.Text = "설정 파일(config.json) 열기";
            bCfgFile.Location = new Point(fieldX, y);
            bCfgFile.Size = new Size(330, 30);
            bCfgFile.Click += delegate
            {
                try
                {
                    ConfigStore.Save(CollectConfig());
                    Process.Start(new ProcessStartInfo(ConfigStore.ConfigPath) { UseShellExecute = true });
                }
                catch (Exception ex) { Program.Err(ex.Message); }
            };
            p.Controls.Add(bCfgFile);
            y += 44;

            Label note = new Label();
            _noteLabel = note;
            note.Location = new Point(12, y);
            note.Size = new Size(760, 120);
            note.ForeColor = Color.DimGray;
            note.Text =
                "· 등록할 수 있는 워크스페이스 개수에는 제한이 없습니다. 다만 윈도우 점프 목록 자체가 한 번에 보여주는 줄 수는\r\n" +
                "  운영체제가 정합니다(기본 10개 안팎). 그 수를 넘는 항목은 점프 목록의 [워크스페이스 관리 / 전체 목록…] 에서\r\n" +
                "  검색과 함께 전부 볼 수 있고, 위의 '표시 개수 늘리기' 로 윈도우 한도 자체를 올릴 수도 있습니다.\r\n" +
                "· 점프 목록에 넣을 항목과 순서는 [워크스페이스] 탭에서 체크와 드래그로 정합니다.";
            p.Controls.Add(note);

            p.Resize += delegate { LayoutSettingsRows(); };
            LayoutSettingsRows();
        }

        /// <summary>입력칸과 오른쪽 '찾아보기' 버튼을 현재 창 너비에 맞춰 배치한다.</summary>
        private void LayoutSettingsRows()
        {
            if (_settingsPanel == null) return;
            int right = _settingsPanel.ClientSize.Width - 18;
            if (right < 420) return;

            foreach (KeyValuePair<TextBox, Button> row in _settingsRows)
            {
                TextBox t = row.Key;
                Button b = row.Value;
                b.Left = right - b.Width;
                int w = b.Left - 12 - t.Left;
                if (w > 120) t.Width = w;
            }

            if (_noteLabel != null)
                _noteLabel.Width = Math.Max(400, right - _noteLabel.Left);
        }

        private void AddLabel(Panel p, string text, int x, int y, int w)
        {
            AddLabel(p, text, x, y, w, Color.Black, false);
        }

        private void AddLabel(Panel p, string text, int x, int y, int w, Color color)
        {
            AddLabel(p, text, x, y, w, color, false);
        }

        private void AddLabel(Panel p, string text, int x, int y, int w, Color color, bool bold)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 20);
            l.ForeColor = color;
            if (bold) l.Font = new Font(Font, FontStyle.Bold);
            p.Controls.Add(l);
        }

        // ------------------------------------------------------------------
        //  동작
        // ------------------------------------------------------------------
        private void ManagerForm_Load(object sender, EventArgs e)
        {
            if (_cfg.AutoScan)
            {
                int added = ConfigStore.SyncFromDisk(_cfg);
                if (added > 0) ConfigStore.Save(_cfg);
            }
            RefreshList(null);
            SetStatus(null);
            _search.Focus();
        }

        private bool IsFiltered()
        {
            return _search != null && _search.Text.Trim().Length > 0;
        }

        private void RefreshList(List<WsItem> keepSelected)
        {
            if (keepSelected == null) keepSelected = SelectedItems();

            _suppressCheck = true;
            _list.BeginUpdate();
            _list.Items.Clear();

            string q = _search.Text.Trim();
            foreach (WsItem it in _cfg.Items)
            {
                if (q.Length > 0)
                {
                    string hay = (it.DisplayName + " " + it.Path + " " + it.Group);
                    if (hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                ListViewItem lvi = new ListViewItem(it.DisplayName);
                lvi.SubItems.Add(it.KindLabel);
                lvi.SubItems.Add(it.Group);
                lvi.SubItems.Add(it.Path);
                lvi.Checked = it.Show;
                lvi.Tag = it;
                if (!it.Exists) lvi.ForeColor = Color.Firebrick;
                else if (!it.Show) lvi.ForeColor = Color.Gray;
                _list.Items.Add(lvi);
            }

            foreach (ListViewItem lvi in _list.Items)
                if (keepSelected.Contains((WsItem)lvi.Tag)) lvi.Selected = true;

            _list.EndUpdate();
            _suppressCheck = false;

            bool canOrder = !IsFiltered();
            _btnUp.Enabled = canOrder;
            _btnDown.Enabled = canOrder;
            _btnTop.Enabled = canOrder;
            _btnBottom.Enabled = canOrder;
        }

        private List<WsItem> SelectedItems()
        {
            List<WsItem> l = new List<WsItem>();
            if (_list == null) return l;
            foreach (ListViewItem lvi in _list.SelectedItems) l.Add((WsItem)lvi.Tag);
            return l;
        }

        private void List_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_suppressCheck) return;
            WsItem it = e.Item.Tag as WsItem;
            if (it == null) return;
            it.Show = e.Item.Checked;
            e.Item.ForeColor = !it.Exists ? Color.Firebrick : (it.Show ? SystemColors.WindowText : Color.Gray);
            SetStatus(null);
        }

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { RemoveSelected(null, null); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Up) { MoveSelected(-1); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Down) { MoveSelected(1); e.Handled = true; }
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (_list.Items.Count > 0)
                {
                    WsItem it = (WsItem)_list.Items[0].Tag;
                    OpenItem(it);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down && _list.Items.Count > 0)
            {
                _list.Focus();
                _list.Items[0].Selected = true;
                e.Handled = true;
            }
        }

        // ---- 순서 변경 ----
        private void MoveSelected(int delta)
        {
            if (IsFiltered()) return;
            List<WsItem> sel = SelectedItems();
            if (sel.Count == 0) return;

            List<int> idx = new List<int>();
            foreach (WsItem it in sel) idx.Add(_cfg.Items.IndexOf(it));
            idx.Sort();

            if (delta < 0)
            {
                for (int i = 0; i < idx.Count; i++)
                {
                    int cur = idx[i];
                    if (cur == 0) continue;
                    if (i > 0 && idx[i - 1] == cur - 1) continue;
                    WsItem tmp = _cfg.Items[cur - 1];
                    _cfg.Items[cur - 1] = _cfg.Items[cur];
                    _cfg.Items[cur] = tmp;
                    idx[i] = cur - 1;
                }
            }
            else
            {
                for (int i = idx.Count - 1; i >= 0; i--)
                {
                    int cur = idx[i];
                    if (cur >= _cfg.Items.Count - 1) continue;
                    if (i < idx.Count - 1 && idx[i + 1] == cur + 1) continue;
                    WsItem tmp = _cfg.Items[cur + 1];
                    _cfg.Items[cur + 1] = _cfg.Items[cur];
                    _cfg.Items[cur] = tmp;
                    idx[i] = cur + 1;
                }
            }

            RefreshList(sel);
            EnsureVisible(sel);
        }

        private void MoveToEdge(bool toTop)
        {
            if (IsFiltered()) return;
            List<WsItem> sel = SelectedItems();
            if (sel.Count == 0) return;

            List<WsItem> ordered = new List<WsItem>();
            foreach (WsItem it in _cfg.Items) if (sel.Contains(it)) ordered.Add(it);
            foreach (WsItem it in ordered) _cfg.Items.Remove(it);

            if (toTop) _cfg.Items.InsertRange(0, ordered);
            else _cfg.Items.AddRange(ordered);

            RefreshList(sel);
            EnsureVisible(sel);
        }

        private void EnsureVisible(List<WsItem> sel)
        {
            foreach (ListViewItem lvi in _list.Items)
                if (sel.Contains((WsItem)lvi.Tag)) { lvi.EnsureVisible(); break; }
            _list.Focus();
        }

        // ---- 드래그로 순서 변경 ----
        private void List_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (IsFiltered())
            {
                SetStatus("검색 중에는 순서를 바꿀 수 없습니다. 검색어를 지우세요.");
                return;
            }
            _list.DoDragDrop(_list.SelectedItems, DragDropEffects.Move);
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
            Point pt = _list.PointToClient(new Point(e.X, e.Y));
            ListViewItem over = _list.GetItemAt(pt.X, pt.Y);
            if (over == null)
            {
                _list.InsertionMark.Index = _list.Items.Count > 0 ? _list.Items.Count - 1 : -1;
                _list.InsertionMark.AppearsAfterItem = true;
                return;
            }
            bool after = pt.Y > over.Bounds.Top + over.Bounds.Height / 2;
            _list.InsertionMark.Index = over.Index;
            _list.InsertionMark.AppearsAfterItem = after;
        }

        private void List_DragDrop(object sender, DragEventArgs e)
        {
            int mark = _list.InsertionMark.Index;
            bool after = _list.InsertionMark.AppearsAfterItem;
            _list.InsertionMark.Index = -1;
            if (mark < 0 || IsFiltered()) return;

            List<WsItem> sel = SelectedItems();
            if (sel.Count == 0) return;

            WsItem anchor = (WsItem)_list.Items[mark].Tag;
            if (sel.Contains(anchor)) return;

            List<WsItem> ordered = new List<WsItem>();
            foreach (WsItem it in _cfg.Items) if (sel.Contains(it)) ordered.Add(it);
            foreach (WsItem it in ordered) _cfg.Items.Remove(it);

            int target = _cfg.Items.IndexOf(anchor);
            if (target < 0) target = _cfg.Items.Count;
            else if (after) target += 1;

            _cfg.Items.InsertRange(target, ordered);
            RefreshList(sel);
            EnsureVisible(sel);
        }

        // ---- 항목 추가/삭제 ----
        private void AddWorkspaceFiles(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "VS Code 워크스페이스 (*.code-workspace)|*.code-workspace|모든 파일 (*.*)|*.*";
            d.Multiselect = true;
            if (_cfg.ScanRoots.Count > 0 && Directory.Exists(_cfg.ScanRoots[0]))
                d.InitialDirectory = _cfg.ScanRoots[0];
            if (d.ShowDialog(this) != DialogResult.OK) return;

            int n = 0;
            foreach (string f in d.FileNames) if (AddPath(f)) n++;
            RefreshList(null);
            SetStatus(n + "개 추가했습니다.");
        }

        private void AddFolder(object sender, EventArgs e)
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            d.Description = "VS Code 로 열 폴더를 선택하세요";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (AddPath(d.SelectedPath))
            {
                RefreshList(null);
                SetStatus("폴더를 추가했습니다.");
            }
            else SetStatus("이미 목록에 있습니다.");
        }

        private bool AddPath(string path)
        {
            string norm = ConfigStore.NormalizePath(path);
            foreach (WsItem it in _cfg.Items)
                if (string.Equals(ConfigStore.NormalizePath(it.Path), norm,
                                  StringComparison.OrdinalIgnoreCase)) return false;
            WsItem n = new WsItem();
            n.Path = path;
            n.Show = true;
            _cfg.Items.Add(n);
            return true;
        }

        private void RescanNow(object sender, EventArgs e)
        {
            AppConfig cfg = CollectConfig();
            int added = ConfigStore.SyncFromDisk(cfg);
            RefreshList(null);
            SetStatus("스캔 완료 — 새로 찾은 항목 " + added + "개");
        }

        private void RenameSelected(object sender, EventArgs e)
        {
            List<WsItem> sel = SelectedItems();
            if (sel.Count != 1) { SetStatus("항목 하나를 선택하세요."); return; }
            string cur = sel[0].Name == null ? sel[0].DisplayName : sel[0].Name;
            string v;
            if (!InputBox.Show(this, "표시 이름", "점프 목록에 표시할 이름:", cur, out v)) return;
            sel[0].Name = string.IsNullOrEmpty(v.Trim()) ? null : v.Trim();
            RefreshList(sel);
        }

        private void SetGroupSelected(object sender, EventArgs e)
        {
            List<WsItem> sel = SelectedItems();
            if (sel.Count == 0) { SetStatus("항목을 선택하세요."); return; }
            string cur = sel[0].Group;
            string v;
            if (!InputBox.Show(this, "그룹", "그룹(카테고리) 이름 — 비우면 기본 그룹:", cur, out v)) return;
            foreach (WsItem it in sel) it.Group = v.Trim();
            RefreshList(sel);
        }

        private void RemoveSelected(object sender, EventArgs e)
        {
            List<WsItem> sel = SelectedItems();
            if (sel.Count == 0) return;
            if (MessageBox.Show(this, sel.Count + "개 항목을 목록에서 제거할까요?\r\n" +
                                "(디스크의 파일은 삭제되지 않습니다)",
                                "확인", MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Question) != DialogResult.OK) return;
            foreach (WsItem it in sel) _cfg.Items.Remove(it);
            RefreshList(new List<WsItem>());
            SetStatus(sel.Count + "개 제거했습니다.");
        }

        private void SetAllChecked(bool on)
        {
            _suppressCheck = true;
            foreach (ListViewItem lvi in _list.Items)
            {
                lvi.Checked = on;
                ((WsItem)lvi.Tag).Show = on;
            }
            _suppressCheck = false;
            RefreshList(null);
            SetStatus(null);
        }

        private void OpenSelected()
        {
            List<WsItem> sel = SelectedItems();
            if (sel.Count == 0) return;
            foreach (WsItem it in sel) OpenItem(it);
        }

        private void OpenItem(WsItem it)
        {
            string err;
            if (!VsCode.OpenPath(CollectConfig(), it.Path, false, out err)) Program.Err(err);
        }

        // ---- 저장/적용 ----
        private List<string> ParseRoots()
        {
            List<string> roots = new List<string>();
            foreach (string line in _txtRoots.Lines)
            {
                string s = line.Trim();
                if (s.Length > 0) roots.Add(s);
            }
            return roots;
        }

        private AppConfig CollectConfig()
        {
            _cfg.CodePath = _txtCode.Text.Trim();
            _cfg.ScanRoots = ParseRoots();
            _cfg.ScanDepth = (int)_numDepth.Value;
            _cfg.AutoScan = _chkAutoScan.Checked;
            _cfg.ScanFolders = _chkScanFolders.Checked;
            _cfg.FolderScanDepth = (int)_numFolderDepth.Value;
            _cfg.RequireProjectMarker = _chkRequireMarker.Checked;
            _cfg.MaxJumpItems = (int)_numMax.Value;
            _cfg.GroupByCategory = _chkGroup.Checked;
            _cfg.CategoryTitle = _txtCategory.Text.Trim().Length == 0
                ? "워크스페이스" : _txtCategory.Text.Trim();
            _cfg.LeftClickMode = _rbFolder.Checked ? "folder" : "new";
            _cfg.LeftClickPath = _txtLeftPath.Text.Trim();
            return _cfg;
        }

        private void SaveAndApply(bool report)
        {
            try
            {
                AppConfig cfg = CollectConfig();
                ConfigStore.Save(cfg);
                JumpListResult r = JumpListBuilder.Build(cfg);
                if (!r.Ok)
                {
                    SetStatus("점프 목록 적용 실패: " + r.Error);
                    if (report) Program.Err("점프 목록 적용에 실패했습니다.\r\n" + r.Error);
                    return;
                }
                string extra = "";
                if (report && r.TotalItems > r.ShownItems)
                    extra = "  ← 넘치는 항목은 점프 목록의 [워크스페이스 관리 / 전체 목록…] 에서 볼 수 있습니다.";
                ShowResult(r, extra);
            }
            catch (Exception ex)
            {
                Program.Err("저장 실패: " + ex.Message);
            }
        }

        private void SetStatus(string msg)
        {
            int total = 0, shown = 0;
            foreach (WsItem it in _cfg.Items) { total++; if (it.Show) shown++; }
            string baseText = "등록 " + total + "개 · 점프 목록 표시 대상 " + shown + "개";
            _status.Text = string.IsNullOrEmpty(msg) ? baseText : baseText + "   |   " + msg;
        }

        private void ShowResult(JumpListResult r, string extra)
        {
            _status.Text = "적용됨 — 등록 " + r.TotalItems + "개 · 점프 목록에 " + r.ShownItems +
                           "개 표시 · 상한 " + r.Capacity + "개 (윈도우 보고 " + r.MaxSlots + "개)" + extra;
        }

        // ---- 설치/윈도우 설정 ----
        private void CreateShortcut_Click(object sender, EventArgs e)
        {
            try
            {
                ConfigStore.Save(CollectConfig());
                JumpListBuilder.Build(_cfg);
                string lnk = ShortcutMaker.CreateStartMenuShortcut(
                    "VS Code 워크스페이스", Program.IconPath());

                DialogResult dr = MessageBox.Show(this,
                    "시작 메뉴에 바로가기를 만들었습니다.\r\n\r\n" + lnk + "\r\n\r\n" +
                    "작업 표시줄에 고정하는 방법\r\n" +
                    "  1) 시작 → 모든 앱 → 'VS Code 워크스페이스' 를 찾습니다\r\n" +
                    "  2) 마우스 오른쪽 클릭 → 자세히 → 작업 표시줄에 고정\r\n\r\n" +
                    "바로가기가 있는 폴더를 열까요?",
                    "바로가기 생성 완료", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (dr == DialogResult.Yes)
                    Process.Start("explorer.exe", "/select,\"" + lnk + "\"");
            }
            catch (Exception ex)
            {
                Program.Err("바로가기 생성 실패: " + ex.Message);
            }
        }

        private void RaiseJumpLimit_Click(object sender, EventArgs e)
        {
            string v;
            if (!InputBox.Show(this, "점프 목록 표시 개수",
                    "윈도우가 점프 목록에 표시할 최대 항목 수 (기본 10, 권장 20~30):",
                    "25", out v)) return;

            int n;
            if (!int.TryParse(v.Trim(), out n) || n < 1 || n > 60)
            {
                Program.Err("1 에서 60 사이의 숫자를 입력하세요.");
                return;
            }

            if (MessageBox.Show(this,
                    "레지스트리 값을 바꿉니다.\r\n\r\n" +
                    "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\r\n" +
                    "JumpListItems_Maximum = " + n + "\r\n\r\n" +
                    "적용하려면 탐색기(explorer.exe)를 다시 시작해야 합니다.\r\n" +
                    "지금 진행할까요? (열려 있는 탐색기 창이 모두 닫힙니다)",
                    "확인", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return;

            try
            {
                RegistryKey k = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                k.SetValue("JumpListItems_Maximum", n, RegistryValueKind.DWord);
                k.Close();

                foreach (Process p in Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(); } catch { }
                }
                Thread.Sleep(1200);
                if (Process.GetProcessesByName("explorer").Length == 0)
                    Process.Start("explorer.exe");

                Thread.Sleep(800);
                SaveAndApply(false);
                SetStatus("레지스트리를 적용하고 점프 목록을 다시 만들었습니다. 슬롯 수는 화면 높이에 따라 더 줄어들 수 있습니다.");
            }
            catch (Exception ex)
            {
                Program.Err("적용 실패: " + ex.Message);
            }
        }

        // 다른 인스턴스가 보낸 '앞으로 가져오기' 메시지 처리
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Program.WM_SHOW_MANAGER)
            {
                if (WindowState == FormWindowState.Minimized)
                    WindowState = FormWindowState.Normal;
                NativeWin.ShowWindow(Handle, NativeWin.SW_RESTORE);
                NativeWin.SetForegroundWindow(Handle);
                Activate();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { SaveAndApply(false); }
            catch (Exception ex) { Log.Write("close save: " + ex.Message); }
            base.OnFormClosing(e);
        }
    }

    // ------------------------------------------------------------------
    internal static class InputBox
    {
        public static bool Show(IWin32Window owner, string title, string prompt,
                                string initial, out string value)
        {
            value = "";
            Form f = new Form();
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ClientSize = new Size(460, 126);
            f.Font = new Font("Segoe UI", 9F);

            Label l = new Label();
            l.Text = prompt;
            l.Location = new Point(12, 14);
            l.Size = new Size(436, 20);
            f.Controls.Add(l);

            TextBox t = new TextBox();
            t.Text = initial == null ? "" : initial;
            t.Location = new Point(12, 40);
            t.Width = 436;
            t.SelectAll();
            f.Controls.Add(t);

            Button ok = new Button();
            ok.Text = "확인";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(272, 80);
            ok.Size = new Size(84, 28);
            f.Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "취소";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(364, 80);
            cancel.Size = new Size(84, 28);
            f.Controls.Add(cancel);

            f.AcceptButton = ok;
            f.CancelButton = cancel;

            bool okPressed = f.ShowDialog(owner) == DialogResult.OK;
            if (okPressed) value = t.Text;
            f.Dispose();
            return okPressed;
        }
    }
}
