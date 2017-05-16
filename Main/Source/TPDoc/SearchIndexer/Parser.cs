using HtmlAgilityPack;
//using Iveonik.Stemmers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchIndexer
{
    public class Posting
    {
        public Posting(int line, int linepos, int offset)
        {
            Positions = new List<Tuple<int, int, int>>();
            Add(line, linepos, offset);
        }

        public Posting(List<Tuple<int, int, int>> positions)
        {
            Positions = positions;
        }

        public List<Tuple<int, int, int>> Positions { get; set; }

        public void Add(int line, int linepos, int offset)
        {
            Positions.Add(new Tuple<int, int, int>(line, linepos, offset));
        }

        public void Add(List<Tuple<int, int, int>> positions)
        {
            Positions.AddRange(positions);
        }

        public override string ToString()
        {
            String str = "[";
            foreach (Tuple<int, int, int> pos in Positions)
            {
                str += pos.Item1 + "," + pos.Item2 + "," + pos.Item3 + ",";
            }
            str += "]";
            return str;
        }
    }

    public class Parser
    {
        public readonly string[] IgnoreWords = new string[] { "and", "the", "for", "has", "are", "med", "til", "har", "type" };
        public Dictionary<String, Posting> Words { get; set; }
        public string[] splitStrings;
        //public EnglishStemmer stemmer;
        public bool stemWords;

        public Parser(bool useStemming = false)
        {
            Words = new Dictionary<String, Posting>();
            splitStrings = new string[] { " ", "\r\n", "\n", ".", "_" };
            //stemmer = new EnglishStemmer();
            stemWords = useStemming;
        }

        public void Clear()
        {
            Words.Clear();
        }

        public Dictionary<String, Posting> ParseHtml(string html)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            TraverseNode(doc.DocumentNode);
            return Words;
        }

        private void UpsertWord(String w, int line, int linepos, int offset)
        {
            if (w == null || w.Length < 3 || w.Length > 199)
                return;

            if (IgnoreWords.Contains(w))
                return;

            if (Words.ContainsKey(w))
            {
                Words[w].Add(line, linepos, offset);
            }
            else
            {
                Words.Add(w, new Posting(line, linepos, offset));
            }
        }        

        public static string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            bool noLetters = true;
            foreach (char c in str)
            {
                if (c >= '0' && c <= '9')
                {
                    sb.Append(c);
                }
                else if((c >= 'a' && c <= 'z') || c == 'æ' || c == 'å' || c == 'ø')
                {
                    sb.Append(c);
                    noLetters = false;
                }
                else if(sb.Length > 0 &&  c == '-')
                    sb.Append(c);
            }
            if (noLetters)
                return "";
            else
                return sb.ToString();
        }

        public string StemWord(string word)
        {
            return word; //stemmer.Stem(word);
        }

        public void ParseWord(string word, int line, int linepos, int offset)
        {
            String w = RemoveSpecialCharacters(word.ToLowerInvariant());
            if (stemWords)
                w = StemWord(w);
            UpsertWord(w, line, linepos, offset);
        }

        public void ParsePhrase(string phrase, int line, int linepos)
        {
            string[] words = phrase.Split(splitStrings, StringSplitOptions.RemoveEmptyEntries);
            int offset = 0;

            foreach (string w in words)
            {
                ParseWord(w, line, linepos, offset);
                offset = offset + w.Length + 1;
            }
        }

        private void TraverseNode(HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                if(node?.Name == "pre" || node?.ParentNode?.Name == "pre" || node?.Name == "code" || node?.ParentNode?.Name == "code")
                {
                    ;
                }
                else if (node.InnerText != null && node.InnerText.Length > 2)
                {
                    ParsePhrase(node.InnerText, node.Line, node.LinePosition);
                }
            }
            else
            {
                foreach (HtmlNode n in node.ChildNodes)
                {
                    TraverseNode(n);
                }
            }
        }

        public static void CheckNode(HtmlNode node, int line, int lineposition, ref HtmlNode match)
        {
            if (node.NodeType == HtmlNodeType.Text && node.Line == line && node.LinePosition == lineposition)
                match = node;
            else
            {
                foreach (HtmlNode n in node.ChildNodes)
                {
                    CheckNode(n, line, lineposition, ref match);
                }
            }

        }


    }
}
