// 초동 확률 계산 — 초기하분포(비복원 추출) 정확 계산. 시뮬레이션 없음.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpeningCalc
{
    public static class Odds
    {
        /// <summary>조합 nCk (k가 작을 때 안전한 곱셈 형태).</summary>
        public static double Comb(int n, int k)
        {
            if (k < 0 || n < 0 || k > n) return 0;
            if (k > n - k) k = n - k;
            double r = 1;
            for (int i = 0; i < k; i++) r = r * (n - i) / (i + 1);
            return r;
        }

        /// <summary>덱 n장 중 c장이 대상일 때, 손패 h장에 대상이 정확히 k장일 확률.</summary>
        public static double Exactly(int n, int c, int h, int k)
        {
            if (h < 0 || h > n || k < 0 || k > c || h - k > n - c) return 0;
            return Comb(c, k) * Comb(n - c, h - k) / Comb(n, h);
        }

        /// <summary>P(대상 ≥ 1장).</summary>
        public static double AtLeastOne(int n, int c, int h)
        {
            if (c <= 0 || h <= 0 || n <= 0) return 0;
            if (c >= n) return 1;                    // 덱 전체가 대상
            return 1.0 - Comb(n - c, h) / Comb(n, h);
        }

        /// <summary>P(대상 ≥ kmin장).</summary>
        public static double AtLeast(int n, int c, int h, int kmin)
        {
            double s = 0;
            for (int k = Math.Max(0, kmin); k <= Math.Min(c, h); k++) s += Exactly(n, c, h, k);
            return s;
        }

        /// <summary>P(=k) 분포 (k = 0..min(c,h)).</summary>
        public static double[] Distribution(int n, int c, int h)
        {
            int max = Math.Min(c, h);
            var d = new double[max + 1];
            for (int k = 0; k <= max; k++) d[k] = Exactly(n, c, h, k);
            return d;
        }

        /// <summary>여러 그룹(서로 겹치지 않음)이 각각 1장 이상 잡힐 확률. counts = 그룹별 장수.</summary>
        public static double ComboAtLeastOne(int n, int[] counts, int h)
        {
            if (counts == null || counts.Length == 0) return 0;
            if (counts.Any(c => c <= 0)) return 0;
            int inside = counts.Sum();
            if (inside > n) return 0;
            if (counts.Length > h) return 0;              // 손패보다 그룹이 많으면 불가

            // dp[k] = 그룹들에서 k장을 뽑는 경우의 수(각 그룹 ≥1)
            var dp = new double[h + 1];
            dp[0] = 1;
            foreach (int c in counts)
            {
                var nd = new double[h + 1];
                for (int k = 0; k <= h; k++)
                {
                    if (dp[k] == 0) continue;
                    for (int take = 1; take <= c && k + take <= h; take++)
                        nd[k + take] += dp[k] * Comb(c, take);
                }
                dp = nd;
            }
            double ways = 0;
            for (int k = 0; k <= h; k++) ways += dp[k] * Comb(n - inside, h - k);
            return ways / Comb(n, h);
        }

        /// <summary>P(그룹 중 최소 하나 이상) — 그룹들을 합쳐 한 덩어리로 본다(서로 겹치지 않으므로 동일).</summary>
        public static double AnyOfGroups(int n, int[] counts, int h)
        {
            int sum = counts.Where(c => c > 0).Sum();
            return AtLeastOne(n, sum, h);
        }

        /// <summary>기대 장수 = h * (대상 장수 / 덱 장수).</summary>
        public static double Expected(int n, int c, int h)
        {
            if (n <= 0) return 0;
            return (double)h * c / n;
        }

        // ── 자체 검증 ──────────────────────────────────────────
        /// <summary>작은 덱은 전수 열거로 대조해 수식을 검증한다.</summary>
        public static int SelfTest(List<string> log)
        {
            int fail = 0;
            // ① 알려진 값: 40장 중 3장, 5장 뽑기 → 1장 이상 = 33.76%
            double p = AtLeastOne(40, 3, 5);
            double expect = 1.0 - (double)435897 / 658008;
            bool ok1 = Math.Abs(p - expect) < 1e-12;
            log.Add(string.Format("① 40장/3장/5장 ≥1: {0:P4} (기대 {1:P4}) {2}", p, expect, ok1 ? "OK" : "FAIL"));
            if (!ok1) fail++;

            // ② 완전수 3장 중 1장 이상(패 5) = 100%
            bool ok2 = Math.Abs(AtLeastOne(40, 40, 5) - 1.0) < 1e-12;
            log.Add("② 덱 전체가 대상 → 100% " + (ok2 ? "OK" : "FAIL"));
            if (!ok2) fail++;

            // ③ 전수 열거 대조: 12장 덱(그룹 A 4장, B 3장, 나머지 5장), 손패 5장
            int n = 12, h = 5, a = 4, b = 3;
            double bruteA = 0, bruteB = 0, bruteBoth = 0, bruteNone = 0, bruteExact2A = 0;
            long total = 0;
            // 카드 인덱스 0..a-1 = A, a..a+b-1 = B, 나머지
            var idx = Enumerable.Range(0, n).ToArray();
            foreach (var combo in Combinations(idx, h))
            {
                total++;
                int ca = combo.Count(x => x < a);
                int cb = combo.Count(x => x >= a && x < a + b);
                if (ca >= 1) bruteA++;
                if (cb >= 1) bruteB++;
                if (ca >= 1 && cb >= 1) bruteBoth++;
                if (ca == 0 && cb == 0) bruteNone++;
                if (ca == 2) bruteExact2A++;
            }
            bool ok3 = Near(bruteA / total, AtLeastOne(n, a, h))
                    && Near(bruteB / total, AtLeastOne(n, b, h))
                    && Near(bruteBoth / total, ComboAtLeastOne(n, new[] { a, b }, h))
                    && Near(bruteNone / total, 1 - AnyOfGroups(n, new[] { a, b }, h))
                    && Near(bruteExact2A / total, Exactly(n, a, h, 2));
            log.Add(string.Format("③ 12장 전수열거({0}조합): A≥1 {1:P3}/{2:P3} · B≥1 {3:P3}/{4:P3} · A∧B {5:P3}/{6:P3} · A=2 {7:P3}/{8:P3} → {9}",
                total, bruteA / total, AtLeastOne(n, a, h), bruteB / total, AtLeastOne(n, b, h),
                bruteBoth / total, ComboAtLeastOne(n, new[] { a, b }, h),
                bruteExact2A / total, Exactly(n, a, h, 2), ok3 ? "OK" : "FAIL"));
            if (!ok3) fail++;

            // ④ 분포 합 = 1
            var d = Distribution(45, 7, 5);
            double sum = d.Sum();
            bool ok4 = Near(sum, 1.0);
            log.Add(string.Format("④ P(=k) 합 = {0:F12} {1}", sum, ok4 ? "OK" : "FAIL"));
            if (!ok4) fail++;

            // ⑤ 3그룹 조합 검증(전수 열거): 15장(A4,B3,C2,나머지6), 손패 5
            int n5 = 15, a5 = 4, b5 = 3, c5 = 2;
            long t5 = 0, both5 = 0;
            foreach (var combo in Combinations(Enumerable.Range(0, n5).ToArray(), 5))
            {
                t5++;
                bool A = combo.Any(x => x < a5);
                bool B = combo.Any(x => x >= a5 && x < a5 + b5);
                bool C = combo.Any(x => x >= a5 + b5 && x < a5 + b5 + c5);
                if (A && B && C) both5++;
            }
            bool ok5 = Near((double)both5 / t5, ComboAtLeastOne(n5, new[] { a5, b5, c5 }, 5));
            log.Add(string.Format("⑤ 3그룹 A∧B∧C: {0:P3} vs {1:P3} {2}",
                (double)both5 / t5, ComboAtLeastOne(n5, new[] { a5, b5, c5 }, 5), ok5 ? "OK" : "FAIL"));
            if (!ok5) fail++;

            log.Add("결과: " + (fail == 0 ? "ALL PASS" : fail + " FAIL"));
            return fail;
        }

        private static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }

        private static IEnumerable<int[]> Combinations(int[] src, int k)
        {
            var idx = Enumerable.Range(0, k).ToArray();
            if (k > src.Length) yield break;
            while (true)
            {
                yield return idx.Select(i => src[i]).ToArray();
                int p = k - 1;
                while (p >= 0 && idx[p] == src.Length - k + p) p--;
                if (p < 0) yield break;
                idx[p]++;
                for (int i = p + 1; i < k; i++) idx[i] = idx[i - 1] + 1;
            }
        }
    }
}
