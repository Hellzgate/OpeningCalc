// 그룹/콤보 확률 계산 결과(CLI·GUI 공용)
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpeningCalc
{
    public sealed class GroupStat
    {
        public int Group;
        public int Count;
        public List<CardEntry> Cards = new List<CardEntry>();
        public double P1;        // ≥1
        public double PExact1;   // =1
        public double P2Plus;    // ≥2
        public string Label
        {
            get
            {
                if (Cards.Count == 0) return "(없음)";
                if (Cards.Count == 1) return Cards[0].Name + " x" + Cards[0].Count;
                return Cards[0].Name + " x" + Cards[0].Count + " 외 " + (Cards.Count - 1) + "종";
            }
        }
    }

    public sealed class ComboStat
    {
        public int A, B;
        public double P;
        public string LabelA, LabelB;
    }

    public sealed class Analysis
    {
        public int DeckSize, Hand;
        public List<GroupStat> Groups = new List<GroupStat>();
        public List<ComboStat> Combos = new List<ComboStat>();
        public double AnyP;          // 그룹 1~9 중 하나라도
        public double AnyExpected;   // 기대 장수
        public int StarterCount;     // 그룹 1~9 합계 장수
    }

    public static class Analyzer
    {
        /// <summary>덱의 그룹 지정(1~9)을 읽어 그룹별·조합별 확률을 계산한다.</summary>
        public static Analysis Run(DeckInfo deck, int hand)
        {
            var a = new Analysis { DeckSize = deck.Size, Hand = hand };

            var byGroup = new SortedDictionary<int, List<CardEntry>>();
            foreach (var c in deck.Main)
            {
                if (c.Group <= 0) continue;
                if (!byGroup.ContainsKey(c.Group)) byGroup[c.Group] = new List<CardEntry>();
                byGroup[c.Group].Add(c);
            }

            foreach (var kv in byGroup)
            {
                int cnt = kv.Value.Sum(x => x.Count);
                var g = new GroupStat
                {
                    Group = kv.Key,
                    Count = cnt,
                    Cards = kv.Value,
                    P1 = Odds.AtLeastOne(deck.Size, cnt, hand),
                    PExact1 = Odds.Exactly(deck.Size, cnt, hand, 1),
                    P2Plus = Odds.AtLeast(deck.Size, cnt, hand, 2)
                };
                a.Groups.Add(g);
                a.StarterCount += cnt;
            }

            // 조합: 그룹 2개씩(서로 다른 그룹)
            for (int i = 0; i < a.Groups.Count; i++)
                for (int j = i + 1; j < a.Groups.Count; j++)
                {
                    double p = Odds.ComboAtLeastOne(deck.Size, new[] { a.Groups[i].Count, a.Groups[j].Count }, hand);
                    a.Combos.Add(new ComboStat
                    {
                        A = a.Groups[i].Group,
                        B = a.Groups[j].Group,
                        P = p,
                        LabelA = a.Groups[i].Label,
                        LabelB = a.Groups[j].Label
                    });
                }
            a.Combos.Sort((x, y) => y.P.CompareTo(x.P));

            var counts = a.Groups.Select(g => g.Count).ToArray();
            a.AnyP = counts.Length > 0 ? Odds.AnyOfGroups(deck.Size, counts, hand) : 0;
            a.AnyExpected = Odds.Expected(deck.Size, a.StarterCount, hand);
            return a;
        }
    }
}
