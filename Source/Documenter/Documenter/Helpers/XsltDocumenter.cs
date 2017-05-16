using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomXsltDocumenter
{
    public class XsltDocumenter
    {
        public const string XslValueOfPrefix = @"xsl:value-of select";
        public const string XslElementAttributePrefix = @"xsl:attribute name";
        public const string XslTextAttributePrefix = @"xsl:text";
        public const string XslElementPrefix = @"xsl:element name";
        public const string XslTemplateNamePrefix = @"xsl:template name";
        public const string XslApplyTemplateNamePrefix = @"xsl:apply-templates select";
        public const string XslMatchTemplateNamePrefix = @"xsl:template match";
        List<string> xsltListDoc;

        public List<KeyValuePair<string, string>> DoMapDocumentation(string fileNamePath)
        {
            List<KeyValuePair<string, string>> resultValuesList = new List<KeyValuePair<string, string>>();
            try
            {
                xsltListDoc = LoadXslt(fileNamePath);
                int i = 0;
                string sourceNodeName;
                string destinationNodeName;
                foreach (string line in xsltListDoc)
                {
                    sourceNodeName = GetSourceChildNodeName(line, i);
                    if (!string.IsNullOrEmpty(sourceNodeName))
                    {
                        destinationNodeName = GetDestinationNodeName(i, true, true, false, 100000);
                        resultValuesList.Add(new KeyValuePair<string, string>(sourceNodeName, destinationNodeName));
                    }
                    i++;
                }

                //List<string> listToSave = new List<string>();
                //foreach (var item in resultValuesList)
                //{
                //    listToSave.Add(item.Key + " -----------> " + item.Value);
                //}

                //string destFileNamePrefixFixed = fileNamePath.Replace(".xsl", ".txt");
                //string destFileName = destFileNamePrefixFixed.Replace(@"\CustomXslt\AllCustomXslts", @"\CustomXslt\OutPutKeyValues");
                //File.WriteAllLines(destFileName, listToSave.ToArray());

            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Exception occured during parsing {0} file", fileNamePath), ex);
            }

            return resultValuesList;
        }

        public string GetSourceChildNodeName(string xsltLine, int lineNumber)
        {
            int valueOfPrefixStartIndex;
            int valueOfPrefixEndIndex;
            int xslTextAttributePrefixStartIndex;
            int xslTextAttributePrefixEndIndex;
            string result;
            string replacedSemicolumns;

            if (xsltLine.Contains(XslValueOfPrefix))
            {
                valueOfPrefixStartIndex = xsltLine.IndexOf(XslValueOfPrefix);
                valueOfPrefixEndIndex = GetLastIndexOf(xsltLine);
                result = xsltLine.Substring(valueOfPrefixStartIndex + XslValueOfPrefix.Length + 2, valueOfPrefixEndIndex - valueOfPrefixStartIndex - XslValueOfPrefix.Length - 3);
                replacedSemicolumns = result.Replace(@"&quot;", @"""");
                string parentTemplateName = GetSourceParentTempalteName(lineNumber);
                if (!string.IsNullOrEmpty(parentTemplateName)) return parentTemplateName + @"/" + replacedSemicolumns;

                return replacedSemicolumns;
            }

            if (xsltLine.Contains(XslTextAttributePrefix))
            {
                xslTextAttributePrefixStartIndex = xsltLine.IndexOf(XslTextAttributePrefix);
                xslTextAttributePrefixEndIndex = GetLastIndexOf(xsltLine);
                return @"HardcodedText: """ + xsltLine.Substring(xslTextAttributePrefixStartIndex + XslTextAttributePrefix.Length + 1, xslTextAttributePrefixEndIndex - xslTextAttributePrefixStartIndex - XslTextAttributePrefix.Length - 1) + @"""";
            }



            return string.Empty;
        }

        /// <summary>
        /// Recourcsively retrieves template names for initial lineNumber
        /// </summary>
        /// <param name="lineNumber"></param>
        /// <returns></returns>
        public string GetSourceParentTempalteName(int lineNumber)
        {
            int valueOfPrefixStartIndex;

            for (int i = lineNumber; i > 0; i--)
            {
                if (xsltListDoc[i].Contains(XslMatchTemplateNamePrefix))
                {
                    valueOfPrefixStartIndex = xsltListDoc[i].IndexOf(XslMatchTemplateNamePrefix);
                    string result = xsltListDoc[i].Substring(valueOfPrefixStartIndex + XslMatchTemplateNamePrefix.Length + 2, xsltListDoc[i].Length - valueOfPrefixStartIndex - XslMatchTemplateNamePrefix.Length - 4);
                    string parentResult = string.Empty;
                    int y = 0;
                    foreach (string line in xsltListDoc)
                    {
                        if (line.Contains(string.Format(XslApplyTemplateNamePrefix + @"=""{0}""", result))
                            && y < i)
                        {
                            parentResult = GetSourceParentTempalteName(y);
                        }
                        y++;
                    }

                    if (!string.IsNullOrEmpty(parentResult)) result = parentResult + "/" + result;
                    return result;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// recursive procedure which gets all parts of destination node relying on xsl file formatting
        /// </summary>
        /// <param name="xsltListDoc"></param>
        /// <param name="lineIndex"></param>
        /// <param name="isForElement"></param>
        /// <param name="isForAttribute"></param>
        /// <param name="isForTemplate"></param>
        /// <param name="startPosition"></param>
        /// <returns></returns>
        public string GetDestinationNodeName(int lineIndex, bool isForElement, bool isForAttribute, bool isForTemplate, int startPosition)
        {
            int xslElementPrefixStartIndex;
            int xslElementPrefixEndIndex;
            int xslElementAttributePrefixStartIndex;
            int xslElementAttributePrefixEndIndex;

            for (int i = lineIndex; i > 0; i--)
            {
                if (isForElement && xsltListDoc[i].Contains(XslElementPrefix))
                {
                    xslElementPrefixStartIndex = xsltListDoc[i].IndexOf(XslElementPrefix);
                    if (xslElementPrefixStartIndex < startPosition)
                    {
                        xslElementPrefixEndIndex = xsltListDoc[i].IndexOf(@">");
                        //string parentTempalteElement = GetDestinationNodeName(xsltListDoc, i, false, false, true, 0) + @"\";
                        string parentElementNode = GetDestinationNodeName(i, true, false, false, xslElementPrefixStartIndex);
                        string currentElementName = xsltListDoc[i].Substring(xslElementPrefixStartIndex + XslElementPrefix.Length + 2, xslElementPrefixEndIndex - xslElementPrefixStartIndex - XslElementPrefix.Length - 3);

                        if (!string.IsNullOrEmpty(parentElementNode)) parentElementNode = parentElementNode + @"\";

                        return parentElementNode + currentElementName;
                    }
                }

                if (isForAttribute && xsltListDoc[i].Contains(XslElementAttributePrefix))
                {
                    xslElementAttributePrefixStartIndex = xsltListDoc[i].IndexOf(XslElementAttributePrefix);
                    xslElementAttributePrefixEndIndex = xsltListDoc[i].IndexOf(@">");
                    string attributeResultName = xsltListDoc[i].Substring(xslElementAttributePrefixStartIndex + XslElementAttributePrefix.Length + 2, xslElementAttributePrefixEndIndex - xslElementAttributePrefixStartIndex - XslElementAttributePrefix.Length - 3);
                    string parentElement = GetDestinationNodeName(i, true, false, false, xslElementAttributePrefixStartIndex);
                    return parentElement + @"\@" + attributeResultName;
                }

                if (isForTemplate && xsltListDoc[i].Contains(XslTemplateNamePrefix))
                {
                    xslElementPrefixStartIndex = xsltListDoc[i].IndexOf(XslTemplateNamePrefix);
                    xslElementPrefixEndIndex = xsltListDoc[i].IndexOf(@">");

                    return xsltListDoc[i].Substring(xslElementPrefixStartIndex + XslElementPrefix.Length + 3, xslElementPrefixEndIndex - xslElementPrefixStartIndex - XslElementPrefix.Length - 4);
                }
            }

            return string.Empty;
        }

        private List<string> LoadXslt(string filePath)
        {
            List<string> sb = new List<string>();
            using (StreamReader Reader = new StreamReader(filePath))
            {
                string line;
                while ((line = Reader.ReadLine()) != null)
                {
                    sb.Add(line);
                }
            }

            return sb;
        }

        public int GetLastIndexOf(string xsltLine)
        {
            if (xsltLine.IndexOf(@"/>") != -1) return xsltLine.IndexOf(@"/>");
            if (xsltLine.IndexOf(@"</") != -1) return xsltLine.IndexOf(@"</");

            return -1;
        }
    }
}
