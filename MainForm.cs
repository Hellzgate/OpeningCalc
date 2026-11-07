using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace OpeningCalc
{
    /// <summary>초동 확률 계산기 — .ydk 를 읽고 카드에 그룹(1~9)을 지정하면 확률을 계산한다.</summary>
    public sealed class MainForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(30, 31, 34);
        private static readonly Color Fg = Color.FromArgb(230, 232, 235);
        private static readonly Color Sub = Color.FromArgb(150, 156, 165);
        private static readonly Color Box = Color.FromArgb(43, 45, 49);
        private static readonly Color Accent = Color.FromArgb(86, 156, 214);
        private static readonly Color Row1 = Color.FromArgb(38, 40, 44);
        private static readonly Color Row2 = Color.FromArgb(34, 36, 39);
        private static readonly Color Border = Color.FromArgb(70, 74, 80);

        private TextBox TxtDeck, TxtCdb, TxtLog;
        private Button BtnOpen, BtnPickCdb, BtnCopy, BtnClear, BtnPlans, BtnTraps;
        private ComboBox CmbHand;
        private DataGridView GridCards, GridGroups, GridCombos;
        private Label LblSummary, LblGroupsH, LblCombosH;

        private DeckInfo _deck;
        private Analysis _last;
        private bool _wired;
        private int _hand = 5;                      // 5 = 선공, 6 = 후공
        private readonly List<Plan> _plans = new List<Plan>();
        private readonly List<CardGroup> _groups = new List<CardGroup>();   // 플랜에서 참조하는 카드군
        private readonly List<int> _trapIds = new List<int>();              // 패트랩 카드(따로 계산)
        private readonly List<int> _brickIds = new List<int>();             // 뽑히면 곤란한 카드

        private sealed class CardRow
        {
            public int Group { get; set; }
            public string Name { get; set; }
            public int Id { get; set; }
            public int Count { get; set; }
            public string P1 { get; set; }
        }

        public MainForm()
        {
            Text = "초동 확률 계산기 — .ydk (선공 5장)";            ClientSize = new Size(980, 664);
            MinimumSize = new Size(900, 600);
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Malgun Gothic", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AllowDrop = true;

            // 2026-11-07: 절대배치(SetBounds) → TableLayoutPanel (리사이즈·DPI 대응)
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Bg,
                Padding = new Padding(12, 8, 12, 10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));   // 제목
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // 덱/카드DB
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 본문
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));   // 로그
            Controls.Add(root);

            // ── 제목 ──────────────────────────────────────────
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Margin = new Padding(0) };
            var title = new Label
            {
                Text = "초동 확률 계산기",
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(0, 2, 0, 0),
                Font = new Font("Malgun Gothic", 12.5f, FontStyle.Bold),
                ForeColor = Fg,
                BackColor = Color.Transparent
            };
            var subTitle = new Label
            {
                Text = "손패 5장 · 카드의 '그룹'을 1~9 로 묶으면 그룹/조합 확률 · [초동 플랜…] 으로 다중 조건(카드군 N장) 계산",
                Dock = DockStyle.Top,
                Height = 16,
                Font = new Font("Malgun Gothic", 8.25f),
                ForeColor = Sub,
                BackColor = Color.Transparent
            };
            head.Controls.Add(subTitle);
            head.Controls.Add(title);
            root.Controls.Add(head, 0, 0);

            // ── 덱 / 카드DB ───────────────────────────────────
            var deckRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 1,
                BackColor = Bg,
                Margin = new Padding(0, 2, 0, 4)
            };
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));    // 덱
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));    // 덱 경로
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));   // 덱 열기
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));    // 패
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));    // 패 장수
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));    // 카드DB
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));   // cdb 경로
            deckRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));    // …

            var lblDeck = new Label { Text = "덱", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Sub, BackColor = Color.Transparent };
            TxtDeck = MakeText();
            TxtDeck.Dock = DockStyle.Fill;
            TxtDeck.AllowDrop = true;
            TxtDeck.Margin = new Padding(0, 3, 6, 3);
            BtnOpen = MakeButton("덱 열기…", primary: true);
            BtnOpen.Dock = DockStyle.Fill;
            BtnOpen.Margin = new Padding(0, 2, 8, 2);
            BtnOpen.Click += OnOpenDeck;

            var lblHand = new Label { Text = "패", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Sub, BackColor = Color.Transparent };
            CmbHand = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Box,
                ForeColor = Fg,
                Margin = new Padding(0, 2, 8, 2)
            };
            CmbHand.Items.AddRange(new object[] { "5 (선공)", "6 (후공)" });
            CmbHand.SelectedIndex = 0;
            CmbHand.SelectedIndexChanged += OnHandChanged;
            new ToolTip().SetToolTip(CmbHand, "손패 장수 — 선공 5장 / 후공 6장");

            var lblCdb = new Label { Text = "카드DB", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Sub, BackColor = Color.Transparent };
            TxtCdb = MakeText();
            TxtCdb.Dock = DockStyle.Fill;
            TxtCdb.Margin = new Padding(0, 3, 6, 3);
            BtnPickCdb = MakeButton("…");
            BtnPickCdb.Dock = DockStyle.Fill;
            BtnPickCdb.Margin = new Padding(0, 2, 0, 2);
            BtnPickCdb.Click += OnPickCdb;

            deckRow.Controls.Add(lblDeck, 0, 0);
            deckRow.Controls.Add(TxtDeck, 1, 0);
            deckRow.Controls.Add(BtnOpen, 2, 0);
            deckRow.Controls.Add(lblHand, 3, 0);
            deckRow.Controls.Add(CmbHand, 4, 0);
            deckRow.Controls.Add(lblCdb, 5, 0);
            deckRow.Controls.Add(TxtCdb, 6, 0);
            deckRow.Controls.Add(BtnPickCdb, 7, 0);
            root.Controls.Add(deckRow, 0, 1);

            // ── 본문: 카드(좌) | 요약·그룹·조합(우) ────────────
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Bg,
                Margin = new Padding(0, 2, 0, 6)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));

            GridCards = MakeGrid();
            GridCards.Dock = DockStyle.Fill;
            GridCards.Margin = new Padding(0, 0, 10, 0);
            GridCards.AutoGenerateColumns = false;
            GridCards.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "그룹",
                Name = "colGroup",
                DataPropertyName = "Group",
                Width = 52,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            });
            ((DataGridViewComboBoxColumn)GridCards.Columns[0]).Items.AddRange(new object[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            ((DataGridViewComboBoxColumn)GridCards.Columns[0]).ValueType = typeof(int);
            var colName = Col("Name", "카드", 290, true);
            colName.DataPropertyName = "Name";
            colName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            GridCards.Columns.Add(colName);
            var colId = Col("Id", "ID", 84, true);
            colId.DataPropertyName = "Id";
            GridCards.Columns.Add(colId);
            var colCnt = Col("Count", "장수", 52, true);
            colCnt.DataPropertyName = "Count";
            GridCards.Columns.Add(colCnt);
            var colP1 = Col("P1", "≥1 (이 카드)", 96, true);
            colP1.DataPropertyName = "P1";
            GridCards.Columns.Add(colP1);
            GridCards.CellValueChanged += OnCardEdited;
            GridCards.CurrentCellDirtyStateChanged += delegate
            {
                if (GridCards.IsCurrentCellDirty) GridCards.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            AttachHint(GridCards, "여기에 덱 카드가 표시됩니다.\n(.ydk 파일을 끌어놓거나 [덱 열기…])");

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Bg,
                Margin = new Padding(0)
            };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // 요약
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));   // 그룹 제목
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));    // 그룹 표
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));   // 조합 제목
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));    // 조합 표
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // 버튼(2행)

            LblSummary = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Accent,
                Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            LblGroupsH = new Label
            {
                Text = "그룹 요약 (그룹 = 초동 묶음)",
                Dock = DockStyle.Fill,
                ForeColor = Sub,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            GridGroups = MakeGrid();
            GridGroups.Dock = DockStyle.Fill;
            GridGroups.Margin = new Padding(0, 0, 0, 6);
            GridGroups.AutoGenerateColumns = false;
            GridGroups.Columns.Add(Col("g", "그룹", 48, true));
            GridGroups.Columns.Add(Col("cards", "내용", 150, true));
            GridGroups.Columns.Add(Col("n", "장수", 46, true));
            GridGroups.Columns.Add(Col("p1", "≥1", 70, true));
            AttachHint(GridGroups, "카드의 '그룹'을 1~9 로 지정하면\n그룹별 확률이 표시됩니다.");

            LblCombosH = new Label
            {
                Text = "조합 (둘 다 1장 이상)",
                Dock = DockStyle.Fill,
                ForeColor = Sub,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            GridCombos = MakeGrid();
            GridCombos.Dock = DockStyle.Fill;
            GridCombos.Margin = new Padding(0, 0, 0, 6);
            GridCombos.AutoGenerateColumns = false;
            GridCombos.Columns.Add(Col("a", "A", 40, true));
            GridCombos.Columns.Add(Col("b", "B", 40, true));
            GridCombos.Columns.Add(Col("p", "확률", 70, true));
            GridCombos.Columns.Add(Col("note", "내용", 160, true));
            AttachHint(GridCombos, "그룹이 2개 이상이면\n두 그룹이 동시에 잡힐 확률이 표시됩니다.");

            var btnRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Bg,
                Margin = new Padding(0, 6, 0, 0)
            };
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            btnRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));

            BtnCopy = MakeButton("결과 복사");
            BtnCopy.Dock = DockStyle.Fill;
            BtnCopy.Height = 28;
            BtnCopy.Margin = new Padding(0, 0, 4, 4);
            BtnCopy.Click += OnCopy;
            BtnClear = MakeButton("그룹 초기화");
            BtnClear.Dock = DockStyle.Fill;
            BtnClear.Height = 28;
            BtnClear.Margin = new Padding(4, 0, 0, 4);
            BtnClear.Click += OnClearGroups;
            BtnPlans = MakeButton("초동 플랜…", primary: true);
            BtnPlans.Dock = DockStyle.Fill;
            BtnPlans.Height = 28;
            BtnPlans.Margin = new Padding(0, 0, 4, 0);
            BtnPlans.Click += OnOpenPlans;
            new ToolTip().SetToolTip(BtnPlans, "여러 조건(카드군 N장)을 모두 만족하는 '초동 플랜'과\n플랜끼리의 하나라도/둘 다 확률을 계산합니다.");
            BtnTraps = MakeButton("확률 분석…");
            BtnTraps.Dock = DockStyle.Fill;
            BtnTraps.Height = 28;
            BtnTraps.Margin = new Padding(4, 0, 0, 0);
            BtnTraps.Click += OnOpenTraps;
            new ToolTip().SetToolTip(BtnTraps, "패트랩 · 곤란한 카드 · 미지정(나머지) 확률과\n초동과의 조합 확률을 한 화면에서 봅니다.");

            btnRow.Controls.Add(BtnCopy, 0, 0);
            btnRow.Controls.Add(BtnClear, 1, 0);
            btnRow.Controls.Add(BtnPlans, 0, 1);
            btnRow.Controls.Add(BtnTraps, 1, 1);

            right.Controls.Add(LblSummary, 0, 0);
            right.Controls.Add(LblGroupsH, 0, 1);
            right.Controls.Add(GridGroups, 0, 2);
            right.Controls.Add(LblCombosH, 0, 3);
            right.Controls.Add(GridCombos, 0, 4);
            right.Controls.Add(btnRow, 0, 5);

            body.Controls.Add(GridCards, 0, 0);
            body.Controls.Add(right, 1, 0);
            root.Controls.Add(body, 0, 2);

            // ── 로그 ──────────────────────────────────────────
            TxtLog = new TextBox();
            TxtLog.Multiline = true;
            TxtLog.ReadOnly = true;
            TxtLog.ScrollBars = ScrollBars.Vertical;
            TxtLog.Dock = DockStyle.Fill;
            TxtLog.BackColor = Color.FromArgb(24, 25, 28);
            TxtLog.ForeColor = Color.FromArgb(200, 210, 220);
            TxtLog.Font = new Font("Consolas", 8.5f);
            TxtLog.WordWrap = false;
            TxtLog.BorderStyle = BorderStyle.FixedSingle;
            TxtLog.Margin = new Padding(0, 2, 0, 0);
            root.Controls.Add(TxtLog, 0, 3);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            TxtDeck.DragEnter += OnDragEnter;
            TxtDeck.DragDrop += OnDragDrop;

            string cdb = DeckLoader.GuessEdoPro();
            TxtCdb.Text = cdb;

            LoadSettings();
            if (!string.IsNullOrEmpty(TxtDeck.Text) && File.Exists(TxtDeck.Text)) OpenDeck(TxtDeck.Text);

            Log("덱(.ydk)을 끌어놓거나 [덱 열기…]. 카드의 '그룹'을 1~9 로 지정하면 확률이 계산됩니다.");
            Recompute();
            _wired = true;
        }

        // ── UI 헬퍼 ───────────────────────────────────────────
        private void AddLabel(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.SetBounds(x, y, 60, 22);
            l.ForeColor = Sub;
            Controls.Add(l);
        }

        private TextBox MakeText()
        {
            var t = new TextBox();
            t.BackColor = Box;
            t.ForeColor = Fg;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = new Font("Consolas", 9f);      // 경로가 ₩ 로 보이지 않게(한글 폰트의 백슬래시 글리프)
            return t;
        }

        private Button MakeButton(string text, bool primary = false)
        {
            var b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = primary ? Accent : Border;
            b.BackColor = primary ? Accent : Color.FromArgb(52, 55, 60);
            b.ForeColor = primary ? Color.White : Fg;
            b.Font = new Font("Malgun Gothic", primary ? 9.5f : 9f, primary ? FontStyle.Bold : FontStyle.Regular);
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(108, 178, 236) : Color.FromArgb(62, 66, 73);
            b.FlatAppearance.MouseDownBackColor = primary ? Accent : Color.FromArgb(52, 55, 60);
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            return b;
        }

        private DataGridView MakeGrid()
        {
            var g = new DataGridView();
            g.BackgroundColor = Color.FromArgb(34, 36, 41);   // 빈 영역이 배경과 구분되게
            g.GridColor = Color.FromArgb(52, 55, 60);
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(44, 47, 52);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Fg;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            g.DefaultCellStyle.BackColor = Row1;
            g.DefaultCellStyle.ForeColor = Fg;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 90, 130);
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.AlternatingRowsDefaultCellStyle.BackColor = Row2;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.ReadOnly = false;
            g.RowTemplate.Height = 26;
            g.ColumnHeadersHeight = 30;
            g.DefaultCellStyle.Padding = new Padding(4, 0, 0, 0);
            return g;
        }

        private DataGridViewTextBoxColumn Col(string name, string head, int w, bool ro)
        {
            var c = new DataGridViewTextBoxColumn();
            c.Name = name;
            c.HeaderText = head;
            c.Width = w;
            c.ReadOnly = ro;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            return c;
        }

        /// <summary>행이 없을 때 그리드 안에 안내 문구를 그린다(빈 화면처럼 보이지 않게).</summary>
        private static void AttachHint(DataGridView g, string hint)
        {
            g.Paint += delegate (object s, PaintEventArgs e)
            {
                if (g.RowCount > 0) return;
                var r = g.ClientRectangle;
                r.Y += g.ColumnHeadersHeight + 2;
                r.Height -= g.ColumnHeadersHeight + 4;
                if (r.Height < 10) return;
                using (var f = new Font("Malgun Gothic", 9f))
                    TextRenderer.DrawText(e.Graphics, hint, f, r, Color.FromArgb(120, 128, 140),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            };
        }

        // ── 덱 ────────────────────────────────────────────────
        private void OnOpenDeck(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "덱 파일 (.ydk)";
                d.Filter = "Yu-Gi-Oh 덱 (*.ydk)|*.ydk|모든 파일 (*.*)|*.*";
                if (Directory.Exists(@"D:\edopro\WindBot\Decks")) d.InitialDirectory = @"D:\edopro\WindBot\Decks";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                OpenDeck(d.FileName);
            }
        }

        private void OpenDeck(string path)
        {
            try
            {
                TxtDeck.Text = path;
                _deck = DeckLoader.Load(path);
                string cdb = TxtCdb.Text.Trim();
                if (cdb.Length == 0) cdb = DeckLoader.GuessEdoPro();
                int named = cdb.Length > 0 ? DeckLoader.FillNames(_deck, cdb) : 0;

                foreach (var c in _deck.Main) c.Group = 0;
                _plans.Clear();
                _groups.Clear();
                _trapIds.Clear();
                _brickIds.Clear();
                var preset = PlanStore.Load(path);
                _plans.AddRange(preset.Plans);
                _groups.AddRange(preset.Groups);
                _trapIds.AddRange(preset.HandTrapIds);
                _brickIds.AddRange(preset.BrickIds);
                BindCards();
                Recompute();
                Log(string.Format("덱: {0} — 메인 {1}장({2}종) · 엑스트라 {3} · 사이드 {4} · 이름 {5}/{2}",
                    Path.GetFileName(path), _deck.Size, _deck.Main.Count, _deck.ExtraCount, _deck.SideCount, named));
                if (_plans.Count > 0 || _groups.Count > 0 || _trapIds.Count > 0 || _brickIds.Count > 0)
                    Log(string.Format("저장된 초동 플랜 {0}개 · 카드군 {1}개 · 패트랩 {2}종 · 곤란 카드 {3}종을 불러왔습니다.",
                        _plans.Count, _groups.Count, _trapIds.Count, _brickIds.Count));
                foreach (string w in _deck.Warnings.Take(3)) Log("⚠ " + w);
                SaveSettings();
            }
            catch (Exception ex)
            {
                Log("오류: " + ex.Message);
            }
        }

        private void BindCards()
        {
            var rows = _deck.Main.Select(c => new CardRow
            {
                Group = c.Group,
                Name = c.Name,
                Id = c.Id,
                Count = c.Count,
                P1 = _deck.Size > 0 ? Odds.AtLeastOne(_deck.Size, c.Count, _hand).ToString("P2") : "-"
            }).ToList();
            GridCards.DataSource = new BindingList<CardRow>(rows);
        }

        private void OnCardEdited(object sender, DataGridViewCellEventArgs e)
        {
            if (_deck == null) return;
            if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
            try
            {
                var row = GridCards.Rows[e.RowIndex].DataBoundItem as CardRow;
                if (row == null) return;
                int g = 0;
                object v = GridCards.Rows[e.RowIndex].Cells[0].Value;
                if (v != null) int.TryParse(v.ToString(), out g);
                row.Group = g;
                var card = _deck.Main.FirstOrDefault(c => c.Id == row.Id);
                if (card != null) card.Group = g;
                Recompute();
            }
            catch { }
        }

        // ── 계산/표시 ─────────────────────────────────────────
        private void Recompute()
        {
            if (_deck == null)
            {
                GridGroups.Rows.Clear();
                GridCombos.Rows.Clear();
                LblSummary.Text = "덱(.ydk)을 열어주세요\n끌어놓기 또는 [덱 열기…] (선공 5장 기준)";
                return;
            }
            var a = Analyzer.Run(_deck, _hand);
            _last = a;

            GridGroups.Rows.Clear();
            foreach (var g in a.Groups)
                GridGroups.Rows.Add(g.Group, g.Label, g.Count, g.P1.ToString("P2"));

            GridCombos.Rows.Clear();
            foreach (var c in a.Combos)
                GridCombos.Rows.Add(c.A, c.B, c.P.ToString("P2"), c.LabelA + " / " + c.LabelB);

            if (a.Groups.Count == 0)
                LblSummary.Text = string.Format("메인 {0}장 · 손패 {1}장\n카드의 '그룹'을 1~9 로 지정하세요", a.DeckSize, a.Hand);
            else
                LblSummary.Text = string.Format("메인 {0}장 · 손패 {1}장\n초동 {2}장 → {3:P2} (기대 {4:F2}장)",
                    a.DeckSize, a.Hand, a.StarterCount, a.AnyP, a.AnyExpected);
            if (_plans != null && _plans.Any(p => p.ValidIn(_groups)))
            {
                LblSummary.Text += string.Format("\n초동 플랜 {0}개 · 카드군 {1}개 — [초동 플랜…]",
                    _plans.Count(p => p.ValidIn(_groups)), _groups.Count);
            }
            int trapCards = _trapIds.Count == 0 ? 0 : _deck.Main.Where(x => _trapIds.Contains(x.Id)).Sum(x => x.Count);
            int brickCards = _brickIds.Count == 0 ? 0 : _deck.Main.Where(x => _brickIds.Contains(x.Id)).Sum(x => x.Count);
            var assigned = new HashSet<int>(_trapIds);
            assigned.UnionWith(_brickIds);
            foreach (var p in _plans)
            {
                if (p == null || !p.ValidIn(_groups)) continue;
                foreach (var s in p.Slots)
                    if (s != null && s.Valid) foreach (int id in s.ResolveIds(_groups)) assigned.Add(id);
            }
            int restCards = _deck.Main.Where(x => !assigned.Contains(x.Id)).Sum(x => x.Count);
            if (trapCards > 0 || brickCards > 0)
            {
                LblSummary.Text += string.Format("\n패트랩 {0}장 ≥1 {1:P1} · 곤란 {2}장 0장 {3:P1} · 미지정 {4}장 — [확률 분석…]",
                    trapCards, Odds.AtLeast(_deck.Size, trapCards, _hand, 1),
                    brickCards, brickCards <= 0 ? 1.0 : 1.0 - Odds.AtLeast(_deck.Size, brickCards, _hand, 1),
                    restCards);
            }
        }

        private void OnClearGroups(object sender, EventArgs e)
        {
            if (_deck == null) return;
            foreach (var c in _deck.Main) c.Group = 0;
            BindCards();
            Recompute();
            Log("그룹 초기화");
        }

        /// <summary>손패 장수(5 = 선공 / 6 = 후공) 변경.</summary>
        private void OnHandChanged(object sender, EventArgs e)
        {
            _hand = CmbHand != null && CmbHand.SelectedIndex == 1 ? 6 : 5;
            Text = "초동 확률 계산기 — .ydk (" + (_hand == 5 ? "선공 5장" : "후공 6장") + ")";
            if (_deck != null) BindCards();
            Recompute();
            if (_wired) Log(string.Format("손패 {0}장 기준으로 다시 계산했습니다.", _hand));
        }

        /// <summary>초동 플랜 창(여러 조건 AND + 플랜끼리 OR/AND, 카드군 참조).</summary>
        private void OnOpenPlans(object sender, EventArgs e)
        {
            if (_deck == null) { Log("덱(.ydk)을 먼저 열어주세요."); return; }
            using (var f = new PlanForm(_deck, _hand, new PlanStore.Preset { Plans = _plans, Groups = _groups, HandTrapIds = _trapIds, BrickIds = _brickIds }))
            {
                f.ShowDialog(this);
                _plans.Clear();
                _plans.AddRange(f.Plans.Select(p => p.Clone()));
                _groups.Clear();
                _groups.AddRange(f.Groups.Select(g => g.Clone()));
                _trapIds.Clear();
                _trapIds.AddRange(f.Preset.HandTrapIds);
                _brickIds.Clear();
                _brickIds.AddRange(f.Preset.BrickIds);
            }
            try
            {
                var res = PlanOdds.Compute(_deck, _plans, _hand, _groups);
                Log(string.Format("초동 플랜 {0}개 · 카드군 {1}개 저장됨 · 손패 {2}장 · 하나라도 {3:P2}",
                    _plans.Count(p => p.ValidIn(_groups)), _groups.Count, _hand, res.AnyP));
            }
            catch (Exception ex) { Log("플랜 계산 오류: " + ex.Message); }
            Recompute();
        }

        /// <summary>확률 분석 창(패트랩 · 곤란 · 미지정 + 초동 조합).</summary>
        private void OnOpenTraps(object sender, EventArgs e)
        {
            if (_deck == null) { Log("덱(.ydk)을 먼저 열어주세요."); return; }
            using (var f = new DeckStatsForm(_deck, _hand, _plans, _groups, _trapIds, _brickIds))
            {
                f.ShowDialog(this);
                _trapIds.Clear();
                _trapIds.AddRange(f.TrapIds);
                _brickIds.Clear();
                _brickIds.AddRange(f.BrickIds);
            }
            SaveStats();
            Recompute();
            int c = _deck.Main.Where(x => _trapIds.Contains(x.Id)).Sum(x => x.Count);
            int b = _deck.Main.Where(x => _brickIds.Contains(x.Id)).Sum(x => x.Count);
            if (c > 0 || b > 0)
                Log(string.Format("패트랩 {0}장 → ≥1 {1:P2} · 곤란 {2}장 → 0장일 확률 {3:P2} (손패 {4}장)",
                    c, Odds.AtLeast(_deck.Size, c, _hand, 1), b,
                    c <= 0 && b <= 0 ? 0 : 1.0 - Odds.AtLeast(_deck.Size, b, _hand, 1), _hand));
        }

        /// <summary>패트랩·곤란 목록만 프리셋에 저장(플랜·카드군은 그대로).</summary>
        private void SaveStats()
        {
            try
            {
                var preset = PlanStore.Load(_deck != null ? _deck.FilePath : null);
                preset.HandTrapIds = _trapIds.ToList();
                preset.BrickIds = _brickIds.ToList();
                PlanStore.Save(_deck != null ? _deck.FilePath : null, preset);
            }
            catch { }
        }

        private void OnCopy(object sender, EventArgs e)
        {
            if (_last == null) return;
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("{0} · 메인 {1}장 · 손패 {2}장(선공)", Path.GetFileName(_deck.FilePath), _last.DeckSize, _last.Hand));
            sb.AppendLine();
            foreach (var g in _last.Groups)
                sb.AppendLine(string.Format("[{0}] {1}  {2}장  ≥1 {3:P2}  =1 {4:P2}  ≥2 {5:P2}",
                    g.Group, g.Label, g.Count, g.P1, g.PExact1, g.P2Plus));
            sb.AppendLine();
            sb.AppendLine(string.Format("초동 {0}장 → 1장 이상 {1:P2} (기대 {2:F2}장)", _last.StarterCount, _last.AnyP, _last.AnyExpected));
            if (_last.Combos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("조합 (둘 다 1장 이상)");
                foreach (var c in _last.Combos)
                    sb.AppendLine(string.Format("  [{0}] ∧ [{1}]  {2:P2}", c.A, c.B, c.P));
            }
            if (_plans != null && _plans.Any(p => p.ValidIn(_groups)) && _deck != null)
            {
                try
                {
                    var pr = PlanOdds.Compute(_deck, _plans, _last.Hand, _groups);
                    sb.AppendLine();
                    sb.AppendLine(string.Format("초동 플랜 (손패 {0}장)", _last.Hand));
                    for (int i = 0; i < _plans.Count; i++)
                    {
                        if (!_plans[i].ValidIn(_groups)) continue;
                        sb.AppendLine(string.Format("  {0} : {1:P2}   [ {2} ]",
                            _plans[i].Name, pr.PlanP[i], _plans[i].Describe(_deck, _groups)));
                    }
                    sb.AppendLine(string.Format("  하나라도 : {0:P2}", pr.AnyP));
                    foreach (var p in pr.Pairs)
                    {
                        if (double.IsNaN(p.P)) continue;
                        string strict = double.IsNaN(p.PDisjoint) ? "생략" : p.PDisjoint.ToString("P2");
                        sb.AppendLine(string.Format("  {0} ∧ {1} : 겹침 허용 {2:P2} / 카드 분리 {3}",
                            _plans[p.A].Name, _plans[p.B].Name, p.P, strict));
                    }
                }
                catch { }
            }
            try { Clipboard.SetText(sb.ToString()); Log("결과를 클립보드에 복사했습니다."); }
            catch (Exception ex) { Log("복사 실패: " + ex.Message); }
        }

        private void OnPickCdb(object sender, EventArgs e)
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "EDOPro 폴더 (cdb 가 있는 곳)";
                if (Directory.Exists(TxtCdb.Text)) d.SelectedPath = TxtCdb.Text;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                TxtCdb.Text = d.SelectedPath;
                if (_deck != null) OpenDeck(_deck.FilePath);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            OpenDeck(files[0]);
        }

        // ── 검증/설정 ─────────────────────────────────────────
        /// <summary>레이아웃 진단: 각 컨트롤 실제 위치/크기(DPI·AutoScale 확인용).</summary>
        public string DumpLayout()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("form client={0}x{1} dpi={2} autoscale={3} font={4} {5}pt",
                ClientSize.Width, ClientSize.Height, DeviceDpi, AutoScaleMode, Font.Name, Font.Size));
            foreach (Control c in Controls)
            {
                sb.AppendLine(string.Format("  {0,-10} {1,-14} x={2,4} y={3,4} w={4,4} h={5,4} anchor={6}",
                    c.GetType().Name, string.IsNullOrEmpty(c.Text) ? c.Name : c.Text.Replace("\r", " ").Replace("\n", " ").Trim(),
                    c.Left, c.Top, c.Width, c.Height, c.Anchor));
            }
            return sb.ToString();
        }
        /// <summary>창을 띄우지 않고 화면을 PNG 로 저장(레이아웃 검증용).</summary>
        public void SaveScreenshot(string path)
        {
            // 화면 밖(-4000,-4000)에 잠깐 띄워 자식을 실제로 그린 뒤 저장(사용자에게는 안 보임)
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-4000, -4000);
            Show();
            Application.DoEvents();
            Refresh();
            Application.DoEvents();
            using (var bmp = new Bitmap(Width, Height))
            {
                DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            Hide();
        }

        /// <summary>검증용: 덱을 열고 그룹을 자동 배정(1=첫 카드, 2=두번째 ...)해 화면을 채운다.</summary>
        public void OpenDeckForTest(string path)
        {
            OpenDeck(path);
            if (_deck == null) return;
            int g = 0;
            foreach (var c in _deck.Main)
            {
                if (c.Count <= 0) continue;
                c.Group = (g++ % 2) + 1;      // 데모: 2개 그룹으로 번갈아 배정
            }
            BindCards();
            Recompute();
        }

        public int ControlCount() { return Controls.Count; }

        public string CheckControls()
        {
            var sb = new StringBuilder();
            sb.Append("deck=" + (TxtDeck != null ? "OK" : "FAIL"));
            sb.Append(" cdb=" + (TxtCdb != null ? "OK" : "FAIL"));
            sb.Append(" open=" + (BtnOpen != null ? "OK" : "FAIL"));
            sb.Append(" cards=" + (GridCards != null && GridCards.Columns.Count == 5 ? "OK" : "FAIL"));
            sb.Append(" groups=" + (GridGroups != null && GridGroups.Columns.Count == 4 ? "OK" : "FAIL"));
            sb.Append(" combos=" + (GridCombos != null && GridCombos.Columns.Count == 4 ? "OK" : "FAIL"));
            sb.Append(" copy=" + (BtnCopy != null ? "OK" : "FAIL"));
            sb.Append(" plans=" + (BtnPlans != null ? "OK" : "FAIL"));
            sb.Append(" traps=" + (BtnTraps != null ? "OK" : "FAIL"));
            sb.Append(" hand=" + (CmbHand != null && CmbHand.Items.Count == 2 ? "OK" : "FAIL"));
            sb.Append(" wired=" + (_wired ? "OK" : "FAIL"));
            sb.Append(" math=" + (MathCheck() ? "OK" : "FAIL"));
            sb.Append(" planmath=" + (PlanMathCheck() ? "OK" : "FAIL"));
            sb.Append(" groupui=" + (GroupUiCheck() ? "OK" : "FAIL"));
            sb.Append(" trapui=" + (TrapUiCheck() ? "OK" : "FAIL"));
            sb.Append(" trapmath=" + (TrapMathCheck() ? "OK" : "FAIL"));
            sb.Append(" preset=" + (PresetRoundTripCheck() ? "OK" : "FAIL"));
            return sb.ToString();
        }

        /// <summary>확률 분석 창이 예외 없이 만들어지는지(창은 띄우지 않는다).</summary>
        private bool TrapUiCheck()
        {
            if (_deck == null || _deck.Main.Count < 3) return false;
            try
            {
                var ids = _deck.Main.Take(2).Select(c => c.Id).ToList();
                var br = _deck.Main.Skip(2).Take(1).Select(c => c.Id).ToList();
                using (var f = new DeckStatsForm(_deck, _hand, _plans, _groups, ids, br))
                {
                    f.CreateControl();
                    f.SetCards(ids, br);
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>패트랩 조합 확률 점검 — 10장(패트랩 6 + 3번 카드 4)에서 손패 4장 기준 손계산과 대조.</summary>
        private static bool TrapMathCheck()
        {
            var deck = new DeckInfo();
            deck.Main.Add(new CardEntry { Id = 1, Count = 3, Name = "#1" });
            deck.Main.Add(new CardEntry { Id = 2, Count = 3, Name = "#2" });
            deck.Main.Add(new CardEntry { Id = 3, Count = 4, Name = "#3" });

            // 패트랩 ≥1 = 1 - C(4,4)/C(10,4) = 1 - 1/210
            double p1 = Odds.AtLeast(10, 6, 4, 1);
            bool ok1 = Math.Abs(p1 - (1.0 - 1.0 / 210.0)) < 1e-12;

            // 초동(3번 1장) + 패트랩 ≥1 = 1 - (전부 3번) - (전부 트랩) + 0 = 194/210
            var comb = new Plan
            {
                Name = "조합",
                Slots =
                {
                    new PlanSlot { Need = 1, Ids = new List<int> { 3 } },
                    new PlanSlot { Need = 1, Ids = new List<int> { 1, 2 } }
                }
            };
            var r = PlanOdds.Compute(deck, new List<Plan> { comb }, 4, null, false, false);
            bool ok2 = Math.Abs(r.PlanP[0] - 194.0 / 210.0) < 1e-9;
            return ok1 && ok2;
        }

        /// <summary>프리셋(카드군 + 플랜) 저장→불러오기 왕복.</summary>
        private static bool PresetRoundTripCheck()
        {
            string key = Path.Combine(Path.GetTempPath(), "oc_rt_" + Guid.NewGuid().ToString("N") + ".ydk");
            try
            {
                var p = new PlanStore.Preset();
                p.Groups.Add(new CardGroup { Name = "G1", Ids = new List<int> { 1, 2 } });
                p.Plans.Add(new Plan
                {
                    Name = "P1",
                    Slots = { new PlanSlot { Need = 2, GroupNames = new List<string> { "G1" } } }
                });
                PlanStore.Save(key, p);
                var back = PlanStore.Load(key);
                return back.Groups.Count == 1 && back.Groups[0].Name == "G1" && back.Groups[0].Ids.Count == 2
                    && back.Plans.Count == 1 && back.Plans[0].Slots.Count == 1
                    && back.Plans[0].Slots[0].Need == 2
                    && back.Plans[0].Slots[0].GroupNames.Count == 1
                    && back.Plans[0].Slots[0].GroupNames[0] == "G1";
            }
            catch { return false; }
            finally { try { File.Delete(PlanStore.PathFor(key)); } catch { } }
        }

        /// <summary>카드군 편집 창·조건 창이 예외 없이 만들어지는지(창은 띄우지 않는다).</summary>
        private bool GroupUiCheck()
        {
            if (_deck == null || _deck.Main.Count == 0) return false;
            var groups = new List<CardGroup>
            {
                new CardGroup { Name = "테스트군", Ids = new List<int> { _deck.Main[0].Id } }
            };
            try
            {
                using (var g = new CardGroupForm(_deck, groups)) { g.CreateControl(); }
                using (var s = new SlotForm(_deck, groups, new PlanSlot { Need = 1, GroupNames = new List<string> { "테스트군" } })) { s.CreateControl(); }
                using (var p = new PlanForm(_deck, _hand, new PlanStore.Preset { Plans = _plans, Groups = groups }))
                {
                    p.NoSaveOnClose = true;
                    p.CreateControl();
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>플랜 엔진 자체 점검 — 겹치는 두 슬롯은 "카드 2장"이 필요하다 + 카드군 참조 = 명시 ID.</summary>
        private static bool PlanMathCheck()
        {
            var deck = new DeckInfo();
            deck.Main.Add(new CardEntry { Id = 1, Count = 2, Name = "#1" });
            deck.Main.Add(new CardEntry { Id = 2, Count = 2, Name = "#2" });
            deck.Main.Add(new CardEntry { Id = 3, Count = 2, Name = "#3" });
            var plans = new List<Plan>
            {
                new Plan { Name = "A", Slots = { new PlanSlot { Need = 1, Ids = new List<int> { 1 } } } },
                new Plan { Name = "B", Slots = { new PlanSlot { Need = 1, Ids = new List<int> { 1 } } } }
            };
            var r = PlanOdds.Compute(deck, plans, 3);
            // 6장 중 3장: P(1번 카드 ≥1) = 0.8, P(1번 2장) = C(2,2)*C(4,1)/C(6,3) = 0.2
            bool baseOk = Math.Abs(r.PlanP[0] - 0.8) < 1e-9
                && Math.Abs(r.Pairs[0].P - 0.8) < 1e-9              // 겹침 허용
                && Math.Abs(r.Pairs[0].PDisjoint - 0.2) < 1e-9;     // 카드 분리
            if (!baseOk) return false;

            // 카드군 참조가 명시 ID 와 **완전히 같은 값**을 내야 한다
            var groups = new List<CardGroup> { new CardGroup { Name = "G", Ids = new List<int> { 1, 2 } } };
            var byGroup = PlanOdds.Compute(deck, new List<Plan>
            {
                new Plan { Name = "S", Slots = { new PlanSlot { Need = 1, GroupNames = new List<string> { "G" } } } }
            }, 3, groups);
            var byIds = PlanOdds.Compute(deck, new List<Plan>
            {
                new Plan { Name = "S", Slots = { new PlanSlot { Need = 1, Ids = new List<int> { 1, 2 } } } }
            }, 3);
            return Math.Abs(byGroup.PlanP[0] - byIds.PlanP[0]) < 1e-12 && byGroup.PlanP[0] > 0;
        }

        private static bool MathCheck()
        {
            // 40장/3장/5장 ≥1 = 33.76%, 2그룹 조합이 각 그룹 단독보다 작을 것
            double p = Odds.AtLeastOne(40, 3, 5);
            double both = Odds.ComboAtLeastOne(40, new[] { 3, 3 }, 5);
            double one = Odds.AtLeastOne(40, 3, 5);
            return Math.Abs(p - 0.3376) < 0.0002 && both < one && both > 0;
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpeningCalc", "settings.json");
            }
        }

        private sealed class Settings { public string Deck { get; set; } public string Cdb { get; set; } }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (s == null) return;
                if (!string.IsNullOrEmpty(s.Deck)) TxtDeck.Text = s.Deck;
                if (!string.IsNullOrEmpty(s.Cdb)) TxtCdb.Text = s.Cdb;
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new Settings { Deck = TxtDeck.Text.Trim(), Cdb = TxtCdb.Text.Trim() },
                    new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            }
            catch { }
        }

        private void Log(string line)
        {
            if (TxtLog == null || TxtLog.IsDisposed) return;
            TxtLog.AppendText(line + Environment.NewLine);
        }
    }
}
