// 덱 확률 분석 — 한 화면에서 카테고리별 확률을 **모두** 보여주고 마지막에 요약을 붙인다.
//   ① 패트랩(사용자 지정)  ② 곤란한 카드(사용자 지정)  ③ 플랜이 쓰는 카드  ④ 미지정(나머지)
//   + 카드별 확률 · 초동(플랜) 전체 · 초동+패트랩/곤란 조합 · 맨 아래 요약
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpeningCalc
{
    public sealed class DeckStatsForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(30, 31, 34);
        private static readonly Color Fg = Color.FromArgb(230, 232, 235);
        private static readonly Color Sub = Color.FromArgb(150, 156, 165);
        private static readonly Color Accent = Color.FromArgb(86, 156, 214);
        private static readonly Color Border = Color.FromArgb(70, 74, 80);

        private readonly DeckInfo _deck;
        private readonly int _hand;
        private readonly IList<Plan> _plans;
        private readonly IList<CardGroup> _groups;
        private List<int> _trapIds;
        private List<int> _brickIds;

        private Label _lblTrap, _lblBrick;
        private TextBox _out;

        public List<int> TrapIds { get { return _trapIds; } }
        public List<int> BrickIds { get { return _brickIds; } }
        /// <summary>화면에 표시된 결과 텍스트(검증·복사용).</summary>
        public string OutputText { get { return _out == null ? "" : _out.Text; } }

        public DeckStatsForm(DeckInfo deck, int hand, IList<Plan> plans, IList<CardGroup> groups,
                             IEnumerable<int> trapIds, IEnumerable<int> brickIds)
        {
            _deck = deck;
            _hand = hand;
            _plans = plans ?? new List<Plan>();
            _groups = groups ?? new List<CardGroup>();
            _trapIds = trapIds == null ? new List<int>() : trapIds.Distinct().ToList();
            _brickIds = brickIds == null ? new List<int>() : brickIds.Distinct().ToList();

            Text = "확률 분석 — 패트랩 · 곤란 카드 · 미지정 (전체 결과 + 요약)";
            ClientSize = new Size(900, 760);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Malgun Gothic", 9f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Bg, Padding = new Padding(12, 8, 12, 10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // 패트랩
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // 곤란
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(root);

            var head = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            head.Controls.Add(new Label
            {
                Text = "확률 분석",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Malgun Gothic", 11.5f, FontStyle.Bold),
                ForeColor = Fg
            });
            head.Controls.Add(new Label
            {
                Text = "패트랩 · 곤란한 카드 · 미지정 · 카드별 · 초동 전체 · 조합 — 맨 아래에 요약",
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = new Font("Malgun Gothic", 8.25f),
                ForeColor = Sub
            });
            root.Controls.Add(head, 0, 0);

            _lblTrap = new Label { Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(Row(_lblTrap, "패트랩 고르기…", OnPickTrap), 0, 1);
            _lblBrick = new Label { Dock = DockStyle.Fill, ForeColor = Sub, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(Row(_lblBrick, "곤란 카드 고르기…", OnPickBrick), 0, 2);

            _out = new TextBox
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
            root.Controls.Add(_out, 0, 3);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            var bCalc = Small("새로 계산", delegate { Recalc(); });
            bCalc.Width = 104;
            var bCopy = Small("결과 복사", OnCopy);
            bCopy.Width = 104;
            var bClose = Small("닫기", delegate { Close(); });
            bClose.Width = 96;
            bottom.Controls.Add(bCalc);
            bottom.Controls.Add(bCopy);
            bottom.Controls.Add(bClose);
            root.Controls.Add(bottom, 0, 4);

            Recalc();
        }

        private Control Row(Label label, string buttonText, EventHandler onClick)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172));
            row.Controls.Add(label, 0, 0);
            var b = Small(buttonText, onClick);
            b.Width = 166;
            b.Height = 30;
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold);
            row.Controls.Add(b, 1, 0);
            return row;
        }

        private Button Small(string text, EventHandler click)
        {
            var b = new Button
            {
                Text = text,
                Width = 96,
                Height = 28,
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

        private void OnPickTrap(object sender, EventArgs e)
        {
            var ids = PickCards("패트랩", _trapIds);
            if (ids == null) return;
            _trapIds = ids;
            Recalc();
        }

        private void OnPickBrick(object sender, EventArgs e)
        {
            var ids = PickCards("곤란한 카드", _brickIds);
            if (ids == null) return;
            _brickIds = ids;
            Recalc();
        }

        /// <summary>카드 고르기 — 카드군 편집 창을 재사용(이름은 저장하지 않음).</summary>
        private List<int> PickCards(string name, List<int> current)
        {
            if (_deck == null) return null;
            var seed = new CardGroup { Name = name, Ids = current.ToList() };
            using (var f = new CardGroupEditForm(_deck, seed))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return null;
                return f.Result.Ids.Distinct().ToList();
            }
        }

        // ── 계산 ─────────────────────────────────────────────
        private void Recalc()
        {
            var sb = new StringBuilder();
            if (_deck == null) { _out.Text = "덱을 먼저 여세요."; return; }
            bestComboValue = -1;
            int N = _deck.Size, h = _hand;

            var trapSet = new HashSet<int>(_trapIds);
            var brickSet = new HashSet<int>(_brickIds);

            // 플랜이 쓰는 카드(카드군 해석 포함)
            var planSet = new HashSet<int>();
            foreach (var p in _plans)
            {
                if (p == null || !p.ValidIn(_groups)) continue;
                foreach (var s in p.Slots)
                    if (s != null && s.Valid) foreach (int id in s.ResolveIds(_groups)) planSet.Add(id);
            }

            var assigned = new HashSet<int>(trapSet);
            assigned.UnionWith(brickSet);
            assigned.UnionWith(planSet);
            var restIds = _deck.Main.Where(c => !assigned.Contains(c.Id)).Select(c => c.Id).ToList();

            Func<IEnumerable<int>, int> cardsOf = ids => _deck.Main.Where(c => ids.Contains(c.Id)).Sum(c => c.Count);
            int cTrap = cardsOf(trapSet), cBrick = cardsOf(brickSet), cRest = cardsOf(restIds), cPlan = cardsOf(planSet);
            int cAssigned = N - cRest;

            _lblTrap.Text = _trapIds.Count == 0 ? "패트랩 미지정" : string.Format("패트랩 {0}장 ({1}종)", cTrap, _trapIds.Count);
            _lblBrick.Text = _brickIds.Count == 0 ? "곤란 카드 미지정" : string.Format("곤란 카드 {0}장 ({1}종)", cBrick, _brickIds.Count);

            sb.AppendLine(string.Format("덱 {0}장 · 손패 {1}장 · 지정 {2}장 / 미지정 {3}장", N, h, cAssigned, cRest));
            sb.AppendLine();

            // ① 카테고리 요약
            sb.AppendLine("── ① 카테고리별 (초기하분포, 정확값) ───────────────────────────────────");
            Category(sb, "패트랩", cTrap, N, h);
            Category(sb, "곤란 카드", cBrick, N, h);
            Category(sb, "미지정(나머지)", cRest, N, h);
            sb.AppendLine(string.Format("  · 플랜이 쓰는 카드 {0}장 (겹침 포함)", cPlan));
            int overlap = cTrap + cBrick + cPlan - cAssigned;
            if (overlap > 0)
                sb.AppendLine(string.Format("  · 패트랩/곤란/플랜이 서로 {0}장 겹칩니다 — 미지정 계산에서는 한 번만 셉니다", overlap));
            sb.AppendLine();

            // ② 분포
            sb.AppendLine("── ② 장수별 분포 (k장 잡힐 확률) ─────────────────────────────────────");
            Distribution(sb, "패트랩", cTrap, N, h);
            Distribution(sb, "곤란 카드", cBrick, N, h);
            Distribution(sb, "미지정", cRest, N, h);
            sb.AppendLine();

            // ③ 카드별
            sb.AppendLine("── ③ 카드별 (≥1장 잡힐 확률 · =1 · =2) ────────────────────────────────");
            CardList(sb, "패트랩", trapSet);
            CardList(sb, "곤란 카드", brickSet);
            CardList(sb, "미지정", new HashSet<int>(restIds));
            sb.AppendLine();

            // ④ 초동(플랜) 전체
            var valid = new List<int>();
            for (int i = 0; i < _plans.Count; i++) if (_plans[i].ValidIn(_groups)) valid.Add(i);

            PlanResult planRes = null;
            if (valid.Count > 0)
            {
                planRes = PlanOdds.Compute(_deck, _plans, h, _groups);
                sb.AppendLine("── ④ 초동(플랜) 전체 ────────────────────────────────────────────────");
                for (int i = 0; i < _plans.Count; i++)
                {
                    if (!_plans[i].ValidIn(_groups)) { sb.AppendLine(string.Format("  · {0} : (조건 없음)", planName(i))); continue; }
                    sb.AppendLine(string.Format("  · {0} : {1:P2}   [ {2} ]",
                        planName(i), double.IsNaN(planRes.PlanP[i]) ? 0 : planRes.PlanP[i], _plans[i].Describe(_deck, _groups)));
                }
                if (valid.Count > 1)
                    sb.AppendLine(string.Format("  · 하나라도(OR) : {0:P2}", planRes.AnyP));
                foreach (var p in planRes.Pairs)
                {
                    if (p.DisjointSkipped) continue;
                    sb.AppendLine(string.Format("  · {0} ∧ {1} : 겹침 허용 {2:P2} / 카드 분리 {3}",
                        planName(p.A), planName(p.B), p.P,
                        double.IsNaN(p.PDisjoint) ? "-" : p.PDisjoint.ToString("P2")));
                }
                if (!string.IsNullOrEmpty(planRes.Note)) sb.AppendLine("  ⚠ " + planRes.Note);
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("── ④ 초동(플랜) ──────────────────────────────────────────────────");
                sb.AppendLine("  (플랜 없음 — [초동 플랜…] 에서 만들 수 있습니다)");
                sb.AppendLine();
            }

            // ⑤ 초동 + 패트랩/곤란
            var comboRows = new List<string>();
            string bestCombo = null;
            if (valid.Count > 0 && (cTrap > 0 || cBrick > 0))
            {
                sb.AppendLine("── ⑤ 초동 + 패트랩/곤란 (같은 손패에서 함께) ────────────────────────");
                sb.AppendLine("  (트랩≥1 = 패트랩 1장 이상 · 곤란0 = 곤란 카드 0장 · 둘 다 = 트랩≥1 이면서 곤란0)");

                var queries = new List<Plan>();
                var layout = new List<int[]>();
                foreach (int i in valid)
                {
                    var plan = _plans[i];
                    int baseSlots = plan.Slots.Count(s => s != null && s.Valid);
                    bool fits = baseSlots + 1 <= PlanOdds.MaxQuerySlots;
                    int q0 = queries.Count; queries.Add(plan);
                    int q1 = -1, q2 = -1, q3 = -1;
                    if (cTrap > 0 && fits)
                    {
                        var c1 = new Plan { Name = plan.Name + "+트랩" };
                        foreach (var s in plan.Slots) c1.Slots.Add(s);
                        c1.Slots.Add(new PlanSlot { Need = 1, Ids = _trapIds.ToList() });
                        q1 = queries.Count; queries.Add(c1);

                        if (cBrick > 0 && baseSlots + 2 <= PlanOdds.MaxQuerySlots)
                        {
                            var c3 = new Plan { Name = plan.Name + "+트랩+브릭" };
                            foreach (var s in plan.Slots) c3.Slots.Add(s);
                            c3.Slots.Add(new PlanSlot { Need = 1, Ids = _trapIds.ToList() });
                            c3.Slots.Add(new PlanSlot { Need = 1, Ids = _brickIds.ToList() });
                            q3 = queries.Count; queries.Add(c3);
                        }
                    }
                    if (cBrick > 0 && fits)
                    {
                        var c2 = new Plan { Name = plan.Name + "+브릭" };
                        foreach (var s in plan.Slots) c2.Slots.Add(s);
                        c2.Slots.Add(new PlanSlot { Need = 1, Ids = _brickIds.ToList() });
                        q2 = queries.Count; queries.Add(c2);
                    }
                    layout.Add(new[] { q0, q1, q2, q3 });
                }

                var t0 = DateTime.UtcNow;
                var res = PlanOdds.Compute(_deck, queries, h, _groups, withPairs: false, withAny: false);
                double ms = (DateTime.UtcNow - t0).TotalMilliseconds;

                for (int k = 0; k < valid.Count; k++)
                {
                    var L = layout[k];
                    double p0 = res.PlanP[L[0]];
                    var parts = new List<string> { string.Format("플랜만 {0:P2}", double.IsNaN(p0) ? 0 : p0) };
                    if (L[1] >= 0 && !double.IsNaN(res.PlanP[L[1]]))
                    {
                        double v = res.PlanP[L[1]];
                        parts.Add(string.Format("+트랩≥1 {0:P2}", v));
                        if (bestCombo == null || v > bestComboValue)
                        {
                            bestComboValue = v;
                            bestCombo = string.Format("{0} + 패트랩≥1", planName(valid[k]));
                        }
                    }
                    if (L[2] >= 0 && !double.IsNaN(res.PlanP[L[2]]))
                        parts.Add(string.Format("+곤란0 {0:P2}", p0 - res.PlanP[L[2]]));
                    if (L[3] >= 0 && L[1] >= 0 && !double.IsNaN(res.PlanP[L[3]]))
                        parts.Add(string.Format("둘 다 {0:P2}", res.PlanP[L[1]] - res.PlanP[L[3]]));
                    sb.AppendLine(string.Format("  · {0} : {1}", Clip(planName(valid[k]), 22), string.Join(" | ", parts)));
                    comboRows.Add(planName(valid[k]));
                }
                sb.AppendLine();
                sb.AppendLine(string.Format("  (카드 분리 기준 · 계산 {0:N0} ms, 배분 {1:N0})", ms, res.Leaves));
                if (!string.IsNullOrEmpty(res.Note)) sb.AppendLine("  ⚠ " + res.Note);
                sb.AppendLine();
            }
            else if (valid.Count > 0)
            {
                sb.AppendLine("── ⑤ 초동 + 패트랩/곤란 ────────────────────────────────────────────");
                sb.AppendLine("  (패트랩·곤란 카드를 지정하면 초동과의 조합 확률도 나옵니다)");
                sb.AppendLine();
            }

            // ⑥ 요약
            sb.AppendLine("══════════ 요약 ══════════");
            if (planRes != null && valid.Count > 0)
            {
                int best = -1; double bestP = -1;
                for (int i = 0; i < _plans.Count; i++)
                {
                    if (!_plans[i].ValidIn(_groups)) continue;
                    double v = planRes.PlanP[i];
                    if (!double.IsNaN(v) && v > bestP) { bestP = v; best = i; }
                }
                if (best >= 0)
                    sb.AppendLine(string.Format("  초동 최고      : {0}  {1:P2}", planName(best), bestP));
                if (valid.Count > 1)
                    sb.AppendLine(string.Format("  초동 하나라도  : {0:P2}", planRes.AnyP));
                var topPair = planRes.Pairs.Where(p => !double.IsNaN(p.P)).OrderByDescending(p => p.P).FirstOrDefault();
                if (topPair != null)
                    sb.AppendLine(string.Format("  초동 둘 다 최고: {0} ∧ {1}  {2:P2}",
                        planName(topPair.A), planName(topPair.B), topPair.P));
            }
            else sb.AppendLine("  초동           : (플랜 없음)");

            sb.AppendLine(string.Format("  패트랩         : {0}장 → ≥1 {1:P2} · ≥2 {2:P2}",
                cTrap, cTrap > 0 ? Odds.AtLeast(N, cTrap, h, 1) : 0, cTrap > 0 ? Odds.AtLeast(N, cTrap, h, 2) : 0));
            sb.AppendLine(string.Format("  곤란 카드      : {0}장 → 0장일 확률 {1:P2}",
                cBrick, cBrick > 0 ? Odds.Exactly(N, cBrick, h, 0) : 1.0));
            sb.AppendLine(string.Format("  미지정         : {0}장 → 0장일 확률 {1:P2}  (손패가 전부 지정 카드)",
                cRest, cRest > 0 ? Odds.Exactly(N, cRest, h, 0) : 1.0));
            if (bestCombo != null)
                sb.AppendLine(string.Format("  최고 조합      : {0}  {1:P2}", bestCombo, bestComboValue));
            sb.AppendLine(string.Format("  기대 장수      : 패트랩 {0:F2} + 곤란 {1:F2} + 미지정 {2:F2} = {3:F2}장",
                Odds.Expected(N, cTrap, h), Odds.Expected(N, cBrick, h), Odds.Expected(N, cRest, h),
                (cTrap + cBrick + cRest) > 0 ? Odds.Expected(N, cTrap + cBrick + cRest, h) : 0));

            _out.Text = sb.ToString();
        }

        private double bestComboValue = -1;

        private string planName(int i)
        {
            return string.IsNullOrEmpty(_plans[i].Name) ? ("플랜 " + (i + 1)) : _plans[i].Name;
        }

        /// <summary>카테고리 안 카드별 ≥1 / =1 / =2 확률.</summary>
        private void CardList(StringBuilder sb, string label, HashSet<int> set)
        {
            if (_deck == null || set.Count == 0) { sb.AppendLine(string.Format("  · {0} : 없음", label)); return; }
            sb.AppendLine(string.Format("  [{0}]", label));
            int N = _deck.Size, h = _hand;
            foreach (var c in _deck.Main.Where(x => set.Contains(x.Id)).OrderByDescending(x => x.Count).ThenBy(x => x.Name))
            {
                sb.AppendLine(string.Format("    - {0} x{1} : ≥1 {2:P2} | =1 {3:P2} | =2 {4:P2}",
                    c.Name, c.Count, Odds.AtLeast(N, c.Count, h, 1), Odds.Exactly(N, c.Count, h, 1), Odds.Exactly(N, c.Count, h, 2)));
            }
        }

        private static void Category(StringBuilder sb, string name, int c, int N, int h)
        {
            if (c <= 0)
            {
                sb.AppendLine(string.Format("  · {0} : 없음", name));
                return;
            }
            sb.AppendLine(string.Format("  · {0} : {1}장   0장 {2:P2} | ≥1 {3:P2} | ≥2 {4:P2} | ≥3 {5:P2} | 기대 {6:F2}장",
                name, c, Odds.Exactly(N, c, h, 0), Odds.AtLeast(N, c, h, 1), Odds.AtLeast(N, c, h, 2),
                Odds.AtLeast(N, c, h, 3), Odds.Expected(N, c, h)));
        }

        private static void Distribution(StringBuilder sb, string name, int c, int N, int h)
        {
            if (c <= 0) { sb.AppendLine(string.Format("  · {0} : 없음", name)); return; }
            int max = Math.Min(c, h);
            var parts = new List<string>();
            for (int k = 0; k <= max; k++) parts.Add(string.Format("{0}장 {1:P1}", k, Odds.Exactly(N, c, h, k)));
            sb.AppendLine(string.Format("  · {0} : {1}", name, string.Join(" | ", parts)));
        }

        private static string Clip(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "(이름 없음)";
            return s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        }

        private void OnCopy(object sender, EventArgs e)
        {
            try { Clipboard.SetText(_out.Text); } catch { }
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

        /// <summary>검증용: 카드 지정.</summary>
        public void SetCards(IEnumerable<int> traps, IEnumerable<int> bricks)
        {
            if (traps != null) _trapIds = traps.Distinct().ToList();
            if (bricks != null) _brickIds = bricks.Distinct().ToList();
            Recalc();
        }
    }
}
