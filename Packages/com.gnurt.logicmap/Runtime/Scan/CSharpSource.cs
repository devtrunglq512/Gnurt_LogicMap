using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Một file C# đã chuẩn bị để tìm kiếm bằng regex, không cần trình biên dịch.
    /// <see cref="Code"/>: comment thay bằng dấu cách. <see cref="Masked"/>: thêm cả nội dung chuỗi bị xoá,
    /// để dấu ngoặc hay chữ trong chuỗi không làm nhầm. Cả hai dài bằng bản gốc nên vị trí và số dòng giữ nguyên.
    /// </summary>
    internal sealed class CSharpSource
    {
        public readonly string Code;
        public readonly string Masked;
        readonly List<int> _lineStarts = new List<int> { 0 };

        static readonly Regex MemberRegex = new Regex(@"\b([A-Za-z_]\w*)\s*\((?:[^()]|\([^()]*\))*\)\s*(?:where\s[^{;]*)?(\{|=>)", RegexOptions.Compiled);
        static readonly HashSet<string> NotMembers = new HashSet<string>
        {
            "if", "for", "foreach", "while", "switch", "catch", "using", "lock", "fixed", "return", "nameof",
            "typeof", "sizeof", "default", "new", "base", "this", "when", "checked", "unchecked", "else", "do",
        };

        List<(int start, int bodyStart, int bodyEnd, string name)> _members;

        public CSharpSource(string source)
        {
            source ??= "";
            var code = source.ToCharArray();
            var masked = source.ToCharArray();
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    int end = source.IndexOf('\n', i);
                    if (end < 0) end = source.Length;
                    Blank(code, i, end);
                    Blank(masked, i, end);
                    i = end;
                }
                else if (c == '/' && next == '*')
                {
                    int end = source.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    end = end < 0 ? source.Length : end + 2;
                    Blank(code, i, end);
                    Blank(masked, i, end);
                    i = end;
                }
                else if (IsStringStart(source, i))
                {
                    int end = SkipString(source, i);
                    int open = source.IndexOf('"', i);
                    Blank(masked, open + 1, end - 1); // giữ dấu nháy để vẫn biết đây là chuỗi
                    i = end;
                }
                else if (c == '\'')
                {
                    int end = SkipChar(source, i);
                    Blank(masked, i + 1, end - 1);
                    i = end;
                }
                else
                {
                    i++;
                }
            }
            Code = new string(code);
            Masked = new string(masked);

            for (int k = 0; k < source.Length; k++)
                if (source[k] == '\n') _lineStarts.Add(k + 1);
        }

        static void Blank(char[] chars, int from, int to)
        {
            for (int k = from; k < to && k < chars.Length; k++)
                if (chars[k] != '\n' && chars[k] != '\r') chars[k] = ' ';
        }

        static bool IsStringStart(string s, int i)
        {
            char c = s[i];
            if (c == '"') return true;
            if (c != '@' && c != '$') return false;
            // @"..", $"..", $@"..", @$".."
            int j = i + 1;
            if (j < s.Length && (s[j] == '@' || s[j] == '$') && s[j] != c) j++;
            return j < s.Length && s[j] == '"';
        }

        /// <summary>Vị trí ngay sau chuỗi bắt đầu tại i (kể cả chuỗi @, $ và phần {..} lồng trong $).</summary>
        internal static int SkipString(string s, int i)
        {
            bool verbatim = false, interpolated = false;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '@') verbatim = true;
                if (s[i] == '$') interpolated = true;
                i++;
            }
            i++; // dấu nháy mở
            while (i < s.Length)
            {
                char c = s[i];
                if (verbatim && c == '"')
                {
                    if (i + 1 < s.Length && s[i + 1] == '"') { i += 2; continue; }
                    return i + 1;
                }
                if (!verbatim)
                {
                    if (c == '\\') { i += 2; continue; }
                    if (c == '"') return i + 1;
                    if (c == '\n') return i; // chuỗi không đóng: dừng ở cuối dòng
                }
                if (interpolated && c == '{')
                {
                    if (i + 1 < s.Length && s[i + 1] == '{') { i += 2; continue; }
                    i = SkipInterpolation(s, i + 1);
                    continue;
                }
                i++;
            }
            return s.Length;
        }

        static int SkipInterpolation(string s, int i)
        {
            int depth = 1;
            while (i < s.Length)
            {
                if (IsStringStart(s, i)) { i = SkipString(s, i); continue; }
                if (s[i] == '\'') { i = SkipChar(s, i); continue; }
                if (s[i] == '{') depth++;
                else if (s[i] == '}' && --depth == 0) return i + 1;
                i++;
            }
            return s.Length;
        }

        static int SkipChar(string s, int i)
        {
            i++;
            while (i < s.Length)
            {
                if (s[i] == '\\') { i += 2; continue; }
                if (s[i] == '\'' || s[i] == '\n') return i + 1;
                i++;
            }
            return s.Length;
        }

        /// <summary>Số dòng (bắt đầu từ 1) của vị trí index.</summary>
        public int LineOf(int index)
        {
            int lo = 0, hi = _lineStarts.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_lineStarts[mid] <= index) lo = mid;
                else hi = mid - 1;
            }
            return lo + 1;
        }

        /// <summary>Nội dung dòng chứa index, đã bỏ comment và khoảng trắng hai đầu.</summary>
        public string LineText(int index)
        {
            int line = LineOf(index) - 1;
            int start = _lineStarts[line];
            int end = line + 1 < _lineStarts.Count ? _lineStarts[line + 1] : Code.Length;
            return Code.Substring(start, end - start).Trim();
        }

        /// <summary>Vị trí dấu đóng khớp với dấu mở tại index ((, {, [). -1 nếu không có.</summary>
        public int FindClosing(int openIndex)
        {
            char open = Masked[openIndex];
            char close = open == '(' ? ')' : open == '{' ? '}' : ']';
            int depth = 0;
            for (int i = openIndex; i < Masked.Length; i++)
            {
                if (Masked[i] == open) depth++;
                else if (Masked[i] == close && --depth == 0) return i;
            }
            return -1;
        }

        /// <summary>Tách đoạn [from, to) theo dấu phân cách nằm ở cấp ngoài cùng (không trong ngoặc, không trong chuỗi).</summary>
        public List<string> SplitTopLevel(int from, int to, char separator)
        {
            var parts = new List<string>();
            int depth = 0, start = from;
            for (int i = from; i < to; i++)
            {
                char c = Masked[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == separator && depth == 0)
                {
                    parts.Add(Code.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            parts.Add(Code.Substring(start, to - start).Trim());
            return parts;
        }

        /// <summary>Tên hàm (hoặc constructor) đang chứa vị trí index. null nếu không nằm trong hàm nào.</summary>
        public string EnclosingMember(int index)
        {
            if (_members == null) IndexMembers();
            string found = null;
            foreach (var member in _members)
            {
                if (member.start > index) break;
                if (index > member.bodyStart && index < member.bodyEnd) found = member.name; // hàm lồng trong hàm: lấy cái trong cùng
            }
            return found;
        }

        void IndexMembers()
        {
            _members = new List<(int, int, int, string)>();
            foreach (Match match in MemberRegex.Matches(Masked))
            {
                string name = match.Groups[1].Value;
                if (NotMembers.Contains(name)) continue;
                var body = match.Groups[2];
                int bodyEnd;
                if (body.Value == "{")
                {
                    bodyEnd = FindClosing(body.Index);
                }
                else
                {
                    bodyEnd = Masked.IndexOf(';', body.Index);
                }
                if (bodyEnd < 0) bodyEnd = Masked.Length;
                _members.Add((match.Index, body.Index, bodyEnd, name));
            }
        }

        static readonly Regex IfRegex = new Regex(@"^(?:else\s+)?if\s*\(", RegexOptions.Compiled);
        static readonly Regex CaseRegex = new Regex(@"(?:^|[;:{}]\s*)(case\s+[^:]+|default)\s*:\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Đoán điều kiện dẫn tới câu lệnh ở vị trí index: "level > 3", "else", "case 2"...
        /// Chỉ xét khối bao quanh gần nhất. null nếu không nằm trong if/else/case.
        /// </summary>
        public string ConditionAt(int index)
        {
            // Câu lệnh hiện tại bắt đầu sau dấu ; { } gần nhất.
            int statementStart = LastIndexOfAny(Masked, index - 1, ';', '{', '}') + 1;
            string prefix = Masked.Substring(statementStart, index - statementStart).Trim();
            string fromPrefix = ConditionFromHeader(statementStart + LeadingSpaces(statementStart, index), prefix);
            if (fromPrefix != null) return fromPrefix;

            // Không có thì xem phần đầu của khối { } đang chứa câu lệnh.
            int open = FindEnclosingOpen(statementStart - 1);
            if (open < 0) return null;
            int headerStart = LastIndexOfAny(Masked, open - 1, ';', '{', '}') + 1;
            string header = Masked.Substring(headerStart, open - headerStart).Trim();
            if (header.StartsWith("switch"))
            {
                // Trong switch: tìm nhãn case gần nhất phía trên.
                string body = Masked.Substring(open + 1, statementStart - open - 1);
                var cases = Regex.Matches(body, @"\b(case\s+[^:;{}]+|default)\s*:");
                return cases.Count == 0 ? null : Shorten(CodeAt(open + 1 + cases[cases.Count - 1].Groups[1].Index, cases[cases.Count - 1].Groups[1].Length));
            }
            return ConditionFromHeader(headerStart + LeadingSpaces(headerStart, open), header);
        }

        string ConditionFromHeader(int start, string header)
        {
            if (header.Length == 0) return null;
            var caseMatch = CaseRegex.Match(header);
            if (caseMatch.Success)
                return Shorten(CodeAt(start + caseMatch.Groups[1].Index, caseMatch.Groups[1].Length));

            var ifMatch = IfRegex.Match(header);
            if (ifMatch.Success)
            {
                int open = start + ifMatch.Length - 1;
                int close = FindClosing(open);
                if (close < 0) return null;
                return Shorten(Code.Substring(open + 1, close - open - 1));
            }
            if (header == "else") return "else";
            return null;
        }

        string CodeAt(int start, int length) => Code.Substring(start, length);

        int LeadingSpaces(int from, int to)
        {
            int i = from;
            while (i < to && char.IsWhiteSpace(Masked[i])) i++;
            return i - from;
        }

        int FindEnclosingOpen(int index)
        {
            int depth = 0;
            for (int i = index; i >= 0; i--)
            {
                char c = Masked[i];
                if (c == '}') depth++;
                else if (c == '{')
                {
                    if (depth == 0) return i;
                    depth--;
                }
            }
            return -1;
        }

        static int LastIndexOfAny(string s, int from, params char[] chars)
        {
            for (int i = System.Math.Min(from, s.Length - 1); i >= 0; i--)
                if (System.Array.IndexOf(chars, s[i]) >= 0) return i;
            return -1;
        }

        /// <summary>Gộp khoảng trắng và cắt bớt để vừa làm nhãn trên chart.</summary>
        internal static string Shorten(string text, int max = 48)
        {
            var sb = new StringBuilder();
            bool space = false;
            foreach (char c in text.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            string result = sb.ToString();
            return result.Length <= max ? result : result.Substring(0, max - 1) + "…";
        }
    }
}
