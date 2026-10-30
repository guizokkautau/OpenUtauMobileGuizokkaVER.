using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;

namespace UserPhonemizers {
    [Phonemizer("Japanese C+V", "JA C+V", author: "Guizokka", language: "JA")]
    public class JapaneseSeparateCVPhonemizer : Phonemizer {
        static readonly Dictionary<string, string> Mora = new Dictionary<string, string> {
            // Vowels
            ["あ"]="a", ["い"]="i", ["う"]="u", ["え"]="e", ["お"]="o",
            // K
            ["か"]="k a", ["き"]="k i", ["く"]="k u", ["け"]="k e", ["こ"]="k o",
            // S
            ["さ"]="s a", ["し"]="sh i", ["す"]="s u", ["せ"]="s e", ["そ"]="s o",
            // T
            ["た"]="ta", ["ち"]="ch i", ["つ"]="ts u", ["て"]="t e", ["と"]="t o",
            // N
            ["な"]="n a", ["に"]="n i", ["ぬ"]="n u", ["ね"]="n e", ["の"]="n o",
            // H
            ["は"]="h a", ["ひ"]="h i", ["ふ"]="f u", ["へ"]="h e", ["ほ"]="h o",
            // M
            ["ま"]="m a", ["み"]="m i", ["む"]="m u", ["め"]="m e", ["も"]="m o",
            // Y
            ["や"]="y a", ["ゆ"]="y u", ["よ"]="y o",
            // R
            ["ら"]="r a", ["り"]="r i", ["る"]="r u", ["れ"]="r e", ["ろ"]="r o",
            // W / moraic nasal
            ["わ"]="w a", ["を"]="o", ["ん"]="n",
            // Voiced K
            ["が"]="g a", ["ぎ"]="g i", ["ぐ"]="g u", ["げ"]="g e", ["ご"]="g o",
            // Z
            ["ざ"]="z a", ["じ"]="j i", ["ず"]="z u", ["ぜ"]="z e", ["ぞ"]="z o",
            // D
            ["だ"]="d a", ["ぢ"]="j i", ["づ"]="z u", ["で"]="d e", ["ど"]="d o",
            // B
            ["ば"]="b a", ["び"]="b i", ["ぶ"]="b u", ["べ"]="b e", ["ぼ"]="b o",
            // P
            ["ぱ"]="p a", ["ぴ"]="p i", ["ぷ"]="p u", ["ぺ"]="p e", ["ぽ"]="p o",
            // Small vowels / foreign combinations
            ["ぁ"]="a", ["ぃ"]="i", ["ぅ"]="u", ["ぇ"]="e", ["ぉ"]="o",
            ["ゔ"]="vu"
        };

        static readonly Dictionary<string, string> DigraphBase = new Dictionary<string, string> {
            ["き"]="k", ["ぎ"]="g", ["し"]="sh", ["じ"]="j", ["ち"]="ch",
            ["に"]="n", ["ひ"]="h", ["び"]="b", ["ぴ"]="p", ["み"]="m", ["り"]="r",
            ["て"]="t", ["で"]="d"
        };

        static string Hiragana(string s) {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++) {
                if (chars[i] >= '\u30a1' && chars[i] <= '\u30f6') chars[i] = (char)(chars[i] - 0x60);
            }
            return new string(chars);
        }

        public override void SetSinger(USinger singer) { }

        public override Result Process(Note[] notes, Note? prev, Note? next,
            Note? prevNeighbour, Note? nextNeighbour, Note[] prevs) {
            if (notes == null || notes.Length == 0) return new Result { phonemes = Array.Empty<Phoneme>() };
            string lyric = Hiragana((notes[0].phoneticHint ?? notes[0].lyric ?? "").Trim());
            if (lyric.Length == 0 || lyric == "R" || lyric == "-" || lyric == "br")
                return new Result { phonemes = Array.Empty<Phoneme>() };

            var units = ToUnicodeElements(lyric);
            var phones = new List<string>();
            for (int i = 0; i < units.Count; i++) {
                string u = units[i];
                if (u == "っ") {
                    // Gemination: repeat the next onset when that onset exists in this OTO.
                    if (i + 1 < units.Count) {
                        string nextUnit = units[i + 1];
                        string onset = GetOnset(nextUnit);
                        if (!string.IsNullOrEmpty(onset)) phones.Add(onset);
                    }
                    continue;
                }
                if (i + 1 < units.Count && IsSmallYaYuYo(units[i + 1]) && DigraphBase.TryGetValue(u, out string baseC)) {
                    string vowel = units[i + 1] == "ゃ" ? "a" : units[i + 1] == "ゅ" ? "u" : "o";
                    phones.Add(baseC);
                    phones.Add("y");
                    phones.Add(vowel);
                    i++;
                    continue;
                }
                if (i + 1 < units.Count && IsSmallE(units[i + 1]) && DigraphBase.TryGetValue(u, out string baseCE)) {
                    phones.Add(baseCE);
                    phones.Add("y");
                    phones.Add("e");
                    i++;
                    continue;
                }
                if (Mora.TryGetValue(u, out string romaji)) {
                    if (romaji.Length == 1) {
                        phones.Add(romaji);
                    } else {
                        string onset = GetOnsetFromRomaji(romaji);
                        string vowel = romaji.Substring(romaji.Length - 1, 1);
                        if (!string.IsNullOrEmpty(onset)) phones.Add(onset);
                        phones.Add(vowel);
                    }
                }
                // Unsupported punctuation/characters are ignored rather than generating invalid aliases.
            }

            if (phones.Count == 0) return new Result { phonemes = Array.Empty<Phoneme>() };
            var result = new List<Phoneme>();
            int duration = Math.Max(1, notes[0].duration);
            int spacing = Math.Max(1, duration / Math.Max(phones.Count, 1));
            for (int i = 0; i < phones.Count; i++) {
                result.Add(new Phoneme {
                    phoneme = phones[i],
                    position = i == 0 ? 0 : Math.Min(duration - 1, spacing * i),
                    expressions = new List<PhonemeExpression>()
                });
            }
            return new Result { phonemes = result.ToArray() };
        }

        static bool IsSmallYaYuYo(string s) => s == "ゃ" || s == "ゅ" || s == "ょ";
        static bool IsSmallE(string s) => s == "ぇ";

        static string GetOnset(string unit) {
            unit = Hiragana(unit);
            if (Mora.TryGetValue(unit, out string romaji)) return GetOnsetFromRomaji(romaji);
            if (DigraphBase.TryGetValue(unit, out string c)) return c;
            return "";
        }

        static string GetOnsetFromRomaji(string romaji) {
            if (romaji.Length <= 1) return "";
            if (romaji.StartsWith("ch")) return "ch";
            if (romaji.StartsWith("sh")) return "sh";
            if (romaji.StartsWith("ts")) return "ts";
            return romaji.Substring(0, 1);
        }
    }
}
