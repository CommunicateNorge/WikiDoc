//using AzureStorage;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Wiki.Models;

namespace Wiki.Utilities
{
    public class HtmlGenerator
    {
        public const String Br = "<br>";

        public static String CreateTag(String tagName, String tagContent = "", String className = null, bool closeTag = true, String id = null)
        {
            string tag = StartTag(tagName, className, id) + tagContent;
            if (closeTag)
                tag += CloseTag(tagName);
            return tag;
        }

        public static void CreateUl(IEnumerable<String> listItems, ref StringBuilder sb, String ulClass = null, String liClass = null)
        {
            sb.Append("<ul");
            if (ulClass != null)
                sb.Append($" class=\"{ulClass}\"");
            sb.AppendLine(">");
            foreach (String item in listItems)
            {
                sb.AppendLine(CreateTag("li", item, liClass));
            }
            sb.AppendLine(CloseTag("ul"));
        }

        public static String CreateTable<T>(IEnumerable<T> rowElements, Func<T,String> rowProcessor, bool wrapInTd = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(StartTag("table"));
            CreateRows<T>(rowElements, rowProcessor, wrapInTd);
            sb.AppendLine(CloseTag("table"));
            return sb.ToString();
        }
        public static String CreateRows<T>(IEnumerable<T> rowElements, Func<T, String> rowProcessor, bool wrapInTd = false)
        {
            StringBuilder sb = new StringBuilder();
            foreach (T item in rowElements)
            {
                sb.AppendLine(StartTag("tr"));
                if (wrapInTd) sb.AppendLine(StartTag("td"));
                sb.AppendLine(rowProcessor.Invoke(item));
                if (wrapInTd) sb.AppendLine(CloseTag("td"));
                sb.AppendLine(CloseTag("tr"));
            }
            return sb.ToString();
        }

        public static String CloseTag(String tagName)
        {
            return $"</{tagName}>";
        }

        public static String StartTag(String tagName, String className = null, String id = null)
        {
            className = (className == null) ? "" : $" class=\"{className}\"";
            id = (id == null) ? "" : $" id=\"{id}\"";

            return $"<{tagName}{className}{id}>";
        }

        public static string CreateLink(string lnk, string title = null, string className = null, string id = null, bool dotEncodeLink = false)
        {
            if (title == null)
                title = lnk;

            if (dotEncodeLink)
                lnk = WikiBlob.DotEncode(lnk);

            className = (className == null) ? "" : $" class=\"{className}\"";
            id = (id == null) ? "" : $" id=\"{id}\"";

            return $"<a href=\"{lnk}\"{id}{className}>{title}</a>";
        }

        public string CreateSelectList(List<string> options, string name, string id, string functionOnChangeName = "")
        {
            TagBuilder selectTag = new TagBuilder("select");
            selectTag.MergeAttribute("name", name);
            selectTag.MergeAttribute("id", id);
            selectTag.MergeAttribute("class", "modalSelectList");
            if(functionOnChangeName != "")
                selectTag.MergeAttribute("onchange", functionOnChangeName + ".call(this)");

            TagBuilder optionDefaultTag = new TagBuilder("option");
            optionDefaultTag.MergeAttribute("selected", "selected");
            optionDefaultTag.MergeAttribute("disabled", "disabled");
            optionDefaultTag.InnerHtml = "Select";
            selectTag.InnerHtml += optionDefaultTag;
            
            foreach (var item in options)
            {
                TagBuilder optionTag = new TagBuilder("option");
                optionTag.InnerHtml = item;
                selectTag.InnerHtml += optionTag;
            }

            return selectTag.ToString();
        }

        /// <summary>
        /// Helper method for <see cref="GetNewestPages"/> that creates the HTML formatted table over the last modified pages.
        /// </summary>
        /// <param name="blobs">The n newest blobs to include in the table</param>
        /// <returns>HTML table with the newest pages as content</returns>
        public string CreateTableFromBlobList(List<BlobUriAndModified> blobs)
        {
            TagBuilder tableTag = new TagBuilder("table");
            tableTag.AddCssClass("simpleTable");

            TagBuilder thTag1 = new TagBuilder("th");
            TagBuilder thTag2 = new TagBuilder("th");
            TagBuilder thtrTag = new TagBuilder("tr");
            thTag1.InnerHtml = "Modified";
            thTag2.InnerHtml = "Link";
            thtrTag.InnerHtml += thTag1;
            thtrTag.InnerHtml += thTag2;
            tableTag.InnerHtml += thtrTag;

            foreach (var item in blobs)
            {
                string cleanedUri = HttpContext.Current.Server.UrlDecode(item.URI);
                string cleanedLink = "";

                if (cleanedUri.StartsWith("Manual£"))
                {
                    cleanedLink = cleanedUri;
                    cleanedUri = cleanedUri.Remove(0, 7);
                }
                else if (cleanedUri.EndsWith("£Manual"))
                {
                    cleanedUri = cleanedUri.Remove(cleanedUri.Length - 7, 7);
                    cleanedLink = cleanedUri;
                }

                if (cleanedUri.Length > 35)
                {
                    cleanedUri = cleanedUri.Substring(0, 32) + "...";
                }

                TagBuilder trTag = new TagBuilder("tr");
                TagBuilder tdTag1 = new TagBuilder("td");
                TagBuilder tdTag2 = new TagBuilder("td");
                tdTag1.InnerHtml = item.Modified.ToString(new System.Globalization.CultureInfo("nb-NO"));
                TagBuilder aTag = new TagBuilder("a");
                aTag.MergeAttribute("href", "/Wiki/Page/" + cleanedLink);
                aTag.InnerHtml = cleanedUri.Replace('£', '/').Replace('€', '.');
                tdTag2.InnerHtml += aTag;
                trTag.InnerHtml += tdTag1;
                trTag.InnerHtml += tdTag2;
                tableTag.InnerHtml += trTag;
            }

            return tableTag.ToString();
        }

        /// <summary>
        /// Builds the HTML that gives the user the ability to create a new thumbnail. The HTML returned from this method is appended to the last cell of
        /// the thumbnail grid and is replaced by the new thumb when the user clicks save and the page reloads.
        /// Uses <see cref="GetEditThumbnailHTML"/> generate all the HTML except the top column level, which is only needed when creating new thumbs.
        /// </summary>
        /// <param name="imgLink">Link to the image file to use as a thumb image</param>
        /// <param name="titleLink">The link you are redirected too when clicking the thumb title</param>
        /// <param name="title">The text of the title itself</param>
        /// <param name="text">The body text of the thumb</param>
        /// <returns>Edit box HTML for a thumbnail</returns>
        public string GetCreateThumbnailHTML(string imgLink, string title, string text, string titleLink = "")
        {
            string thumbId = Guid.NewGuid().ToString();

            TagBuilder colDiv = new TagBuilder("div");
            colDiv.AddCssClass("col-sm-6");
            colDiv.AddCssClass("col-md-4");
            colDiv.AddCssClass("indexThumb");

            string innerThumbHTML = GetEditThumbnailHTML(imgLink, titleLink, title, text);

            colDiv.InnerHtml += innerThumbHTML;

            return colDiv.ToString();
        }

        /// <summary>
        /// Builds the HTML that gives the user the ability to edit a thumbnail. The HTML returned from this method is switched with 
        /// the HTML of the thumbnail element the user wishes to edit.
        /// </summary>
        /// <param name="imgLink">Link to the image file to use as a thumb image</param>
        /// <param name="titleLink">The link you are redirected too when clicking the thumb title</param>
        /// <param name="title">The text of the title itself</param>
        /// <param name="text">The body text of the thumb</param>
        /// <returns></returns>
        public string GetEditThumbnailHTML(string imgLink, string titleLink, string title, string text)
        {
            TagBuilder thumbDiv = new TagBuilder("div");
            thumbDiv.AddCssClass("thumbnail");

            TagBuilder capDiv = new TagBuilder("div");
            capDiv.AddCssClass("caption");

            TagBuilder imgEditTag = CreateInputGroup("Url to Image:", imgLink);
            imgEditTag.AddCssClass("imgLinkTextBox");

            TagBuilder aEditBoxTag = CreateInputGroup("Title link:", titleLink);
            aEditBoxTag.AddCssClass("titleLinkTextBox");

            TagBuilder h3EditTag = CreateInputGroup("Title:", title);
            h3EditTag.AddCssClass("titleTextBox");

            TagBuilder divTextAreaTag = new TagBuilder("div");
            divTextAreaTag.MergeAttribute("class", "form-group");
            divTextAreaTag.AddCssClass("textTextBox");

            TagBuilder labelTextAreaTag = new TagBuilder("label");
            labelTextAreaTag.MergeAttribute("for", "textTextBox");
            labelTextAreaTag.InnerHtml = "Text";

            TagBuilder textEditTag = new TagBuilder("textarea");
            textEditTag.MergeAttribute("rows", "5");
            textEditTag.MergeAttribute("cols", "50");
            textEditTag.MergeAttribute("id", "textTextBox");
            textEditTag.MergeAttribute("class", "form-control");
            textEditTag.InnerHtml += text;

            divTextAreaTag.InnerHtml += labelTextAreaTag;
            divTextAreaTag.InnerHtml += textEditTag;

            TagBuilder buttonTag = new TagBuilder("button");
            buttonTag.MergeAttribute("class", "btn btn-success editThumbButton");

            if (imgLink == "" && titleLink == "" && title == "" && text == "")
                buttonTag.MergeAttribute("onclick", "createThumb.call(this)");
            else
                buttonTag.MergeAttribute("onclick", "saveThumb.call(this)");

            buttonTag.MergeAttribute("type", "button");
            buttonTag.InnerHtml = "Save";

            capDiv.InnerHtml += imgEditTag;
            capDiv.InnerHtml += aEditBoxTag;
            capDiv.InnerHtml += h3EditTag;
            capDiv.InnerHtml += divTextAreaTag;
            capDiv.InnerHtml += buttonTag;
            thumbDiv.InnerHtml += capDiv;

            return thumbDiv.ToString();
        }

        private TagBuilder CreateInputGroup(string placeholder, string content)
        {
            TagBuilder divTag = new TagBuilder("div");
            divTag.AddCssClass("input-group");

            TagBuilder spanTag = new TagBuilder("span");
            spanTag.MergeAttribute("class", "input-group-addon");
            spanTag.MergeAttribute("id", "basic-addon1");
            spanTag.InnerHtml = placeholder;

            TagBuilder inputTag = new TagBuilder("input");
            inputTag.MergeAttribute("type", "text");
            inputTag.MergeAttribute("placeholder", placeholder);

            if (!string.IsNullOrEmpty(content))
                inputTag.MergeAttribute("value", content);

            inputTag.MergeAttribute("class", "form-control");
            inputTag.MergeAttribute("aria-describedby", "basic-addon1");

            divTag.InnerHtml += spanTag;
            divTag.InnerHtml += inputTag;

            return divTag;
        }

        public string CreateThumbnail(string imgLink, string title, string text, string titleLink = "")
        {
            string thumbId = Guid.NewGuid().ToString();

            TagBuilder colDiv = new TagBuilder("div");
            colDiv.AddCssClass("col-sm-6");
            colDiv.AddCssClass("col-md-4");
            colDiv.AddCssClass("indexThumb");
            colDiv.MergeAttribute("id", thumbId);

            string innerThumbHTML = CreateEditThumbnail(imgLink, title, text, titleLink);

            colDiv.InnerHtml += innerThumbHTML;

            return colDiv.ToString();
        }

        public string CreateEditThumbnail(string imgLink, string title, string text, string titleLink = "")
        {
            TagBuilder thumbDiv = new TagBuilder("div");
            thumbDiv.AddCssClass("thumbnail");

            TagBuilder capDiv = new TagBuilder("div");
            capDiv.AddCssClass("caption");

            TagBuilder imgTag = new TagBuilder("img");
            imgTag.MergeAttribute("src", imgLink);
            imgTag.MergeAttribute("width", "300");


            TagBuilder aTag = new TagBuilder("a");

            TagBuilder h3Tag = new TagBuilder("h3");
            h3Tag.InnerHtml = title;

            if (titleLink != "")
            {
                TagBuilder imgATag = new TagBuilder("a");
                imgATag.MergeAttribute("href", titleLink);
                imgATag.InnerHtml += imgTag;
                capDiv.InnerHtml += imgATag;

                aTag.MergeAttribute("href", titleLink);
                aTag.InnerHtml += h3Tag;
                capDiv.InnerHtml += aTag;
            }
            else
            {
                capDiv.InnerHtml += imgTag;
                aTag.InnerHtml += h3Tag;
                capDiv.InnerHtml += aTag;
            }

            TagBuilder pTag = new TagBuilder("p");
            pTag.InnerHtml = text;
            capDiv.InnerHtml += pTag;

            TagBuilder pEditTag = new TagBuilder("p");
            pEditTag.AddCssClass("floatRight");
            pEditTag.AddCssClass("indexEditGlyph");

            TagBuilder aEditTag = new TagBuilder("a");
            aEditTag.MergeAttribute("data-toggle", "tooltip");
            aEditTag.MergeAttribute("title", "Edit");
            aEditTag.AddCssClass("editThumbIcon");

            aEditTag.MergeAttribute("href", "#");

            TagBuilder spanEditIconTag = new TagBuilder("span");
            spanEditIconTag.MergeAttribute("class", "glyphicon glyphicon-edit");
            spanEditIconTag.MergeAttribute("aria-hidden", "true");

            aEditTag.InnerHtml += spanEditIconTag;
            pEditTag.InnerHtml += aEditTag;

            TagBuilder pRemoveTag = new TagBuilder("p");
            pRemoveTag.AddCssClass("floatRight");
            pRemoveTag.AddCssClass("indexRemoveGlyph");

            TagBuilder aRemoveTag = new TagBuilder("a");
            aRemoveTag.MergeAttribute("data-toggle", "tooltip");
            aRemoveTag.MergeAttribute("title", "Delete");
            aRemoveTag.AddCssClass("removeThumbIcon");

            aRemoveTag.MergeAttribute("href", "#");

            TagBuilder spanRemoveIconTag = new TagBuilder("span");
            spanRemoveIconTag.MergeAttribute("class", "glyphicon glyphicon-remove");
            spanRemoveIconTag.MergeAttribute("aria-hidden", "true");

            aRemoveTag.InnerHtml += spanRemoveIconTag;
            pRemoveTag.InnerHtml += aRemoveTag;

            capDiv.InnerHtml += pEditTag;
            capDiv.InnerHtml += pRemoveTag;
            thumbDiv.InnerHtml += capDiv;

            return thumbDiv.ToString();
        }

        public string CreateTfsLogTable(List<KeyValuePair<string, string>> files)
        {
            TagBuilder tableTag = new TagBuilder("table");
            TagBuilder trHead = new TagBuilder("tr");
            TagBuilder th1 = new TagBuilder("th");
            th1.InnerHtml = "Date";

            TagBuilder th2 = new TagBuilder("th");
            th2.InnerHtml = "API";

            TagBuilder th3 = new TagBuilder("th");
            th3.InnerHtml = "Type";

            trHead.InnerHtml += th1;
            trHead.InnerHtml += th2;
            trHead.InnerHtml += th3;
            tableTag.InnerHtml += trHead;

            foreach (var item in files)
            {
                TagBuilder trTag = new TagBuilder("tr");

                TagBuilder tdTag1 = new TagBuilder("td");
                tdTag1.InnerHtml = item.Key;
                trTag.InnerHtml += tdTag1;

                TagBuilder tdTag2 = new TagBuilder("td");
                tdTag2.InnerHtml = WikiBlob.GetAsWikiLink(item.Value);
                trTag.InnerHtml += tdTag2;

                TagBuilder tdTag3 = new TagBuilder("td");
                tdTag3.InnerHtml = WikiBlob.GetPartOfBlobName(item.Value, 3);
                trTag.InnerHtml += tdTag3;

                tableTag.InnerHtml += trTag;
            }

            return tableTag.ToString();
        }

        /// <summary>
        /// Creates the page nav.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns></returns>
        public string CreatePageNav(string content)
        {
            StringBuilder bl = new StringBuilder();

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(content);

            HtmlNodeCollection headers = doc.DocumentNode.SelectNodes("//h3 | //h4");

            if (headers == null) return "";

            HtmlNode prevWord = null;
            int idCount = 0;
            bl.Append(@"<p class=""pageNavHeader"">Content<p>");
            bl.Append(@"<ol class=""pageNav"">");
            foreach (var word in headers)
            {
                if (word.Name == "h3")
                {
                    if (prevWord != null && prevWord.Name == "h4")
                    {
                        bl.Append(@"</ul>");
                    }

                    bl.Append(@"<li id=""" + idCount++ + @"pageNav"">");
                    bl.Append(word.InnerText);
                    bl.Append(@"</li>");
                    prevWord = word;
                }
                else if (word.Name == "h4")
                {
                    if (prevWord != null && prevWord.Name == "h3")
                    {
                        bl.Append(@"<ul>");
                    }

                    bl.Append(@"<li id=""" + idCount++ + @"pageNav"">");
                    bl.Append(word.InnerText);
                    bl.Append(@"</li>");
                    prevWord = word;
                }
            }
            bl.Append(@"</ol>");
            return bl.ToString();
        }

		public static string CreateHTMLMenu(MenuModel menuObj)
		{
			TagBuilder topUl = new TagBuilder("ul");

			foreach (var item in menuObj.items)
			{
				TagBuilder topLi = CreateLiForMenu(item, "list-group-item", "linkHeader mainHeader", "projectInternalLinks manualLinkTree");
				
				foreach (var secondLevelItem in item.Children)
				{
					TagBuilder secondLi = CreateLiForMenu(secondLevelItem, "", "", "");

					foreach (var thirdLevelItem in secondLevelItem.Children)
					{
						TagBuilder thirdLi = CreateLiForMenu(thirdLevelItem, "", "", "", true);
						secondLi.InnerHtml += thirdLi;
					}

					topLi.InnerHtml += secondLi;
				}

				topUl.InnerHtml += topLi;
			}

			return topUl.InnerHtml;
		}

		public static string CalculateMenuIdLI(string name)
		{
			return "#" + WikiBlob.SanitizeV2(name) + "LinkTreeMenuLi";
		}

		public static string CalculateMenuIdA(string name)
		{
			return "#" + WikiBlob.SanitizeV2(name) + "LinkTreeMenuA";
		}

		public static string CalculateMenuIdUl(string name)
		{
			return "#" + WikiBlob.SanitizeV2(name) + "LinkTreeMenuUl";
		}

		private static TagBuilder CreateLiForMenu(MenuItem item, string liClasses, string aClasses, string ulClasses, bool lastLevel = false)
		{
			TagBuilder liTag = new TagBuilder("li");
			liTag.MergeAttribute("class", liClasses);
			liTag.MergeAttribute("id", CalculateMenuIdLI(item.Name));

			TagBuilder headerLink = new TagBuilder("a");
			headerLink.MergeAttribute("class", aClasses);
			headerLink.MergeAttribute("id", CalculateMenuIdA(item.Name));
			liTag.InnerHtml += headerLink;

			if (!lastLevel)
			{
				TagBuilder ulTag = new TagBuilder("ul");
				ulTag.MergeAttribute("id", CalculateMenuIdUl(item.Name));
				ulTag.MergeAttribute("class", ulClasses);
				liTag.InnerHtml += ulTag;
			}

			return liTag;
		}
	}
}