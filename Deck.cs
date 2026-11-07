// .ydk 파싱 + 카드 이름(cdb) 조회
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindBotAIEditor.Services;

namespace OpeningCalc
{
    public sealed class CardEntry
    {
        public int Id;
        public int Count;
        public string Name;
        /// <summary>초동 그룹(0 = 미지정, 1~9)</summary>
        public int Group;
        public override string ToString() { return Name + " x" + Count; }
    }

    public sealed class DeckInfo
    {
        public string FilePath;
        public readonly List<CardEntry> Main = new List<CardEntry>();
        public int ExtraCount, SideCount;
        public int Size { get { return Main.Sum(c => c.Count); } }
        public List<string> Warnings = new List<string>();
    }

    public static class DeckLoader
    {
        /// <summary>.ydk 읽기 — #main / #extra / !side. 같은 카드ID는 수량 합산.</summary>
        public static DeckInfo Load(string path)
        {
            var d = new DeckInfo { FilePath = path };
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (Exception ex) { d.Warnings.Add("파일 읽기 실패: " + ex.Message); return d; }

            int section = 0;               // 0=main, 1=extra, 2=side
            var order = new List<int>();
            var counts = new Dictionary<int, int>();

            foreach (string raw in lines)
            {
                string s = (raw ?? "").Trim();
                if (s.Length == 0) continue;
                if (s.StartsWith("#"))
                {
                    string low = s.ToLowerInvariant();
                    if (low.StartsWith("#main")) section = 0;
                    else if (low.StartsWith("#extra")) section = 1;
                    else if (low.StartsWith("!side") || low.StartsWith("#!side") || low.StartsWith("#side")) section = 2;
                    continue;              // #created by ... 등 주석
                }
                if (s.StartsWith("!"))
                {
                    if (s.Equals("!side", StringComparison.OrdinalIgnoreCase)) section = 2;
                    continue;
                }
                int id;
                if (!int.TryParse(s, out id)) { d.Warnings.Add("숫자가 아닌 줄 무시: " + s); continue; }
                if (id <= 0) { d.Warnings.Add("잘못된 카드 ID: " + s); continue; }

                if (section == 1) { d.ExtraCount++; continue; }
                if (section == 2) { d.SideCount++; continue; }

                if (!counts.ContainsKey(id)) { counts[id] = 0; order.Add(id); }
                counts[id]++;
            }

            foreach (int id in order) d.Main.Add(new CardEntry { Id = id, Count = counts[id], Name = "#" + id });
            return d;
        }

        /// <summary>EDOPro 폴더의 cdb들을 열어 카드 이름을 채운다(실패해도 #ID 유지).</summary>
        public static int FillNames(DeckInfo deck, string edoProDir)
        {
            if (string.IsNullOrEmpty(edoProDir) || !Directory.Exists(edoProDir)) return 0;
            try
            {
                var paths = new List<string>();
                foreach (string f in Directory.GetFiles(edoProDir, "*.cdb", SearchOption.AllDirectories))
                    if (!paths.Contains(f)) paths.Add(f);
                if (paths.Count == 0) return 0;

                // CardDatabase 는 64개까지만 열고 "먼저 연 cdb"가 이긴다 → 한국어 cdb를 앞으로
                var ordered = new List<string>();
                for (int r = 0; r <= 2; r++)
                    foreach (string f in paths) if (Rank(f) == r) ordered.Add(f);

                CardDatabase.Instance.OpenMany(ordered);
                int hit = 0;
                foreach (var c in deck.Main)
                {
                    try
                    {
                        string n = CardDatabase.Instance.NameOf(c.Id);
                        if (!string.IsNullOrEmpty(n)) { c.Name = n; hit++; }
                    }
                    catch { }
                }
                return hit;
            }
            catch { return 0; }
        }

        private static int Rank(string path)
        {
            string p = path.ToLowerInvariant();
            bool ko = p.Contains("한국") || p.Contains("korean");
            if (!ko) return 2;
            return p.Contains("languages") ? 0 : 1;
        }

        /// <summary>cdb 가 있는 EDOPro 폴더 후보.</summary>
        public static string GuessEdoPro()
        {
            string[] c = { @"D:\edopro", @"C:\ProjectIgnis", @"C:\EDOPro" };
            foreach (string p in c)
            {
                try { if (Directory.Exists(p) && Directory.GetFiles(p, "*.cdb", SearchOption.AllDirectories).Length > 0) return p; }
                catch { }
            }
            return "";
        }
    }
}
