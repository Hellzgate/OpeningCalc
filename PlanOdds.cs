// 초동 "플랜" 확률 — 여러 슬롯(카드군 N장)을 모두 만족할 확률을 정확히 계산한다.
//
// 모델은 참고 앱(D:\edopro\초동확률 계산기)과 같다: 카드군 + "N장" 슬롯 + AND/OR.
// 그 앱의 계산도 **정확**하며(겹치는 카드군 30건 대조 → 오차 0), 이 파일은 같은 값을
// 더 빠르게 내고 검증·자동화가 가능하도록 다시 구현한 것이다.
//
//   · 계산 방식: 덱 카드를 "어느 슬롯을 채울 수 있는가"(비트마스크)로 분류해 같은 마스크끼리 묶고,
//     손패 장수를 각 묶음에 배분하는 모든 경우(다변량 초기하)를 열거한다. 각 배분에서
//     Hall 정리(부분집합 T 마다  Σneed ≤ 그 슬롯들을 채울 수 있는 장수)로 만족 여부를 판정한다.
//     시뮬레이션이 아니라 완전 열거이므로 오차는 부동소수점 반올림뿐이다.
//   · 같은 카드가 두 슬롯을 동시에 채우지는 못한다 — Hall 조건이 이를 강제한다.
//   · 두 플랜의 AND 를 **두 가지로** 보여준다.
//       겹침 허용 — 두 조건이 같은 카드로 동시에 성립해도 인정(참고 앱과 같은 의미)
//       카드 분리 — 실제로 두 콜보를 모두 실행하려면 카드가 따로 필요(더 엄격, 이 앱의 추가 기능)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpeningCalc
{
    /// <summary>사용자가 직접 묶는 "카드군" — 이름 + 카드ID 목록. 플랜 슬롯에서 이름으로 참조한다.</summary>
    public sealed class CardGroup
    {
        public string Name = "";
        public List<int> Ids = new List<int>();

        public CardGroup Clone()
        {
            var g = new CardGroup { Name = Name };
            if (Ids != null) g.Ids.AddRange(Ids);
            return g;
        }

        /// <summary>"와이반의 용기사, 로켓 전사 외 3종 (5종 9장)" 형태.</summary>
        public string Describe(DeckInfo deck)
        {
            if (Ids == null || Ids.Count == 0) return "(빈 카드군)";
            string names = string.Join(", ", Ids.Take(2).Select(id => PlanSlot.NameOf(deck, id)));
            if (Ids.Count > 2) names += " 외 " + (Ids.Count - 2) + "종";
            int cards = 0;
            if (deck != null) cards = deck.Main.Where(c => Ids.Contains(c.Id)).Sum(c => c.Count);
            return string.Format("{0} ({1}종 {2}장)", names, Ids.Count, cards);
        }
    }

    /// <summary>플랜의 한 슬롯 — 카드군에서 Need 장을 뽑는다(같은 카드 여러 장 허용).</summary>
    public sealed class PlanSlot
    {
        public List<int> Ids = new List<int>();
        /// <summary>참조하는 카드군 이름들(카드군이 바뀌면 자동 반영).</summary>
        public List<string> GroupNames = new List<string>();
        public int Need = 1;

        public PlanSlot Clone()
        {
            var s = new PlanSlot { Need = Need };
            if (Ids != null) s.Ids.AddRange(Ids);
            if (GroupNames != null) s.GroupNames.AddRange(GroupNames);
            return s;
        }

        /// <summary>이 슬롯이 실제로 쓸 수 있는 카드 ID — 직접 고른 카드 + 참조 카드군의 카드.</summary>
        public List<int> ResolveIds(IList<CardGroup> groups)
        {
            var all = new List<int>();
            if (Ids != null) all.AddRange(Ids);
            if (GroupNames != null && groups != null)
            {
                foreach (string gn in GroupNames)
                {
                    var g = groups.FirstOrDefault(x => string.Equals(x.Name, gn, StringComparison.OrdinalIgnoreCase));
                    if (g != null && g.Ids != null) all.AddRange(g.Ids);
                }
            }
            return all.Distinct().ToList();
        }

        /// <summary>형식상 채워져 있는가(카드 또는 카드군 중 하나라도 지정).</summary>
        public bool Valid
        {
            get
            {
                if (Need <= 0) return false;
                return (Ids != null && Ids.Count > 0) || (GroupNames != null && GroupNames.Count > 0);
            }
        }

        /// <summary>실제 해석 결과까지 확인(카드군이 비었으면 false).</summary>
        public bool ValidIn(IList<CardGroup> groups)
        {
            return Valid && ResolveIds(groups).Count > 0;
        }

        public string Describe(DeckInfo deck) { return Describe(deck, null); }

        public string Describe(DeckInfo deck, IList<CardGroup> groups)
        {
            var parts = new List<string>();
            var used = new HashSet<int>();

            if (GroupNames != null)
            {
                foreach (string gn in GroupNames)
                {
                    var g = groups == null ? null : groups.FirstOrDefault(x => string.Equals(x.Name, gn, StringComparison.OrdinalIgnoreCase));
                    int n = g == null || g.Ids == null ? 0 : g.Ids.Count;
                    parts.Add(string.Format("{0}({1}종)", gn, n));
                    if (g != null && g.Ids != null) foreach (int id in g.Ids) used.Add(id);
                }
            }
            var extra = (Ids ?? new List<int>()).Where(id => !used.Contains(id)).ToList();
            if (extra.Count > 0)
            {
                string names = string.Join(", ", extra.Take(3).Select(id => NameOf(deck, id)));
                if (extra.Count > 3) names += " 외 " + (extra.Count - 3) + "종";
                parts.Add(names);
            }
            if (parts.Count == 0) parts.Add("(비어 있음)");
            return string.Join(" + ", parts) + " → " + Need + "장";
        }

        public static string NameOf(DeckInfo deck, int id)
        {
            if (deck != null)
            {
                var c = deck.Main.FirstOrDefault(x => x.Id == id);
                if (c != null && !string.IsNullOrEmpty(c.Name)) return c.Name;
            }
            return "#" + id;
        }
    }

    /// <summary>초동 하나 — 슬롯들을 **모두** 만족해야 성립한다(AND).</summary>
    public sealed class Plan
    {
        public string Name = "";
        public List<PlanSlot> Slots = new List<PlanSlot>();

        public bool Valid { get { return Slots.Any(s => s != null && s.Valid); } }

        /// <summary>카드군까지 해석해 실제로 계산 가능한가.</summary>
        public bool ValidIn(IList<CardGroup> groups)
        {
            return Slots.Any(s => s != null && s.ValidIn(groups));
        }

        public string Describe(DeckInfo deck) { return Describe(deck, null); }

        public string Describe(DeckInfo deck, IList<CardGroup> groups)
        {
            var parts = Slots.Where(s => s != null && s.Valid).Select(s => s.Describe(deck, groups)).ToList();
            return parts.Count == 0 ? "(빈 플랜)" : string.Join(" + ", parts);
        }

        public Plan Clone()
        {
            var p = new Plan { Name = Name };
            foreach (var s in Slots) p.Slots.Add(s.Clone());
            return p;
        }
    }

    public sealed class PlanPair
    {
        public int A, B;
        /// <summary>겹침 허용 AND — 두 조건이 같은 카드로 동시에 성립해도 인정(참고 앱과 동일).</summary>
        public double P;
        /// <summary>카드 분리 AND — 서로 다른 카드로 두 콤보를 모두 실행할 수 있는 확률(더 엄격).</summary>
        public double PDisjoint = double.NaN;
        public bool DisjointSkipped;
    }

    public sealed class PlanResult
    {
        public int DeckSize, Hand;
        public List<double> PlanP = new List<double>();
        public double AnyP;
        public List<PlanPair> Pairs = new List<PlanPair>();
        public string Note = "";
        public long Leaves;       // 열거한 배분 수(진단용)
        public bool Truncated;
        public double CombTotal;  // C(덱, 손패)
    }

    public static class PlanOdds
    {
        /// <summary>열거 상한 — 넘으면 중단하고 Truncated 로 알린다(수 초 폭주 방지).</summary>
        public static long MaxLeaves = 8_000_000;

        /// <summary>한 질의가 가질 수 있는 슬롯 수 상한 — 부분집합 비트마스크(long) 때문에 6.</summary>
        public const int MaxQuerySlots = 6;

        private sealed class Query
        {
            public int Kind;        // 0 = 플랜, 1 = 카드 분리 AND(합집합 슬롯), 2 = 겹침 허용 AND
            public int[] Slots;     // kind 0/1
            public int PlanA, PlanB; // kind 2
        }

        public static PlanResult Compute(DeckInfo deck, IList<Plan> plans, int hand, IList<CardGroup> groups = null, bool withPairs = true, bool withAny = true)
        {
            var res = new PlanResult { DeckSize = deck != null ? deck.Size : 0, Hand = hand };
            if (deck == null || plans == null || plans.Count == 0 || hand <= 0) return res;

            // 슬롯을 전역 인덱스로 펼친다(카드군 참조는 여기서 실제 카드로 해석).
            var slotIds = new List<int[]>();
            var slotNeed = new List<int>();
            var planSlots = new List<int[]>();
            foreach (var p in plans)
            {
                var idx = new List<int>();
                foreach (var s in (p.Slots ?? new List<PlanSlot>()))
                {
                    if (s == null || !s.Valid) continue;
                    var ids = s.ResolveIds(groups);
                    if (ids.Count == 0) continue;          // 빈 카드군 → 이 슬롯은 무시
                    idx.Add(slotIds.Count);
                    slotIds.Add(ids.ToArray());
                    slotNeed.Add(s.Need);
                }
                planSlots.Add(idx.ToArray());
            }

            // 슬롯이 6개를 넘는 플랜은 부분집합 판정(비트마스크)이 불가 → 알린다.
            for (int i = 0; i < planSlots.Count; i++)
                if (planSlots[i].Length > MaxQuerySlots)
                    res.Note = string.Format("슬롯이 {0}개를 넘는 플랜은 계산하지 않습니다(조건을 줄여주세요).", MaxQuerySlots);

            // 질의 구성
            var queries = new List<Query>();
            var planQuery = new int[plans.Count];
            for (int i = 0; i < plans.Count; i++)
            {
                planQuery[i] = queries.Count;
                queries.Add(new Query { Kind = 0, Slots = planSlots[i], PlanA = i });
            }
            var pairList = new List<PlanPair>();
            var pairAndQuery = new List<int>();     // 질의 인덱스
            var pairDisQuery = new List<int>();
            if (withPairs)
                for (int i = 0; i < plans.Count; i++)
                    for (int j = i + 1; j < plans.Count; j++)
                    {
                        var pair = new PlanPair { A = i, B = j };
                        pairList.Add(pair);
                        pairAndQuery.Add(queries.Count);
                        queries.Add(new Query { Kind = 2, PlanA = i, PlanB = j });
                        int n = planSlots[i].Length + planSlots[j].Length;
                        if (n > MaxQuerySlots)
                        {
                            pair.DisjointSkipped = true;
                            pairDisQuery.Add(-1);
                            continue;
                        }
                        pairDisQuery.Add(queries.Count);
                        queries.Add(new Query { Kind = 1, Slots = planSlots[i].Concat(planSlots[j]).ToArray() });
                    }

            double[] probs = Solve(deck, hand, slotIds, slotNeed, queries, plans.Count, ref res);

            var planP = new List<double>();
            for (int i = 0; i < plans.Count; i++)
                planP.Add(planSlots[i].Length == 0 ? 0 : probs[planQuery[i]]);
            res.PlanP = planP;

            for (int k = 0; k < pairList.Count; k++)
            {
                var pair = pairList[k];
                pair.P = probs[pairAndQuery[k]];
                if (pairDisQuery[k] >= 0) pair.PDisjoint = probs[pairDisQuery[k]];
            }
            res.Pairs.AddRange(pairList);

            // "하나라도" (OR)
            if (!withAny) res.AnyP = double.NaN;
            else if (plans.Count == 1) res.AnyP = planP[0];
            else
            {
                var used = new List<int>();
                for (int i = 0; i < plans.Count; i++) used.AddRange(planSlots[i]);
                if (used.Count > 0) res.AnyP = SolveOr(deck, hand, slotIds, slotNeed, planSlots.ToArray(), ref res);
            }
            res.CombTotal = Odds.Comb(res.DeckSize, hand);
            return res;
        }

        /// <summary>슬롯 하나짜리(카드군에서 N장) 확률 — 편집 화면 미리보기용.</summary>
        public static double SinglePlan(DeckInfo deck, PlanSlot slot, int hand)
        {
            if (deck == null || slot == null || !slot.Valid) return 0;
            var slotIds = new List<int[]> { slot.Ids.Distinct().ToArray() };
            var need = new List<int> { slot.Need };
            var q = new List<Query> { new Query { Kind = 0, Slots = new[] { 0 } } };
            var res = new PlanResult();
            double[] p = Solve(deck, hand, slotIds, need, q, 1, ref res);
            return p.Length > 0 ? p[0] : 0;
        }

        // ── 공통 준비: 카드 → 마스크 분류 ───────────────────────
        private static void Classify(DeckInfo deck, List<int[]> slotIds, out int[] cMask, out int[] cTotal, out int relevant)
        {
            var entries = deck.Main.Where(c => c.Count > 0).ToList();
            var totals = new Dictionary<int, int>();
            relevant = 0;
            foreach (var e in entries)
            {
                int m = 0;
                for (int g = 0; g < slotIds.Count; g++)
                    if (Array.IndexOf(slotIds[g], e.Id) >= 0) m |= 1 << g;
                if (m == 0) continue;
                int t; totals.TryGetValue(m, out t);
                totals[m] = t + e.Count;
                relevant += e.Count;
            }
            cMask = totals.Keys.ToArray();
            cTotal = cMask.Select(m => totals[m]).ToArray();
        }

        // ── 핵심: 배분 열거 + Hall 판정 ──────────────────────────
        private static double[] Solve(DeckInfo deck, int hand, List<int[]> slotIds, List<int> slotNeed,
                                      List<Query> queries, int planCount, ref PlanResult res)
        {
            int N = deck.Size;
            int[] cMask, cTotal;
            int relevant;
            Classify(deck, slotIds, out cMask, out cTotal, out relevant);
            int M = cMask.Length;
            int irrelevant = N - relevant;
            int Q = queries.Count;

            var needSum = new int[Q][];
            var cov = new int[Q][];
            var aff = new long[M][];
            var localBit = new int[Q][];     // 전역 슬롯 → 질의 로컬 비트(없으면 -1)

            for (int q = 0; q < Q; q++)
            {
                if (queries[q].Kind == 2) { needSum[q] = new int[1]; cov[q] = new int[1]; continue; }
                int[] sl = queries[q].Slots ?? new int[0];
                int n = sl.Length;
                localBit[q] = new int[slotIds.Count];
                for (int i = 0; i < localBit[q].Length; i++) localBit[q][i] = -1;
                for (int b = 0; b < n; b++) localBit[q][sl[b]] = b;
                int subs = n > MaxQuerySlots ? 1 : (1 << n);
                needSum[q] = new int[subs];
                for (int s = 1; s < subs; s++)
                {
                    int sum = 0;
                    for (int b = 0; b < n; b++) if ((s & (1 << b)) != 0) sum += slotNeed[sl[b]];
                    needSum[q][s] = sum;
                }
                cov[q] = new int[subs];
            }

            for (int ci = 0; ci < M; ci++)
            {
                aff[ci] = new long[Q];
                for (int q = 0; q < Q; q++)
                {
                    if (queries[q].Kind == 2) continue;
                    int[] sl = queries[q].Slots ?? new int[0];
                    int n = sl.Length;
                    if (n == 0) continue;
                    int local = 0;
                    for (int b = 0; b < n; b++)
                        if ((cMask[ci] & (1 << sl[b])) != 0) local |= 1 << b;
                    if (local == 0) continue;
                    long bits = 0;
                    int subs = n > MaxQuerySlots ? 1 : (1 << n);
                    for (int s = 1; s < subs; s++) if ((local & s) != 0) bits |= 1L << (s - 1);
                    aff[ci][q] = bits;
                }
            }

            double total = Odds.Comb(N, hand);
            var ways = new double[Q];
            if (total <= 0) return ways;

            long leaves = 0;
            bool truncated = false;
            var planOk = new bool[planCount];

            Action<int, int, double> rec = null;
            rec = (ci, used, weight) =>
            {
                if (truncated) return;
                if (ci == M)
                {
                    leaves++;
                    if (leaves > MaxLeaves) { truncated = true; return; }
                    if (hand - used > irrelevant) return;
                    double w = weight * Odds.Comb(irrelevant, hand - used);
                    if (w <= 0) return;

                    // ① 플랜 판정 → ② 겹침 허용 AND / ③ 카드 분리 AND
                    for (int q = 0; q < Q; q++)
                        if (queries[q].Kind == 0)
                            planOk[queries[q].PlanA] = Check(queries[q], cov[q], needSum[q]);

                    for (int q = 0; q < Q; q++)
                    {
                        bool ok;
                        if (queries[q].Kind == 2)
                            ok = planOk[queries[q].PlanA] && planOk[queries[q].PlanB];
                        else if (queries[q].Kind == 0)
                            continue;                       // 위에서 이미 셌다
                        else
                            ok = Check(queries[q], cov[q], needSum[q]);
                        if (ok) ways[q] += w;
                    }
                    // 플랜(kind 0)도 같은 가중치로 누적
                    for (int q = 0; q < Q; q++)
                        if (queries[q].Kind == 0 && planOk[queries[q].PlanA]) ways[q] += w;
                    return;
                }
                int maxTake = Math.Min(cTotal[ci], hand - used);
                for (int t = 0; t <= maxTake; t++)
                {
                    if (t > 0)
                        for (int q = 0; q < Q; q++)
                        {
                            long bits = aff[ci][q];
                            while (bits != 0)
                            {
                                int s = BitOperations.TrailingZeroCount(bits) + 1;
                                cov[q][s] += t;
                                bits &= bits - 1;
                            }
                        }
                    rec(ci + 1, used + t, weight * Odds.Comb(cTotal[ci], t));
                    if (t > 0)
                        for (int q = 0; q < Q; q++)
                        {
                            long bits = aff[ci][q];
                            while (bits != 0)
                            {
                                int s = BitOperations.TrailingZeroCount(bits) + 1;
                                cov[q][s] -= t;
                                bits &= bits - 1;
                            }
                        }
                    if (truncated) return;
                }
            };
            rec(0, 0, 1.0);

            res.Leaves = leaves;
            res.Truncated = truncated;
            if (truncated) res.Note = "경우의 수가 너무 많아 중단했습니다(플랜·슬롯을 줄여주세요).";

            var outP = new double[Q];
            for (int q = 0; q < Q; q++) outP[q] = truncated ? double.NaN : ways[q] / total;
            return outP;
        }

        private static bool Check(Query query, int[] cov, int[] needSum)
        {
            int n = (query.Slots ?? new int[0]).Length;
            if (n == 0) return false;
            if (n > MaxQuerySlots) return false;     // 슬롯이 너무 많아 Hall 판정 불가(UI 에서 표시)
            int subs = 1 << n;
            for (int s = 1; s < subs; s++)
                if (needSum[s] > 0 && cov[s] < needSum[s]) return false;
            return true;
        }

        /// <summary>플랜들의 OR(하나라도) — 배분마다 어느 하나라도 만족하면 센다.</summary>
        private static double SolveOr(DeckInfo deck, int hand, List<int[]> slotIds, List<int> slotNeed,
                                      int[][] planSlots, ref PlanResult res)
        {
            int N = deck.Size;
            int[] cMask, cTotal;
            int relevant;
            Classify(deck, slotIds, out cMask, out cTotal, out relevant);
            int M = cMask.Length;
            int irrelevant = N - relevant;

            int P = planSlots.Length;
            var pNeed = new int[P][];
            var cov = new int[P][];
            var aff = new long[M][];
            for (int p = 0; p < P; p++)
            {
                int n = planSlots[p].Length;
                int subs = n > MaxQuerySlots ? 1 : (1 << n);
                pNeed[p] = new int[subs];
                for (int s = 1; s < subs; s++)
                {
                    int sum = 0;
                    for (int b = 0; b < n; b++) if ((s & (1 << b)) != 0) sum += slotNeed[planSlots[p][b]];
                    pNeed[p][s] = sum;
                }
                cov[p] = new int[subs];
            }
            for (int ci = 0; ci < M; ci++)
            {
                aff[ci] = new long[P];
                for (int p = 0; p < P; p++)
                {
                    int n = planSlots[p].Length;
                    if (n == 0) continue;
                    int local = 0;
                    for (int b = 0; b < n; b++)
                        if ((cMask[ci] & (1 << planSlots[p][b])) != 0) local |= 1 << b;
                    if (local == 0) continue;
                    long bits = 0;
                    int subs = n > MaxQuerySlots ? 1 : (1 << n);
                    for (int s = 1; s < subs; s++) if ((local & s) != 0) bits |= 1L << (s - 1);
                    aff[ci][p] = bits;
                }
            }

            double total = Odds.Comb(N, hand);
            if (total <= 0) return 0;
            double ways = 0;
            long leaves = 0;
            bool truncated = false;

            Action<int, int, double> rec = null;
            rec = (ci, used, weight) =>
            {
                if (truncated) return;
                if (ci == M)
                {
                    leaves++;
                    if (leaves > MaxLeaves) { truncated = true; return; }
                    if (hand - used > irrelevant) return;
                    double w = weight * Odds.Comb(irrelevant, hand - used);
                    if (w <= 0) return;
                    for (int p = 0; p < P; p++)
                    {
                        int n = planSlots[p].Length;
                        if (n == 0) continue;
                        if (n > MaxQuerySlots) continue;
                        bool ok = true;
                        int subs = 1 << n;
                        for (int s = 1; s < subs; s++)
                            if (pNeed[p][s] > 0 && cov[p][s] < pNeed[p][s]) { ok = false; break; }
                        if (ok) { ways += w; return; }
                    }
                    return;
                }
                int maxTake = Math.Min(cTotal[ci], hand - used);
                for (int t = 0; t <= maxTake; t++)
                {
                    if (t > 0)
                        for (int p = 0; p < P; p++)
                        {
                            long bits = aff[ci][p];
                            while (bits != 0)
                            {
                                int s = BitOperations.TrailingZeroCount(bits) + 1;
                                cov[p][s] += t;
                                bits &= bits - 1;
                            }
                        }
                    rec(ci + 1, used + t, weight * Odds.Comb(cTotal[ci], t));
                    if (t > 0)
                        for (int p = 0; p < P; p++)
                        {
                            long bits = aff[ci][p];
                            while (bits != 0)
                            {
                                int s = BitOperations.TrailingZeroCount(bits) + 1;
                                cov[p][s] -= t;
                                bits &= bits - 1;
                            }
                        }
                    if (truncated) return;
                }
            };
            rec(0, 0, 1.0);
            if (truncated) { res.Truncated = true; return double.NaN; }
            return ways / total;
        }

        // ── 자체 검증: 12장 덱 전수 열거(792조합)와 대조 ─────────────
        /// <summary>겹치는 카드군·2장 요구·AND 두 종류를 작은 덱에서 전수 열거로 확인한다.</summary>
        public static int SelfTest(List<string> log)
        {
            int fail = 0;
            var deck = new DeckInfo();
            for (int i = 1; i <= 6; i++) deck.Main.Add(new CardEntry { Id = i, Count = 2, Name = "#" + i });

            var plans = new List<Plan>
            {
                new Plan { Name = "1번 1장", Slots = { new PlanSlot { Need = 1, Ids = new List<int> { 1 } } } },
                new Plan { Name = "1·2 중 1장", Slots = { new PlanSlot { Need = 1, Ids = new List<int> { 1, 2 } } } },
                new Plan { Name = "5번 2장", Slots = { new PlanSlot { Need = 2, Ids = new List<int> { 5 } } } },
                new Plan { Name = "1장+2장(분리)", Slots = { new PlanSlot { Need = 1, Ids = new List<int> { 1 } }, new PlanSlot { Need = 1, Ids = new List<int> { 2 } } } },
                new Plan { Name = "겹치는 슬롯", Slots = { new PlanSlot { Need = 1, Ids = new List<int> { 1, 2, 3 } }, new PlanSlot { Need = 1, Ids = new List<int> { 2, 3, 4 } } } }
            };
            const int hand = 5;
            var res = Compute(deck, plans, hand);
            if (double.IsNaN(res.AnyP) || res.Truncated)
            {
                log.Add("⑥ 플랜 엔진: 계산 실패(중단됨) FAIL");
                return 1;
            }

            var ids = new List<int>();
            foreach (var c in deck.Main) for (int k = 0; k < c.Count; k++) ids.Add(c.Id);
            var planSlots = plans.Select(p => p.Slots.Where(s => s.Valid).ToList()).ToList();

            long total = 0;
            var hit = new long[plans.Count];
            long anyHit = 0;
            var pairBoth = new long[plans.Count, plans.Count];      // 겹침 허용
            var pairDis = new long[plans.Count, plans.Count];       // 카드 분리

            foreach (var combo in Combos(ids.Count, hand))
            {
                total++;
                var have = new Dictionary<int, int>();
                foreach (int idx in combo)
                {
                    int id = ids[idx];
                    int c; have.TryGetValue(id, out c);
                    have[id] = c + 1;
                }
                var ok = new bool[plans.Count];
                for (int i = 0; i < plans.Count; i++)
                {
                    ok[i] = Feasible(have, planSlots[i]);
                    if (ok[i]) hit[i]++;
                }
                if (ok.Any(x => x)) anyHit++;
                for (int i = 0; i < plans.Count; i++)
                    for (int j = i + 1; j < plans.Count; j++)
                    {
                        if (ok[i] && ok[j]) pairBoth[i, j]++;
                        var merged = new List<PlanSlot>(planSlots[i]);
                        merged.AddRange(planSlots[j]);
                        if (merged.Count <= MaxQuerySlots && Feasible(have, merged)) pairDis[i, j]++;
                    }
            }

            for (int i = 0; i < plans.Count; i++)
            {
                double b = (double)hit[i] / total;
                bool ok = Math.Abs(b - res.PlanP[i]) < 1e-9;
                log.Add(string.Format("⑥-{0} 플랜 '{1}' 전수 {2:P3} / 엔진 {3:P3} {4}",
                    i + 1, plans[i].Name, b, res.PlanP[i], ok ? "OK" : "FAIL"));
                if (!ok) fail++;
            }
            bool okAny = Math.Abs((double)anyHit / total - res.AnyP) < 1e-9;
            log.Add(string.Format("⑥ 전체(하나라도) 전수 {0:P3} / 엔진 {1:P3} {2}",
                (double)anyHit / total, res.AnyP, okAny ? "OK" : "FAIL"));
            if (!okAny) fail++;

            int pairOk = 0, pairBad = 0;
            foreach (var p in res.Pairs)
            {
                double bBoth = (double)pairBoth[p.A, p.B] / total;
                bool okB = Math.Abs(bBoth - p.P) < 1e-9;
                bool okD = double.IsNaN(p.PDisjoint) || Math.Abs((double)pairDis[p.A, p.B] / total - p.PDisjoint) < 1e-9;
                if (okB && okD) pairOk++; else pairBad++;
            }
            log.Add(string.Format("⑥ AND 쌍 {0}개 — 겹침 허용/카드 분리 전수 대조 {1}{2}",
                res.Pairs.Count, pairOk + " OK", pairBad > 0 ? " · " + pairBad + " FAIL" : ""));
            fail += pairBad;

            log.Add(string.Format("⑥ 플랜 엔진: 조합 {0:N0} · 배분 {1:N0} → {2}", total, res.Leaves,
                fail == 0 ? "ALL PASS" : fail + " FAIL"));
            return fail;
        }

        private static bool Feasible(Dictionary<int, int> have, List<PlanSlot> slots)
        {
            var pool = new Dictionary<int, int>(have);
            return FeasibleRec(pool, slots, 0);
        }

        private static bool FeasibleRec(Dictionary<int, int> pool, List<PlanSlot> slots, int i)
        {
            if (i >= slots.Count) return true;
            var slot = slots[i];
            var cand = slot.Ids.Where(id => pool.ContainsKey(id) && pool[id] > 0).ToList();
            return PickRec(pool, slots, i, cand, slot.Need, 0);
        }

        private static bool PickRec(Dictionary<int, int> pool, List<PlanSlot> slots, int slotIdx, List<int> cand, int left, int start)
        {
            if (left == 0) return FeasibleRec(pool, slots, slotIdx + 1);
            for (int k = start; k < cand.Count; k++)
            {
                int id = cand[k];
                if (pool[id] <= 0) continue;
                pool[id]--;
                bool ok = PickRec(pool, slots, slotIdx, cand, left - 1, k);
                pool[id]++;
                if (ok) return true;
            }
            return false;
        }

        private static IEnumerable<int[]> Combos(int n, int k)
        {
            var idx = Enumerable.Range(0, k).ToArray();
            if (k > n) yield break;
            while (true)
            {
                yield return idx.ToArray();
                int p = k - 1;
                while (p >= 0 && idx[p] == n - k + p) p--;
                if (p < 0) yield break;
                idx[p]++;
                for (int i = p + 1; i < k; i++) idx[i] = idx[i - 1] + 1;
            }
        }
    }
}
