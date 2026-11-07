// 초동 "플랜" 편집/계산 창 — 참고 앱(D:\edopro\초동확률 계산기)의 "초동 + 카드군" 모델을 옮기고,
// 카드군이 겹치는 경우까지 정확히 계산한다(PlanOdds).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace OpeningCalc
{
    /// <summary>플랜 프리셋 저장/불러오기 — 덱 파일별로 %LOCALAPPDATA%\OpeningCalc\plans\ 에 둔다.</summary>
    public static class PlanStore
    {
        private sealed class GroupDto { public string Name { get; set; } public List<int> Ids { get; set; } }
        private sealed class SlotDto { public int Need { get; set; } public List<int> Ids { get; set; } public List<string> Groups { get; set; } }
        private sealed class PlanDto { public string Name { get; set; } public List<SlotDto> Slots { get; set; } }
        private sealed class FileDto { public string Deck { get; set; } public List<GroupDto> Groups { get; set; } public List<PlanDto> Plans { get; set; } public List<int> HandTraps { get; set; } public List<int> Bricks { get; set; } }

        /// <summary>덱 하나의 프리셋(카드군 + 플랜 + 패트랩/곤란 카드).</summary>
        public sealed class Preset
        {
            public List<CardGroup> Groups = new List<CardGroup>();
            public List<Plan> Plans = new List<Plan>();
            public List<int> HandTrapIds = new List<int>();
            public List<int> BrickIds = new List<int>();
        }

        public static string Folder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpeningCalc", "plans");
            }
        }

        public static string PathFor(string deckPath)
        {
            string name = "deck";
            try
            {
                if (!string.IsNullOrEmpty(deckPath))
                {
                    name = Path.GetFileNameWithoutExtension(deckPath);
                    foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                }
            }
            catch { }
            return Path.Combine(Folder, name + ".json");
        }

        public static Preset Load(string deckPath)
        {
            var preset = new Preset();
            try
            {
                string p = PathFor(deckPath);
                if (!File.Exists(p)) return preset;
                var d = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(p));
                if (d == null) return preset;

                foreach (var gd in (d.Groups ?? new List<GroupDto>()))
                    preset.Groups.Add(new CardGroup { Name = gd.Name ?? "", Ids = (gd.Ids ?? new List<int>()).ToList() });

                foreach (var pd in (d.Plans ?? new List<PlanDto>()))
                {
                    var plan = new Plan { Name = pd.Name ?? "" };
                    foreach (var sd in (pd.Slots ?? new List<SlotDto>()))
                        plan.Slots.Add(new PlanSlot
                        {
                            Need = sd.Need,
                            Ids = (sd.Ids ?? new List<int>()).ToList(),
                            GroupNames = (sd.Groups ?? new List<string>()).Where(x => !string.IsNullOrEmpty(x)).ToList()
                        });
                    preset.Plans.Add(plan);
                }

                if (d.HandTraps != null) preset.HandTrapIds.AddRange(d.HandTraps);
                if (d.Bricks != null) preset.BrickIds.AddRange(d.Bricks);
            }
            catch { }
            return preset;
        }

        public static void Save(string deckPath, Preset preset)
        {
            if (preset == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                var dto = new FileDto
                {
                    Deck = deckPath,
                    HandTraps = (preset.HandTrapIds ?? new List<int>()).ToList(),
                    Bricks = (preset.BrickIds ?? new List<int>()).ToList(),
                    Groups = preset.Groups.Select(g => new GroupDto { Name = g.Name, Ids = g.Ids.ToList() }).ToList(),
                    Plans = preset.Plans.Select(p => new PlanDto
                    {
                        Name = p.Name,
                        Slots = p.Slots.Select(s => new SlotDto
                        {
                            Need = s.Need,
                            Ids = s.Ids.ToList(),
                            Groups = s.GroupNames.ToList()
                        }).ToList()
                    }).ToList()
                };
                File.WriteAllText(PathFor(deckPath),
                    JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            }
            catch { }
        }
    }

    /// <summary>카드군(슬롯) 하나를 만드는 작은 창 — 카드군 참조 또는 개별 카드 선택 + "N장".</summary>
    public sealed class SlotForm : Form
    {
        private readonly DeckInfo _deck;
        private readonly IList<CardGroup> _groups;
        private CheckedListBox _clbGroups, _clb;
        private TextBox _txtFind;
        private NumericUpDown _num;
        public PlanSlot Result;

        private static readonly Color Bg = Color.FromArgb(30, 31, 34);
        private static readonly Color Fg = Color.FromArgb(230, 232, 235);
        private static readonly Color Sub = Color.FromArgb(150, 156, 165);
        private static readonly Color Box = Color.FromArgb(43, 45, 49);
        private static readonly Color Accent = Color.FromArgb(86, 156, 214);

        public SlotForm(DeckInfo deck, IList<CardGroup> groups, PlanSlot existing)
        {
            _deck = deck;
            _groups = groups ?? new List<CardGroup>();
            Text = existing == null ? "조건(카드군) 추가" : "조건(카드군) 편집";
            ClientSize = new Size(440, 600);
            MinimumSize = new Size(380, 480);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Malgun Gothic", 9f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Bg, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // 안내
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));   // 카드군 라벨
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));  // 카드군 목록
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // 카드 검색
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 카드 목록
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // 장수
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // 버튼
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "카드군을 고르면 그 안의 카드 전부가 후보가 됩니다. 개별 카드도 함께 고를 수 있습니다.",
                Dock = DockStyle.Fill,
                ForeColor = Sub,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            root.Controls.Add(new Label
            {
                Text = "카드군 (없으면 [플랜 창 → 카드군…] 에서 만드세요)",
                Dock = DockStyle.Fill,
                ForeColor = Sub,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            _clbGroups = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 40, 44),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false
            };
            foreach (var g in _groups)
                _clbGroups.Items.Add(new GroupItem(g));
            root.Controls.Add(_clbGroups, 0, 2);

            _txtFind = new TextBox { Dock = DockStyle.Fill, BackColor = Box, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle };
            _txtFind.TextChanged += delegate { FillList(); };
            root.Controls.Add(_txtFind, 0, 3);

            _clb = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 40, 44),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false
            };
            root.Controls.Add(_clb, 0, 4);

            var numRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg };
            numRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            numRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            numRow.Controls.Add(new Label { Text = "필요 장수", Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _num = new NumericUpDown { Dock = DockStyle.Left, Width = 70, Minimum = 1, Maximum = 6, Value = 1, BackColor = Box, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle };
            numRow.Controls.Add(_num, 1, 0);
            root.Controls.Add(numRow, 0, 5);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Bg, WrapContents = false };
            var ok = new Button { Text = "확인", Width = 96, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Color.White, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            ok.FlatAppearance.BorderSize = 1;
            ok.FlatAppearance.BorderColor = Accent;
            ok.Click += OnOk;
            var cancel = new Button { Text = "취소", Width = 96, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 55, 60), ForeColor = Fg, DialogResult = DialogResult.Cancel };
            cancel.FlatAppearance.BorderSize = 1;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(70, 74, 80);
            btns.Controls.Add(ok);
            btns.Controls.Add(cancel);
            root.Controls.Add(btns, 0, 6);
            AcceptButton = ok;
            CancelButton = cancel;

            FillList();
            if (existing != null)
            {
                _num.Value = Math.Max(1, Math.Min(6, existing.Need));
                for (int i = 0; i < _clbGroups.Items.Count; i++)
                {
                    var gi = _clbGroups.Items[i] as GroupItem;
                    if (gi != null && existing.GroupNames.Any(n => string.Equals(n, gi.Name, StringComparison.OrdinalIgnoreCase)))
                        _clbGroups.SetItemChecked(i, true);
                }
                for (int i = 0; i < _clb.Items.Count; i++)
                {
                    var ce = _clb.Items[i] as CardEntry;
                    if (ce != null && existing.Ids.Contains(ce.Id)) _clb.SetItemChecked(i, true);
                }
            }
        }

        private sealed class GroupItem
        {
            public readonly string Name;
            private readonly string _text;
            public GroupItem(CardGroup g)
            {
                Name = g.Name;
                _text = string.IsNullOrEmpty(g.Name) ? "(이름 없음)" : g.Name + "  ·  " + g.Ids.Count + "종";
            }
            public override string ToString() { return _text; }
        }

        private void FillList()
        {
            string q = (_txtFind.Text ?? "").Trim();
            var checkedIds = new HashSet<int>();
            foreach (var o in _clb.CheckedItems) { var ce = o as CardEntry; if (ce != null) checkedIds.Add(ce.Id); }

            _clb.BeginUpdate();
            _clb.Items.Clear();
            foreach (var c in _deck.Main)
            {
                if (q.Length > 0 &&
                    c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    c.Id.ToString().IndexOf(q, StringComparison.Ordinal) < 0) continue;
                _clb.Items.Add(c);
            }
            for (int i = 0; i < _clb.Items.Count; i++)
            {
                var ce = _clb.Items[i] as CardEntry;
                if (ce != null && checkedIds.Contains(ce.Id)) _clb.SetItemChecked(i, true);
            }
            _clb.EndUpdate();
        }

        private void OnOk(object sender, EventArgs e)
        {
            var ids = new List<int>();
            foreach (var o in _clb.CheckedItems) { var ce = o as CardEntry; if (ce != null) ids.Add(ce.Id); }
            var groupNames = new List<string>();
            foreach (var o in _clbGroups.CheckedItems) { var gi = o as GroupItem; if (gi != null) groupNames.Add(gi.Name); }

            var probe = new PlanSlot { Need = (int)_num.Value, Ids = ids, GroupNames = groupNames };
            if (!probe.Valid) { DialogResult = DialogResult.None; MessageBox.Show(this, "카드군이나 카드를 1개 이상 고르세요.", "조건"); return; }

            var resolved = probe.ResolveIds(_groups);
            int total = _deck.Main.Where(c => resolved.Contains(c.Id)).Sum(c => c.Count);
            if (total < (int)_num.Value)
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(this, string.Format("이 조합은 덱에 {0}장뿐이라 {1}장을 요구할 수 없습니다.", total, _num.Value), "조건");
                return;
            }
            Result = probe;
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>카드군 목록 관리 — 추가/편집/삭제 + 메인 화면의 그룹 1~9 가져오기.</summary>
    public sealed class CardGroupForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(30, 31, 34);
        private static readonly Color Fg = Color.FromArgb(230, 232, 235);
        private static readonly Color Sub = Color.FromArgb(150, 156, 165);
        private static readonly Color Accent = Color.FromArgb(86, 156, 214);
        private static readonly Color Border = Color.FromArgb(70, 74, 80);

        private readonly DeckInfo _deck;
        private readonly List<CardGroup> _groups;
        private ListBox _lst;
        public List<CardGroup> Groups { get { return _groups; } }

        public CardGroupForm(DeckInfo deck, IList<CardGroup> groups)
        {
            _deck = deck;
            _groups = (groups ?? new List<CardGroup>()).Select(g => g.Clone()).ToList();
            Text = "카드군 — 플랜에서 이름으로 참조";
            ClientSize = new Size(600, 520);
            MinimumSize = new Size(520, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Malgun Gothic", 9f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Bg, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(root);

            var head = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            head.Controls.Add(new Label
            {
                Text = "카드군",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Malgun Gothic", 11.5f, FontStyle.Bold),
                ForeColor = Fg
            });
            head.Controls.Add(new Label
            {
                Text = "여러 카드를 하나로 묶어 이름을 붙입니다 · 플랜 조건에서 이 이름을 고르면 그 카드들이 후보가 됩니다\n(카드군을 고치면 그 카드군을 쓰는 플랜도 자동으로 따라갑니다)",
                Dock = DockStyle.Bottom,
                Height = 40,
                Font = new Font("Malgun Gothic", 8.25f),
                ForeColor = Sub
            });
            root.Controls.Add(head, 0, 0);

            _lst = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 40, 44),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            _lst.DoubleClick += delegate { OnEdit(null, EventArgs.Empty); };
            root.Controls.Add(_lst, 0, 1);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, WrapContents = false };
            btns.Controls.Add(Small("+ 카드군", OnAdd));
            btns.Controls.Add(Small("편집", OnEdit));
            btns.Controls.Add(Small("삭제", OnDelete));
            var bImport = Small("메인 그룹에서 가져오기", OnImportMainGroups);
            bImport.Width = 176;
            btns.Controls.Add(bImport);
            root.Controls.Add(btns, 0, 2);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Bg, WrapContents = false };
            var ok = Small("확인", delegate { DialogResult = DialogResult.OK; });
            ok.Width = 96;
            ok.BackColor = Accent;
            ok.ForeColor = Color.White;
            ok.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            var cancel = Small("취소", delegate { DialogResult = DialogResult.Cancel; });
            cancel.Width = 96;
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            root.Controls.Add(bottom, 0, 3);

            RefreshList();
        }

        private Button Small(string text, EventHandler click)        {
            var b = new Button
            {
                Text = text,
                Width = 84,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 55, 60),
                ForeColor = Fg,
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 66, 73);
            b.Click += click;
            return b;
        }

        private void RefreshList()
        {
            _lst.BeginUpdate();
            _lst.Items.Clear();
            for (int i = 0; i < _groups.Count; i++)
                _lst.Items.Add(string.Format("{0}. {1}  ·  {2}", i + 1,
                    string.IsNullOrEmpty(_groups[i].Name) ? "(이름 없음)" : _groups[i].Name,
                    _groups[i].Describe(_deck)));
            _lst.EndUpdate();
        }

        private void OnAdd(object sender, EventArgs e)
        {
            var g = NewGroup();
            if (g == null) return;
            _groups.Add(g);
            RefreshList();
            _lst.SelectedIndex = _groups.Count - 1;
        }

        private CardGroup NewGroup()
        {
            using (var f = new CardGroupEditForm(_deck, null))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return null;
                if (_groups.Any(x => string.Equals(x.Name, f.Result.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(this, "같은 이름의 카드군이 이미 있습니다.", "카드군");
                    return null;
                }
                return f.Result;
            }
        }

        private void OnEdit(object sender, EventArgs e)
        {
            if (_lst.SelectedIndex < 0) return;
            int i = _lst.SelectedIndex;
            using (var f = new CardGroupEditForm(_deck, _groups[i]))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                if (_groups.Any(x => x != _groups[i] && string.Equals(x.Name, f.Result.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(this, "같은 이름의 카드군이 이미 있습니다.", "카드군");
                    return;
                }
                string oldName = _groups[i].Name;
                _groups[i].Name = f.Result.Name;
                _groups[i].Ids = f.Result.Ids;
                // 이름을 바꾸면 플랜의 참조도 따라가야 한다
                if (!string.Equals(oldName, _groups[i].Name, StringComparison.Ordinal))
                {
                    foreach (var p in Owner is PlanForm pf ? pf.Plans : new List<Plan>())
                        foreach (var s in p.Slots)
                            for (int k = 0; k < s.GroupNames.Count; k++)
                                if (string.Equals(s.GroupNames[k], oldName, StringComparison.OrdinalIgnoreCase))
                                    s.GroupNames[k] = _groups[i].Name;
                }
            }
            RefreshList();
        }

        private void OnDelete(object sender, EventArgs e)
        {
            if (_lst.SelectedIndex < 0) return;
            string name = _groups[_lst.SelectedIndex].Name;
            _groups.RemoveAt(_lst.SelectedIndex);
            RefreshList();
            if (Owner is PlanForm pf)
            {
                bool used = pf.Plans.Any(p => p.Slots.Any(s => s.GroupNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))));
                if (used) MessageBox.Show(this, "이 카드군을 쓰는 조건이 남아 있습니다(빈 카드군은 계산에서 무시됩니다).", "카드군");
            }
        }

        private void OnImportMainGroups(object sender, EventArgs e)
        {
            if (_deck == null) return;
            int added = 0;
            for (int g = 1; g <= 9; g++)
            {
                var cards = _deck.Main.Where(c => c.Group == g).ToList();
                if (cards.Count == 0) continue;
                string name = "그룹" + g;
                if (_groups.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                _groups.Add(new CardGroup { Name = name, Ids = cards.Select(c => c.Id).ToList() });
                added++;
            }
            RefreshList();
            MessageBox.Show(this, added == 0
                ? "메인 화면에 그룹 1~9 로 지정된 카드가 없습니다.\n(메인 화면 카드 표의 '그룹' 열에서 먼저 지정하세요)"
                : added + "개 카드군을 가져왔습니다.", "카드군");
        }

        /// <summary>검증용: 창을 띄우지 않고 PNG 로 저장.</summary>
        public void SaveShot(string path)
        {
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
    }

    /// <summary>카드군 하나 편집 — 이름 + 카드 체크.</summary>
    public sealed class CardGroupEditForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(30, 31, 34);
        private static readonly Color Fg = Color.FromArgb(230, 232, 235);
        private static readonly Color Sub = Color.FromArgb(150, 156, 165);
        private static readonly Color Box = Color.FromArgb(43, 45, 49);
        private static readonly Color Accent = Color.FromArgb(86, 156, 214);

        private readonly DeckInfo _deck;
        private TextBox _txtName, _txtFind;
        private CheckedListBox _clb;
        public CardGroup Result;

        public CardGroupEditForm(DeckInfo deck, CardGroup existing)
        {
            _deck = deck;
            Text = existing == null ? "카드군 추가" : "카드군 편집";
            ClientSize = new Size(440, 540);
            MinimumSize = new Size(380, 440);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Malgun Gothic", 9f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, BackColor = Bg, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // 이름
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // 검색
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 카드
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // 힌트
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // 버튼
            Controls.Add(root);

            root.Controls.Add(new Label { Text = "이름", Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtName = new TextBox { Dock = DockStyle.Fill, BackColor = Box, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 5, 0, 5) };
            if (existing != null) _txtName.Text = existing.Name;
            root.Controls.Add(_txtName, 1, 0);

            _txtFind = new TextBox { Dock = DockStyle.Fill, BackColor = Box, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle };
            _txtFind.TextChanged += delegate { FillList(); };
            root.SetColumnSpan(_txtFind, 2);
            root.Controls.Add(_txtFind, 0, 1);

            _clb = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 40, 44),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false
            };
            root.SetColumnSpan(_clb, 2);
            root.Controls.Add(_clb, 0, 2);

            var hint = new Label { Text = "이 카드군에 넣을 카드를 체크하세요(나중에 카드군을 고치면 플랜에도 자동 반영).", Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft };
            root.SetColumnSpan(hint, 2);
            root.Controls.Add(hint, 0, 3);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Bg, WrapContents = false };
            var ok = new Button { Text = "확인", Width = 96, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Color.White, Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold) };
            ok.FlatAppearance.BorderSize = 1;
            ok.FlatAppearance.BorderColor = Accent;
            ok.Click += OnOk;
            var cancel = new Button { Text = "취소", Width = 96, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(52, 55, 60), ForeColor = Fg, DialogResult = DialogResult.Cancel };
            cancel.FlatAppearance.BorderSize = 1;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(70, 74, 80);
            btns.Controls.Add(ok);
            btns.Controls.Add(cancel);
            root.SetColumnSpan(btns, 2);
            root.Controls.Add(btns, 0, 4);
            AcceptButton = ok;
            CancelButton = cancel;

            FillList();
            if (existing != null)
            {
                for (int i = 0; i < _clb.Items.Count; i++)
                {
                    var ce = _clb.Items[i] as CardEntry;
                    if (ce != null && existing.Ids.Contains(ce.Id)) _clb.SetItemChecked(i, true);
                }
            }
        }

        private void FillList()
        {
            string q = (_txtFind.Text ?? "").Trim();
            var checkedIds = new HashSet<int>();
            foreach (var o in _clb.CheckedItems) { var ce = o as CardEntry; if (ce != null) checkedIds.Add(ce.Id); }
            _clb.BeginUpdate();
            _clb.Items.Clear();
            foreach (var c in _deck.Main)
            {
                if (q.Length > 0 &&
                    c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    c.Id.ToString().IndexOf(q, StringComparison.Ordinal) < 0) continue;
                _clb.Items.Add(c);
            }
            for (int i = 0; i < _clb.Items.Count; i++)
            {
                var ce = _clb.Items[i] as CardEntry;
                if (ce != null && checkedIds.Contains(ce.Id)) _clb.SetItemChecked(i, true);
            }
            _clb.EndUpdate();
        }

        private void OnOk(object sender, EventArgs e)
        {
            string name = (_txtName.Text ?? "").Trim();
            if (name.Length == 0) { DialogResult = DialogResult.None; MessageBox.Show(this, "카드군 이름을 입력하세요.", "카드군"); return; }
            var ids = new List<int>();
            foreach (var o in _clb.CheckedItems) { var ce = o as CardEntry; if (ce != null) ids.Add(ce.Id); }
            if (ids.Count == 0) { DialogResult = DialogResult.None; MessageBox.Show(this, "카드를 1장 이상 체크하세요.", "카드군"); return; }
            Result = new CardGroup { Name = name, Ids = ids };
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>초동 플랜 목록 + 편집 + 확률 표시.</summary>
    public sealed class PlanForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(30, 31, 34);
        private static readonly Color Fg = Color.FromArgb(230, 232, 235);
        private static readonly Color Sub = Color.FromArgb(150, 156, 165);
        private static readonly Color Box = Color.FromArgb(43, 45, 49);
        private static readonly Color Accent = Color.FromArgb(86, 156, 214);
        private static readonly Color Border = Color.FromArgb(70, 74, 80);

        private readonly DeckInfo _deck;
        private int _hand;
        private List<Plan> _plans;
        private List<CardGroup> _groups;
        private List<int> _trapIds = new List<int>();
        private List<int> _brickIds = new List<int>();
        private PlanResult _last;

        private ListBox _lst;
        private TextBox _txtName, _txtResult;
        private ListView _lvSlots;
        private Label _lblHead;
        private int _sel = -1;
        private bool _busy;

        public List<Plan> Plans { get { return _plans; } }
        public List<CardGroup> Groups { get { return _groups; } }
        public PlanStore.Preset Preset
        {
            get
            {
                return new PlanStore.Preset
                {
                    Plans = _plans,
                    Groups = _groups,
                    HandTrapIds = _trapIds,
                    BrickIds = _brickIds
                };
            }
        }

        /// <summary>검증용(스크린샷·uicheck)에서 프리셋 파일을 건드리지 않게 한다.</summary>
        public bool NoSaveOnClose;

        public PlanForm(DeckInfo deck, int hand, PlanStore.Preset preset)
        {
            _deck = deck;
            _hand = hand;
            var loaded = (preset != null && (preset.Plans.Count > 0 || preset.Groups.Count > 0))
                ? preset
                : PlanStore.Load(deck != null ? deck.FilePath : null);
            _plans = loaded.Plans.Select(p => p.Clone()).ToList();
            _groups = loaded.Groups.Select(g => g.Clone()).ToList();
            _trapIds = new List<int>(loaded.HandTrapIds ?? new List<int>());
            _brickIds = new List<int>(loaded.BrickIds ?? new List<int>());
            if (_plans.Count == 0) _plans.Add(new Plan { Name = "플랜 1" });

            Text = "초동 플랜 — 여러 조건을 모두 만족하는 확률";
            ClientSize = new Size(900, 740);
            MinimumSize = new Size(760, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Malgun Gothic", 9f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Bg, Padding = new Padding(12, 8, 12, 10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(root);

            var head = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            head.Controls.Add(new Label
            {
                Text = "초동 플랜",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Malgun Gothic", 11.5f, FontStyle.Bold),
                ForeColor = Fg
            });
            head.Controls.Add(new Label
            {
                Text = "플랜 = 여러 조건(카드군 N장)을 모두 만족 · 플랜끼리는 '하나라도/둘 다'로 볼 수 있습니다 (같은 카드가 두 슬롯을 채우지는 못합니다)",
                Dock = DockStyle.Bottom,
                Height = 18,
                Font = new Font("Malgun Gothic", 8.25f),
                ForeColor = Sub
            });
            root.Controls.Add(head, 0, 0);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(body, 0, 1);

            // 좌: 플랜 목록
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Bg, Margin = new Padding(0, 0, 10, 0) };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            left.Controls.Add(new Label { Text = "플랜", Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _lst = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 40, 44),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            _lst.SelectedIndexChanged += OnSelectPlan;
            left.Controls.Add(_lst, 0, 1);
            var lb = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
            lb.Controls.Add(Small("+ 플랜", OnAddPlan));
            lb.Controls.Add(Small("복제", OnClonePlan));
            lb.Controls.Add(Small("삭제", OnDeletePlan));
            left.Controls.Add(lb, 0, 2);
            body.Controls.Add(left, 0, 0);

            // 우: 편집
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Bg };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var nameRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg };
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            nameRow.Controls.Add(new Label { Text = "이름", Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtName = new TextBox { Dock = DockStyle.Fill, BackColor = Box, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 4, 0, 4) };
            _txtName.TextChanged += OnNameChanged;
            nameRow.Controls.Add(_txtName, 1, 0);
            right.Controls.Add(nameRow, 0, 0);
            _lblHead = new Label { Text = "조건 (모두 만족해야 이 플랜)", Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft };
            right.Controls.Add(_lblHead, 0, 1);
            _lvSlots = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(38, 40, 44),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                MultiSelect = false,
                HideSelection = false
            };
            _lvSlots.Columns.Add("필요", 64);
            _lvSlots.Columns.Add("카드군", 372);
            _lvSlots.Columns.Add("덱 장수", 78);
            _lvSlots.DoubleClick += delegate { OnEditSlot(null, EventArgs.Empty); };
            right.Controls.Add(_lvSlots, 0, 2);
            var sb = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
            sb.Controls.Add(Small("+ 조건", OnAddSlot));
            sb.Controls.Add(Small("편집", OnEditSlot));
            sb.Controls.Add(Small("삭제", OnDeleteSlot));
            right.Controls.Add(sb, 0, 3);
            body.Controls.Add(right, 1, 0);

            _txtResult = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(24, 25, 28),
                ForeColor = Color.FromArgb(200, 210, 220),
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.FixedSingle,
                WordWrap = false
            };
            root.Controls.Add(_txtResult, 0, 2);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = Bg, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            var bCalc = Small("계산", delegate { Recompute(true); });
            bCalc.Width = 96;
            bCalc.BackColor = Accent;
            bCalc.ForeColor = Color.White;
            bCalc.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            var bGroups = Small("카드군…", OnEditGroups);
            bGroups.Width = 96;
            new ToolTip().SetToolTip(bGroups, "여러 카드를 이름으로 묶어 두고, 조건에서 그 이름을 고를 수 있습니다.\n(예: '시간의 흑마술사 테마' = 관련 카드 8종)");
            var bCopy = Small("결과 복사", OnCopy);
            bCopy.Width = 110;
            var bClose = Small("닫기", delegate { Close(); });
            bClose.Width = 96;
            bottom.Controls.Add(bCalc);
            bottom.Controls.Add(bGroups);
            bottom.Controls.Add(bCopy);
            bottom.Controls.Add(bClose);
            root.Controls.Add(bottom, 0, 3);
            AcceptButton = null;
            CancelButton = null;

            RefreshList();
            if (_lst.Items.Count > 0) _lst.SelectedIndex = 0;
            Recompute(false);
        }

        private Button Small(string text, EventHandler click)
        {
            var b = new Button
            {
                Text = text,
                Width = 84,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 55, 60),
                ForeColor = Fg,
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 66, 73);
            b.Click += click;
            return b;
        }

        // ── 목록/편집 ─────────────────────────────────────────
        private void RefreshList()
        {
            string keep = _sel >= 0 && _sel < _plans.Count ? _plans[_sel].Name : null;
            _lst.BeginUpdate();
            _lst.Items.Clear();
            for (int i = 0; i < _plans.Count; i++)
            {
                string mark = _plans[i].ValidIn(_groups) ? "" : "  (비어 있음)";
                _lst.Items.Add(string.Format("{0}. {1}{2}", i + 1, string.IsNullOrEmpty(_plans[i].Name) ? "(이름 없음)" : _plans[i].Name, mark));
            }
            _lst.EndUpdate();
            if (keep != null)
            {
                int idx = _plans.FindIndex(p => p.Name == keep);
                _sel = idx;
                if (idx >= 0) _lst.SelectedIndex = idx;
            }
        }

        private void OnSelectPlan(object sender, EventArgs e)
        {
            if (_lst.SelectedIndex < 0) return;
            _sel = _lst.SelectedIndex;
            _txtName.Text = _plans[_sel].Name;
            RefreshSlots();
        }

        private void OnNameChanged(object sender, EventArgs e)
        {
            if (_sel < 0 || _sel >= _plans.Count) return;
            _plans[_sel].Name = _txtName.Text;
            int idx = _sel;
            string text = string.Format("{0}. {1}", idx + 1, string.IsNullOrEmpty(_txtName.Text) ? "(이름 없음)" : _txtName.Text);
            if (idx < _lst.Items.Count) _lst.Items[idx] = text;
        }

        private void RefreshSlots()
        {
            _lvSlots.BeginUpdate();
            _lvSlots.Items.Clear();
            if (_sel >= 0 && _sel < _plans.Count)
            {
                foreach (var s in _plans[_sel].Slots)
                {
                    var resolved = s.ResolveIds(_groups);
                    int total = _deck == null ? 0 : _deck.Main.Where(c => resolved.Contains(c.Id)).Sum(c => c.Count);
                    var it = new ListViewItem(s.Valid ? s.Need + "장" : "?");
                    it.SubItems.Add(s.Describe(_deck, _groups));
                    it.SubItems.Add(total + "장");
                    it.Tag = s;
                    _lvSlots.Items.Add(it);
                }
            }
            _lvSlots.EndUpdate();
        }

        private void OnAddPlan(object sender, EventArgs e)
        {
            _plans.Add(new Plan { Name = "플랜 " + (_plans.Count + 1) });
            RefreshList();
            _lst.SelectedIndex = _plans.Count - 1;
            Recompute(false);
        }

        private void OnClonePlan(object sender, EventArgs e)
        {
            if (_sel < 0) return;
            var c = _plans[_sel].Clone();
            c.Name = c.Name + " 복사";
            _plans.Add(c);
            RefreshList();
            _lst.SelectedIndex = _plans.Count - 1;
            Recompute(false);
        }

        private void OnDeletePlan(object sender, EventArgs e)
        {
            if (_sel < 0 || _plans.Count <= 1) return;
            _plans.RemoveAt(_sel);
            _sel = Math.Min(_sel, _plans.Count - 1);
            RefreshList();
            if (_sel >= 0) _lst.SelectedIndex = _sel;
            Recompute(false);
        }

        private void OnAddSlot(object sender, EventArgs e)
        {
            if (_sel < 0 || _deck == null) return;
            using (var f = new SlotForm(_deck, _groups, null))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                _plans[_sel].Slots.Add(f.Result);
            }
            RefreshSlots();
            RefreshList();
            Recompute(false);
        }

        private void OnEditSlot(object sender, EventArgs e)
        {
            if (_sel < 0 || _deck == null || _lvSlots.SelectedItems.Count == 0) return;
            var slot = _lvSlots.SelectedItems[0].Tag as PlanSlot;
            if (slot == null) return;
            using (var f = new SlotForm(_deck, _groups, slot))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                slot.Need = f.Result.Need;
                slot.Ids = f.Result.Ids;
                slot.GroupNames = f.Result.GroupNames;
            }
            RefreshSlots();
            Recompute(false);
        }

        /// <summary>카드군 목록 편집.</summary>
        private void OnEditGroups(object sender, EventArgs e)
        {
            using (var f = new CardGroupForm(_deck, _groups))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                _groups.Clear();
                _groups.AddRange(f.Groups.Select(g => g.Clone()));
            }
            RefreshSlots();
            RefreshList();
            Recompute(true);
        }

        private void OnDeleteSlot(object sender, EventArgs e)
        {
            if (_sel < 0 || _lvSlots.SelectedItems.Count == 0) return;
            var slot = _lvSlots.SelectedItems[0].Tag as PlanSlot;
            if (slot == null) return;
            _plans[_sel].Slots.Remove(slot);
            RefreshSlots();
            RefreshList();
            Recompute(false);
        }

        // ── 계산/출력 ─────────────────────────────────────────
        private void Recompute(bool force)
        {
            if (_busy || _deck == null) return;
            _busy = true;
            try
            {
                var t0 = DateTime.UtcNow;
                _last = PlanOdds.Compute(_deck, _plans, _hand, _groups);
                double ms = (DateTime.UtcNow - t0).TotalMilliseconds;

                var sb = new StringBuilder();
                sb.AppendLine(string.Format("덱 {0}장 · 손패 {1}장 · 경우의 수 C({0},{1}) = {2:N0}   (계산 {3:N0} ms, 배분 {4:N0})   카드군 {5}개",
                    _last.DeckSize, _last.Hand, _last.CombTotal, ms, _last.Leaves, _groups.Count));
                sb.AppendLine();
                for (int i = 0; i < _plans.Count; i++)
                {
                    if (!_plans[i].ValidIn(_groups)) { sb.AppendLine(string.Format("  {0,-18} : (조건 없음)", NameOf(i))); continue; }
                    sb.AppendLine(string.Format("  {0,-18} : {1,8:P2}   [ {2} ]", NameOf(i), _last.PlanP[i], _plans[i].Describe(_deck, _groups)));
                }
                sb.AppendLine();
                sb.AppendLine(string.Format("  ▶ 하나라도(OR)      : {0,8:P2}", _last.AnyP));
                if (_last.Pairs.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("  둘 다(AND)");
                    foreach (var p in _last.Pairs)
                    {
                        string strict = p.DisjointSkipped ? "계산 생략(슬롯 과다)"
                            : (double.IsNaN(p.PDisjoint) ? "-" : string.Format("{0:P2}", p.PDisjoint));
                        sb.AppendLine(string.Format("    {0} ∧ {1}  겹침 허용 {2,7:P2} · 카드 분리 {3}",
                            NameOf(p.A), NameOf(p.B), p.P, strict));
                    }
                }
                if (!string.IsNullOrEmpty(_last.Note)) sb.AppendLine().AppendLine("  ⚠ " + _last.Note);

                // ── 요약 ───────────────────────────────────────
                sb.AppendLine();
                sb.AppendLine("══════════ 요약 ══════════");
                int best = -1; double bestP = -1;
                for (int i = 0; i < _plans.Count; i++)
                {
                    if (!_plans[i].ValidIn(_groups)) continue;
                    double v = _last.PlanP[i];
                    if (!double.IsNaN(v) && v > bestP) { bestP = v; best = i; }
                }
                if (best >= 0) sb.AppendLine(string.Format("  초동 최고      : {0}  {1:P2}", NameOf(best), bestP));
                else sb.AppendLine("  초동           : (조건 없음)");
                if (_plans.Count(p => p.ValidIn(_groups)) > 1)
                    sb.AppendLine(string.Format("  초동 하나라도  : {0:P2}", _last.AnyP));
                var topPair = _last.Pairs.Where(p => !double.IsNaN(p.P)).OrderByDescending(p => p.P).FirstOrDefault();
                if (topPair != null)
                {
                    string dis = double.IsNaN(topPair.PDisjoint) ? "생략" : topPair.PDisjoint.ToString("P2");
                    sb.AppendLine(string.Format("  초동 둘 다 최고: {0} ∧ {1}  {2:P2} (카드 분리 {3})",
                        NameOf(topPair.A), NameOf(topPair.B), topPair.P, dis));
                }
                sb.AppendLine(string.Format("  손패 {0}장 · 덱 {1}장 · 카드군 {2}개 · 패트랩 {3}종 · 곤란 {4}종",
                    _last.Hand, _last.DeckSize, _groups.Count, _trapIds.Count, _brickIds.Count));
                _txtResult.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                _txtResult.Text = "계산 오류: " + ex.Message;
            }
            finally { _busy = false; }
        }

        /// <summary>검증용: 플랜이 비어 있으면 데모 플랜을 만들어 화면을 채운다.</summary>
        public void DemoForTest()
        {
            if (_deck == null || _deck.Main.Count == 0) return;
            if (_plans.Count == 1 && _plans[0].Slots.Count == 0)
            {
                // 카드군 데모(테마 묶음) + 그것을 참조하는 조건
                var theme = new CardGroup { Name = "테마 카드군" };
                for (int i = 0; i < Math.Min(4, _deck.Main.Count); i++) theme.Ids.Add(_deck.Main[i].Id);
                _groups.Add(theme);

                var p = _plans[0];
                p.Name = "데모 초동";
                p.Slots.Add(new PlanSlot { Need = 1, Ids = new List<int> { _deck.Main[0].Id } });
                p.Slots.Add(new PlanSlot { Need = 1, GroupNames = new List<string> { theme.Name } });
                _plans.Add(new Plan
                {
                    Name = "데모 플랜 2",
                    Slots = new List<PlanSlot> { new PlanSlot { Need = 2, GroupNames = new List<string> { theme.Name } } }
                });
            }
            RefreshList();
            if (_lst.Items.Count > 0) _lst.SelectedIndex = 0;
            RefreshSlots();
            Recompute(true);
        }

        /// <summary>검증용: 창을 띄우지 않고 PNG 로 저장.</summary>
        public void SaveScreenshot(string path)
        {
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

        private string NameOf(int i)        {
            if (i < 0 || i >= _plans.Count) return "?";
            return string.IsNullOrEmpty(_plans[i].Name) ? ("플랜 " + (i + 1)) : _plans[i].Name;
        }

        private void OnCopy(object sender, EventArgs e)
        {
            try { Clipboard.SetText(_txtResult.Text); }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!NoSaveOnClose)
            {
                try { PlanStore.Save(_deck != null ? _deck.FilePath : null, Preset); } catch { }
            }
            base.OnFormClosing(e);
        }
    }
}
