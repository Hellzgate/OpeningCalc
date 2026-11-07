using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpeningCalc
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }   // WinExe: 콘솔이 없으면 예외

            if (args.Length > 0)
            {
                if (args[0] == "--selftest") return SelfTest();
                if (args[0] == "--uicheck") return UiCheck();
                if (args[0] == "--dumpui") return DumpUi();
                if (args[0] == "--screenshot" && args.Length >= 2) return Shot(args[1], args.Length >= 3 ? args[2] : null);
                if (args[0] == "--calc") return Calc(args);
                if (args[0] == "--help" || args[0] == "-h")
                {
                    Usage();
                    return 0;
                }
                // 알 수 없는 인자 → GUI (창 뜸)
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            try
            {
                Application.Run(new MainForm());
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpeningCalc");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "startup-error.txt"), ex.ToString(), Encoding.UTF8);
                }
                catch { }
                MessageBox.Show(ex.Message, "OpeningCalc 시작 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static void Usage()
        {
            Console.WriteLine("OpeningCalc — 유희왕 초동 확률 계산기 (.ydk)");
            Console.WriteLine();
            Console.WriteLine("  OpeningCalc.exe                                  GUI 실행");
            Console.WriteLine("  OpeningCalc.exe --calc <deck.ydk> [옵션]         콘솔 계산");
            Console.WriteLine("  OpeningCalc.exe --selftest                       수식 자체 검증(전수 열거 대조)");
            Console.WriteLine("  OpeningCalc.exe --uicheck                        창 없이 UI 구성 검사");
            Console.WriteLine();
            Console.WriteLine("--calc 옵션");
            Console.WriteLine("  --cdb <EDOPro폴더>     카드 이름을 읽을 cdb (기본: 자동 탐색 D:\\edopro 등)");
            Console.WriteLine("  --hand <N>             패 장수 (기본 5 = 선공)");
            Console.WriteLine("  --groups \"id,id;id\"    초동 그룹 지정 (';' = 그룹 구분, ',' = 카드ID)");
            Console.WriteLine("  --json                 JSON 으로 출력");
        }

        // ── 수식 검증 ─────────────────────────────────────────
        private static int SelfTest()
        {
            var log = new List<string>();
            int fail = Odds.SelfTest(log);
            foreach (string l in log) Console.WriteLine("  " + l);

            // 실제 덱 파일이 있으면 파싱·이름 조회도 확인
            string sample = @"D:\edopro\WindBot\Decks\AI_raisemoon.ydk";
            if (File.Exists(sample))
            {
                var deck = DeckLoader.Load(sample);
                int named = DeckLoader.FillNames(deck, DeckLoader.GuessEdoPro());
                Console.WriteLine(string.Format("  덱 파싱: {0} → 메인 {1}장({2}종) · 엑스트라 {3} · 사이드 {4} · 이름 {5}/{2}",
                    Path.GetFileName(sample), deck.Size, deck.Main.Count, deck.ExtraCount, deck.SideCount, named));
                if (deck.Size <= 0) fail++;
            }

            Console.WriteLine(fail == 0 ? "SELFTEST OK" : "SELFTEST FAIL");
            return fail == 0 ? 0 : 3;
        }

        private static int Shot(string outPath, string deck)
        {
            using (var f = new MainForm())
            {
                f.CreateControl();
                if (!string.IsNullOrEmpty(deck) && File.Exists(deck)) f.OpenDeckForTest(deck);
                f.SaveScreenshot(outPath);
            }
            Console.WriteLine("SHOT " + outPath);
            return 0;
        }

        // ── 레이아웃 진단 ─────────────────────────────────────
        private static int DumpUi()
        {
            using (var f = new MainForm())
            {
                f.CreateControl();
                Console.Write(f.DumpLayout());
            }
            return 0;
        }

        // ── 창 없이 UI 검사 ───────────────────────────────────
        private static int UiCheck()
        {
            using (var f = new MainForm())
            {
                f.CreateControl();
                Console.WriteLine("UICHECK " + f.CheckControls() + " controls=" + f.ControlCount());
                bool ok = f.CheckControls().IndexOf("FAIL", StringComparison.Ordinal) < 0;
                Console.WriteLine(ok ? "UICHECK OK" : "UICHECK FAIL");
                return ok ? 0 : 2;
            }
        }

        // ── 콘솔 계산 ─────────────────────────────────────────
        private static int Calc(string[] args)
        {
            if (args.Length < 2) { Usage(); return 1; }
            string ydk = args[1];
            string cdb = "";
            int hand = 5;
            string groupsSpec = null;
            bool json = false;

            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--cdb": cdb = args[++i]; break;
                    case "--hand": hand = int.Parse(args[++i]); break;
                    case "--groups": groupsSpec = args[++i]; break;
                    case "--json": json = true; break;
                }
            }
            if (!File.Exists(ydk)) { Console.WriteLine("파일 없음: " + ydk); return 1; }

            var deck = DeckLoader.Load(ydk);
            if (string.IsNullOrEmpty(cdb)) cdb = DeckLoader.GuessEdoPro();
            int named = string.IsNullOrEmpty(cdb) ? 0 : DeckLoader.FillNames(deck, cdb);
            if (deck.Size <= 0) { Console.WriteLine("메인 덱이 비었습니다: " + ydk); return 1; }

            if (!string.IsNullOrEmpty(groupsSpec))
            {
                int g = 0;
                foreach (string part in groupsSpec.Split(';'))
                {
                    g++;
                    foreach (string tok in part.Split(','))
                    {
                        int id;
                        if (!int.TryParse(tok.Trim(), out id)) continue;
                        foreach (var c in deck.Main) if (c.Id == id) c.Group = g;
                    }
                }
            }

            var a = Analyzer.Run(deck, hand);

            if (json)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                sb.AppendFormat("\"deck\":\"{0}\",\"size\":{1},\"hand\":{2},", Escape(Path.GetFileName(ydk)), a.DeckSize, a.Hand);
                sb.AppendFormat("\"starterCount\":{0},\"anyP\":{1:F6},\"expected\":{2:F4},", a.StarterCount, a.AnyP, a.AnyExpected);
                sb.Append("\"groups\":[");
                for (int i = 0; i < a.Groups.Count; i++)
                {
                    var g = a.Groups[i];
                    if (i > 0) sb.Append(",");
                    sb.AppendFormat("{{\"group\":{0},\"count\":{1},\"p1\":{2:F6},\"pExact1\":{3:F6},\"p2plus\":{4:F6}}}",
                        g.Group, g.Count, g.P1, g.PExact1, g.P2Plus);
                }
                sb.Append("],\"combos\":[");
                for (int i = 0; i < a.Combos.Count; i++)
                {
                    var c = a.Combos[i];
                    if (i > 0) sb.Append(",");
                    sb.AppendFormat("{{\"a\":{0},\"b\":{1},\"p\":{2:F6}}}", c.A, c.B, c.P);
                }
                sb.Append("]}");
                Console.WriteLine(sb.ToString());
                return 0;
            }

            Console.WriteLine("FILE " + Path.GetFileName(ydk));
            Console.WriteLine(string.Format("  메인 {0}장 ({1}종) · 엑스트라 {2} · 사이드 {3} · 손패 {4}장(선공)",
                deck.Size, deck.Main.Count, deck.ExtraCount, deck.SideCount, hand));
            Console.WriteLine(string.Format("  카드 이름 {0}/{1} (cdb {2})", named, deck.Main.Count, string.IsNullOrEmpty(cdb) ? "없음" : cdb));
            foreach (string w in deck.Warnings.Take(5)) Console.WriteLine("  ⚠ " + w);

            if (a.Groups.Count == 0)
            {
                Console.WriteLine("  (그룹이 지정되지 않았습니다 — GUI 에서 그룹을 지정하거나 --groups 사용)");
                Console.WriteLine("  참고: 카드별 '1장 이상' 확률");
                foreach (var c in deck.Main.OrderByDescending(x => x.Count))
                    Console.WriteLine(string.Format("    {0,-34} x{1}  {2,7:P2}", c.Name, c.Count, Odds.AtLeastOne(deck.Size, c.Count, hand)));
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine("  그룹            장수   ≥1        =1        ≥2");
            foreach (var g in a.Groups)
                Console.WriteLine(string.Format("  [{0}] {1,-22} {2,3}  {3,7:P2}  {4,7:P2}  {5,7:P2}", g.Group, g.Label, g.Count, g.P1, g.PExact1, g.P2Plus));

            Console.WriteLine();
            Console.WriteLine(string.Format("  초동 합계 {0}장 → 1장 이상 {1:P2}  (기대 {2:F2}장)", a.StarterCount, a.AnyP, a.AnyExpected));

            if (a.Combos.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  조합(둘 다 1장 이상)");
                foreach (var c in a.Combos)
                    Console.WriteLine(string.Format("    [{0}] ∧ [{1}]  {2,7:P2}   ({3} / {4})", c.A, c.B, c.P, c.LabelA, c.LabelB));
            }
            return 0;
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
