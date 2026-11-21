using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gnurt.LogicMap
{
    /// <summary>Những gì đọc được từ một file C#.</summary>
    internal sealed class ScriptFacts
    {
        public readonly List<SceneLoadCall> Loads = new List<SceneLoadCall>();
        public bool DontDestroyOnLoad;
        public readonly Dictionary<string, string> Constants = new Dictionary<string, string>(); // "Tên" và "Lớp.Tên" → giá trị
    }

    /// <summary>
    /// Tìm lệnh chuyển scene trong code C# bằng regex. Chỉ đọc chữ, không compile,
    /// nên tham số tính lúc chạy (biến, hàm) thì chỉ đoán được một phần.
    /// </summary>
    internal static class SceneLoadParser
    {
        public static readonly string[] DefaultMethods = { "LoadScene", "LoadSceneAsync", "LoadLevel", "LoadLevelAsync" };

        static readonly Regex DdolRegex = new Regex(@"\bDontDestroyOnLoad\s*\(", RegexOptions.Compiled);
        static readonly Regex ConstRegex = new Regex(@"\b(?:const|static\s+readonly)\s+string\s+([A-Za-z_]\w*)\s*=\s*", RegexOptions.Compiled);
        static readonly Regex TypeRegex = new Regex(@"\b(?:class|struct)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
        static readonly Regex ParameterRegex = new Regex(@"^(?:(?:ref|out|in|this|params)\s+)?[A-Za-z_][\w<>\[\],.?]*\s+[A-Za-z_]\w*(?:\s*=.*)?$", RegexOptions.Compiled);
        static readonly Regex IdentifierPathRegex = new Regex(@"^[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*$", RegexOptions.Compiled);
        static readonly Regex NameofRegex = new Regex(@"^nameof\s*\(\s*(?:[A-Za-z_]\w*\s*\.\s*)*([A-Za-z_]\w*)\s*\)$", RegexOptions.Compiled);
        static readonly Regex EnumToStringRegex = new Regex(@"^(?:[A-Za-z_]\w*\s*\.\s*)+([A-Za-z_]\w*)\s*\.\s*ToString\s*\(\s*\)$", RegexOptions.Compiled);
        static readonly Regex NextRegex = new Regex(@"buildIndex\s*\+\s*1\b", RegexOptions.Compiled);
        static readonly Regex ReloadRegex = new Regex(@"GetActiveScene\s*\(\s*\)\s*\.\s*(?:name|buildIndex|path)$", RegexOptions.Compiled);
        static readonly Regex FormatRegex = new Regex(@"^(?:string|String)\s*\.\s*Format\s*\(", RegexOptions.Compiled);
        static readonly HashSet<string> CallKeywords = new HashSet<string> { "return", "await", "yield", "else", "in", "case", "throw", "do" };

        public static ScriptFacts Parse(string source, IEnumerable<string> methods = null)
        {
            var src = new CSharpSource(source);
            var facts = new ScriptFacts { DontDestroyOnLoad = DdolRegex.IsMatch(src.Masked) };
            ReadConstants(src, facts);

            var names = (methods ?? DefaultMethods).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => Regex.Escape(n.Trim())).Distinct().ToArray();
            if (names.Length == 0) return facts;
            var callRegex = new Regex($@"\b({string.Join("|", names)})\s*(?:<[^<>()]*>)?\s*\(");

            foreach (Match match in callRegex.Matches(src.Masked))
            {
                int nameIndex = match.Groups[1].Index;
                if (!IsCall(src.Masked, nameIndex)) continue;

                int open = match.Index + match.Length - 1;
                int close = src.FindClosing(open);
                if (close < 0) continue;
                var args = src.SplitTopLevel(open + 1, close, ',');
                if (ParameterRegex.IsMatch(args[0])) continue; // khai báo hàm, không phải lời gọi

                facts.Loads.Add(new SceneLoadCall
                {
                    method = match.Groups[1].Value,
                    line = src.LineOf(nameIndex),
                    member = src.EnclosingMember(nameIndex),
                    code = CSharpSource.Shorten(src.LineText(nameIndex), 160),
                    argument = args[0],
                    condition = src.ConditionAt(ExpressionStart(src.Masked, nameIndex)),
                });
            }
            return facts;
        }

        static void ReadConstants(CSharpSource src, ScriptFacts facts)
        {
            var types = TypeRegex.Matches(src.Masked).Cast<Match>().ToList();
            foreach (Match match in ConstRegex.Matches(src.Masked))
            {
                int start = match.Index + match.Length;
                if (start >= src.Code.Length || (src.Code[start] != '"' && src.Code[start] != '@')) continue;
                int end = CSharpSource.SkipString(src.Code, start);
                if (!TryLiteral(src.Code.Substring(start, end - start), out string value)) continue;

                string name = match.Groups[1].Value;
                facts.Constants[name] = value;
                var owner = types.LastOrDefault(t => t.Index < match.Index);
                if (owner != null) facts.Constants[owner.Groups[1].Value + "." + name] = value;
            }
        }

        // Chữ ngay trước tên hàm cho biết đây là lời gọi hay khai báo ("void LoadScene(").
        static bool IsCall(string masked, int nameIndex)
        {
            int i = nameIndex - 1;
            while (i >= 0 && char.IsWhiteSpace(masked[i])) i--;
            if (i < 0) return true;

            char c = masked[i];
            if (c == '.') return true;
            if (c == '>') return i > 0 && masked[i - 1] == '='; // "=>" là lời gọi, "Task<T> LoadScene(" là khai báo
            if (c == ']') return false;                          // "string[] LoadScene("
            if (c == '?') return i == 0 || !char.IsLetterOrDigit(masked[i - 1]); // "int? X(" là khai báo, "a ? X(" là lời gọi
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                int end = i;
                while (i >= 0 && (char.IsLetterOrDigit(masked[i]) || masked[i] == '_')) i--;
                return CallKeywords.Contains(masked.Substring(i + 1, end - i));
            }
            return true;
        }

        // Lùi về đầu biểu thức "A.B().C.LoadScene" để xét điều kiện từ đúng đầu câu lệnh.
        static int ExpressionStart(string masked, int nameIndex)
        {
            int start = nameIndex;
            while (true)
            {
                int i = start - 1;
                while (i >= 0 && char.IsWhiteSpace(masked[i])) i--;
                if (i < 0 || masked[i] != '.') return start;
                i--;
                if (i >= 0 && masked[i] == '?') i--;
                while (i >= 0 && char.IsWhiteSpace(masked[i])) i--;
                if (i >= 0 && (masked[i] == ')' || masked[i] == ']'))
                {
                    char close = masked[i], open = close == ')' ? '(' : '[';
                    int depth = 0;
                    for (; i >= 0; i--)
                    {
                        if (masked[i] == close) depth++;
                        else if (masked[i] == open && --depth == 0) break;
                    }
                    i--;
                    while (i >= 0 && char.IsWhiteSpace(masked[i])) i--;
                }
                int end = i;
                while (i >= 0 && (char.IsLetterOrDigit(masked[i]) || masked[i] == '_')) i--;
                if (i == end) return start;
                start = i + 1;
            }
        }

        /// <summary>Hiểu tham số của lệnh chuyển scene: tên cụ thể, số thứ tự, mẫu, hay chịu.</summary>
        public static void Classify(SceneLoadCall call, IReadOnlyDictionary<string, string> constants)
        {
            string arg = (call.argument ?? "").Trim();
            call.buildIndex = -1;
            call.target = arg;
            call.targetKind = TargetKind.Unknown;
            if (arg.Length == 0) return;

            if (TryLiteral(arg, out string literal)) { Set(call, TargetKind.Name, literal); return; }
            if (int.TryParse(arg, out int index))
            {
                call.targetKind = TargetKind.BuildIndex;
                call.buildIndex = index;
                call.target = "#" + index;
                return;
            }

            var nameofMatch = NameofRegex.Match(arg);
            if (nameofMatch.Success) { Set(call, TargetKind.Name, nameofMatch.Groups[1].Value); return; }
            var enumName = EnumToStringRegex.Match(arg);
            if (enumName.Success) { Set(call, TargetKind.Name, enumName.Groups[1].Value); return; }
            if (NextRegex.IsMatch(arg)) { Set(call, TargetKind.Next, arg); return; }
            if (ReloadRegex.IsMatch(arg)) { Set(call, TargetKind.Reload, arg); return; }
            if (IdentifierPathRegex.IsMatch(arg) && TryConstant(arg, constants, out string constant)) { Set(call, TargetKind.Name, constant); return; }

            string pattern = ToPattern(arg, constants);
            if (pattern == null) return;
            if (pattern.IndexOf('*') >= 0) Set(call, TargetKind.Pattern, pattern);
            else Set(call, TargetKind.Name, pattern);
        }

        static void Set(SceneLoadCall call, TargetKind kind, string target)
        {
            call.targetKind = kind;
            call.target = target;
        }

        static bool TryConstant(string path, IReadOnlyDictionary<string, string> constants, out string value)
        {
            value = null;
            if (constants == null) return false;
            var parts = path.Split('.').Select(p => p.Trim()).ToArray();
            if (constants.TryGetValue(string.Join(".", parts), out value)) return true;
            if (parts.Length >= 2 && constants.TryGetValue(parts[parts.Length - 2] + "." + parts[parts.Length - 1], out value)) return true;
            return constants.TryGetValue(parts[parts.Length - 1], out value);
        }

        // Ghép chuỗi ("Map " + i), chuỗi $"Map {i}" hoặc string.Format("Map {0}", i) thành mẫu "Map *".
        // null nếu không có phần chữ cố định nào.
        static string ToPattern(string arg, IReadOnlyDictionary<string, string> constants)
        {
            if (arg.StartsWith("$") || arg.StartsWith("@$"))
            {
                int end = CSharpSource.SkipString(arg, 0);
                if (end != arg.Length) return null;
                return Normalize(InterpolatedToPattern(arg));
            }

            if (FormatRegex.IsMatch(arg))
            {
                var inner = new CSharpSource(arg);
                int open = arg.IndexOf('(');
                int close = inner.FindClosing(open);
                if (close < 0) return null;
                var formatArgs = inner.SplitTopLevel(open + 1, close, ',');
                if (!TryLiteral(formatArgs[0], out string format)) return null;
                return Normalize(Regex.Replace(format, @"\{\d+(?:[,:][^}]*)?\}", "*"));
            }

            var source = new CSharpSource(arg);
            var parts = source.SplitTopLevel(0, arg.Length, '+');
            if (parts.Count < 2) return null;
            var sb = new StringBuilder();
            bool hasText = false;
            foreach (string part in parts)
            {
                if (TryLiteral(part, out string text) || (IdentifierPathRegex.IsMatch(part) && TryConstant(part, constants, out text)))
                {
                    sb.Append(text);
                    hasText |= text.Length > 0;
                }
                else if (part.StartsWith("$"))
                {
                    string inner = InterpolatedToPattern(part);
                    sb.Append(inner);
                    hasText |= inner.Trim('*').Length > 0;
                }
                else
                {
                    sb.Append('*');
                }
            }
            return hasText ? Normalize(sb.ToString()) : null;
        }

        static string InterpolatedToPattern(string literal)
        {
            int quote = literal.IndexOf('"');
            bool verbatim = literal.IndexOf('@') >= 0 && literal.IndexOf('@') < quote;
            string body = literal.Substring(quote + 1, literal.Length - quote - 2);
            var sb = new StringBuilder();
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (c == '{' && i + 1 < body.Length && body[i + 1] == '{') { sb.Append('{'); i++; continue; }
                if (c == '}' && i + 1 < body.Length && body[i + 1] == '}') { sb.Append('}'); i++; continue; }
                if (c == '{')
                {
                    int depth = 1;
                    while (++i < body.Length && depth > 0)
                    {
                        if (body[i] == '{') depth++;
                        else if (body[i] == '}') depth--;
                    }
                    i--;
                    sb.Append('*');
                    continue;
                }
                if (!verbatim && c == '\\' && i + 1 < body.Length) { sb.Append(Unescape(body[++i])); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string Normalize(string pattern)
        {
            if (pattern == null) return null;
            string collapsed = Regex.Replace(pattern, @"\*+", "*");
            return collapsed.Trim('*').Trim().Length == 0 ? null : collapsed;
        }

        /// <summary>Đọc một chuỗi hằng "..." hoặc @"..." (cả tham số phải là đúng một chuỗi).</summary>
        internal static bool TryLiteral(string text, out string value)
        {
            value = null;
            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return false;
            bool verbatim = text.StartsWith("@\"");
            if (!verbatim && !text.StartsWith("\"")) return false;
            if (CSharpSource.SkipString(text, 0) != text.Length || !text.EndsWith("\"")) return false;

            string body = text.Substring(verbatim ? 2 : 1, text.Length - (verbatim ? 3 : 2));
            var sb = new StringBuilder();
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (verbatim && c == '"' && i + 1 < body.Length && body[i + 1] == '"') { sb.Append('"'); i++; continue; }
                if (!verbatim && c == '\\' && i + 1 < body.Length) { sb.Append(Unescape(body[++i])); continue; }
                sb.Append(c);
            }
            value = sb.ToString();
            return true;
        }

        static char Unescape(char c)
        {
            switch (c)
            {
                case 'n': return '\n';
                case 't': return '\t';
                case '0': return '\0';
                default: return c; // \" \\ \' giữ nguyên ký tự
            }
        }
    }
}
