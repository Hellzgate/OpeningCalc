// --plancheck <scenario.json> — 플랜 확률 엔진을 외부(파이썬 대조 스크립트)에서 검증하기 위한 CLI.
// 시나리오: { hand, deck:[{id,count}], plans:[{name, slots:[{need, ids:[...]}]}] }
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpeningCalc
{
    internal static class PlanCli
    {
        private sealed class SDeck { public int id { get; set; } public int count { get; set; } }
        private sealed class SSlot { public int need { get; set; } public int[] ids { get; set; } public string[] groups { get; set; } }
        private sealed class SPlan { public string name { get; set; } public SSlot[] slots { get; set; } }
        private sealed class SGroup { public string name { get; set; } public int[] ids { get; set; } }
        private sealed class SCase { public int hand { get; set; } public SDeck[] deck { get; set; } public SGroup[] groups { get; set; } public SPlan[] plans { get; set; } }

        public static int Run(string[] args)
        {
            string path = args[1];
            SCase c;
            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
                c = JsonSerializer.Deserialize<SCase>(File.ReadAllText(path), opts);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SCENARIO ERROR: " + ex.Message);
                return 2;
            }
            if (c == null) { Console.WriteLine("SCENARIO EMPTY"); return 2; }

            var deck = new DeckInfo { FilePath = path };
            foreach (var d in c.deck ?? new SDeck[0])
                deck.Main.Add(new CardEntry { Id = d.id, Count = d.count, Name = "#" + d.id });

            var plans = new List<Plan>();
            foreach (var p in c.plans ?? new SPlan[0])
            {
                var plan = new Plan { Name = p.name ?? "" };
                foreach (var s in p.slots ?? new SSlot[0])
                {
                    var slot = new PlanSlot { Need = s.need, Ids = (s.ids ?? new int[0]).ToList() };
                    if (s.groups != null) slot.GroupNames.AddRange(s.groups.Where(x => !string.IsNullOrEmpty(x)));
                    plan.Slots.Add(slot);
                }
                plans.Add(plan);
            }

            var groups = new List<CardGroup>();
            foreach (var g in c.groups ?? new SGroup[0])
                groups.Add(new CardGroup { Name = g.name ?? "", Ids = (g.ids ?? new int[0]).ToList() });

            int hand = c.hand > 0 ? c.hand : 5;
            var res = PlanOdds.Compute(deck, plans, hand, groups);

            var o = new StringBuilder();
            o.Append("{");
            o.AppendFormat("\"hand\":{0},\"deckSize\":{1},\"comb\":{2:F0},", hand, res.DeckSize, res.CombTotal);
            o.Append("\"plans\":[");
            for (int i = 0; i < plans.Count; i++)
            {
                if (i > 0) o.Append(",");
                o.AppendFormat("{{\"name\":{0},\"p\":{1}}}", Q(plans[i].Name), Num(res.PlanP.Count > i ? res.PlanP[i] : double.NaN));
            }
            o.Append("],\"any\":" + Num(res.AnyP));
            o.Append(",\"pairs\":[");
            for (int i = 0; i < res.Pairs.Count; i++)
            {
                var p = res.Pairs[i];
                if (i > 0) o.Append(",");
                o.AppendFormat("{{\"a\":{0},\"b\":{1},\"p\":{2},\"pd\":{3}}}", p.A, p.B, Num(p.P), Num(p.PDisjoint));
            }
            o.Append("],");
            o.AppendFormat("\"leaves\":{0},\"truncated\":{1}", res.Leaves, res.Truncated ? "true" : "false");
            o.Append("}");

            Console.WriteLine(o.ToString());
            return 0;
        }

        private static string Num(double v)
        {
            if (double.IsNaN(v)) return "null";
            return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Q(string s)
        {
            return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
