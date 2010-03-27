using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ReadabilityNet
{
    public class Readability
    {
        private readonly HtmlDocument _doc;
        private string _content;
        private StringBuilder _contentContainer;
        private int _highestScore = -1;

        #region -- Constructors --

        /// <summary>
        /// Initializes a new instance of the <see cref="Readability"/> class. Accepts a string of HTML content to parse.
        /// </summary>
        /// <param name="content">The content.</param>
        public Readability(string content)
        {
            _content = content;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Readability"/> class. Accepts a full HTML document to parse.
        /// </summary>
        /// <param name="doc">The doc.</param>
        public Readability(HtmlDocument doc)
        {
            _doc = doc;
        }

        #endregion

        #region -- Public Methods --

        /// <summary>
        /// Parses the HTML content
        /// </summary>
        /// <returns></returns>
        public HtmlNode Parse()
        {
            Regex rgx;
            string pattern;
            bool malformedContent = false;
            var contentBlocks = new List<HtmlNode>();

            /* replace all <br /> tags with <p /> tags */
            pattern = @"/<br[^>]*>\s| *<br[^>]*>";
            rgx = new Regex(pattern);
            _doc.DocumentNode.InnerHtml = rgx.Replace(_doc.DocumentNode.InnerHtml, "<p />");

            /* remove all <font> tags */
            pattern = @"/<\/?font[^>]*>";
            rgx = new Regex(pattern);
            _doc.DocumentNode.InnerHtml = rgx.Replace(_doc.DocumentNode.InnerHtml, string.Empty);

            HtmlNode articleContent = _doc.CreateElement("DIV");

            //pattern = @"<P>.*</P>";
            //rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            //var paragraphs = rgx.Matches(_doc.DocumentNode.InnerHtml);
            HtmlNodeCollection paragraphs = _doc.DocumentNode.SelectNodes("//p");

            if (paragraphs.Count == 0)
            {
                //pattern = @"<DIV>.*</DIV>";
                //rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                //paragraphs = rgx.Matches(_content);
                paragraphs = _doc.DocumentNode.SelectNodes("//div");

                malformedContent = true;
            }

            /* iterate over each paragraph */
            foreach (HtmlNode node in paragraphs)
            {
                HtmlNode parentNode = node.ParentNode;

                if (parentNode.Readability == null)
                {
                    parentNode.Readability = new HtmlAgilityPack.Readability(0);
                }

                parentNode.Readability.ContentScore = DetermineContentScore(parentNode.Readability.ContentScore,
                                                                            parentNode, node);

                if (parentNode.Readability.ContentScore > 0)
                {
                    bool found = (from b in contentBlocks
                                  where b == parentNode
                                  select b).Any();

                    if (!found)
                    {
                        contentBlocks.Add(parentNode);
                    }
                }
            }

            RemoveScripts();
            RemoveStylesheets();
            RemoveStyles();

            int numContentBlocks = contentBlocks.Count - 1;
            for (int i = numContentBlocks; i >= 0; i--)
            {
                HtmlNode contentElement = contentBlocks[i];

                if (_highestScore < 20 && contentElement.Readability.ContentScore < _highestScore)
                {
                    contentBlocks.RemoveAt(i);
                }
                else if (_highestScore > 20 && contentElement.Readability.ContentScore < 20)
                {
                    contentBlocks.RemoveAt(i);
                }
            }

            if (contentBlocks.Count > 1)
            {
                numContentBlocks = contentBlocks.Count - 1;
                for (int i = numContentBlocks; i >= 0; i--)
                {
                    HtmlNode contentElement = contentBlocks[i];

                    if (hasAnyDescendant(contentElement, contentBlocks))
                    {
                        contentBlocks.RemoveAt(i);
                    }
                }
            }

            foreach (HtmlNode contentElement in contentBlocks)
            {
                removeElementStyles(contentElement);

                removeBreaks(contentElement);

                if (!malformedContent)
                {
                    RemoveNonContentElement(contentElement, "div");
                }

                RemoveElementByMinWords(contentElement, "form", 1000000);
                RemoveElementByMinWords(contentElement, "object", 1000000);
                RemoveElementByMinWords(contentElement, "table", 250);
                RemoveElementByMinWords(contentElement, "h1", 1000000);
                RemoveElementByMinWords(contentElement, "h2", 1000000);
                RemoveElementByMinWords(contentElement, "iframe", 1000000);

                contentElement.InnerHtml = RemoveAllTagsButLeaveContent(contentElement);

                articleContent.AppendChild(contentElement);
            }

            if (contentBlocks.Count == 0)
            {
                articleContent = _doc.CreateElement("DIV");
                articleContent.InnerHtml = "Sorry, readability was unable to parse this page.";
            }

            return articleContent;
        }

        #endregion

        /// <summary>
        /// Removes all the HTML tags from the document, but leaves the content that was in the tags.
        /// </summary>
        /// <param name="contentElement">The content element.</param>
        /// <returns></returns>
        private string RemoveAllTagsButLeaveContent(HtmlNode contentElement)
        {
            var rgx = new Regex(@"<[a-zA-Z\/][^>]*>");

            MatchCollection matches = rgx.Matches(contentElement.InnerHtml);

            return rgx.Replace(contentElement.InnerHtml, string.Empty);
        }

        /// <summary>
        /// Removes all the scripts tags (but not their content) from the document.
        /// </summary>
        private void RemoveScripts()
        {
            HtmlNodeCollection scripts = _doc.DocumentNode.SelectNodes("//script");

            if (scripts != null)
            {
                int numScripts = scripts.Count - 1;

                for (int i = numScripts; i >= 0; i--)
                {
                    HtmlNode script = scripts[i];

                    script.ParentNode.RemoveChild(script);
                }
            }
        }

        /// <summary>
        /// Removes all the stylesheets (but not their content from the document.
        /// </summary>
        private void RemoveStylesheets()
        {
            HtmlNodeCollection scripts = _doc.DocumentNode.SelectNodes("//link");

            if (scripts != null)
            {
                int numScripts = scripts.Count - 1;

                for (int i = numScripts; i >= 0; i--)
                {
                    HtmlNode script = scripts[i];

                    script.ParentNode.RemoveChild(script);
                }
            }
        }

        /// <summary>
        /// Removes all the styles (but not their content from the document.
        /// </summary>
        private void RemoveStyles()
        {
            HtmlNodeCollection styles = _doc.DocumentNode.SelectNodes("//style");

            if (styles != null)
            {
                int startIndex = styles.Count - 1;

                for (int i = startIndex; i >= 0; i--)
                {
                    HtmlNode style = styles[i];

                    if (style.ParentNode != null)
                    {
                        style.ParentNode.RemoveChild(style);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(style.InnerText))
                        {
                            style.InnerText = string.Empty;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the node has any descendents.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="descendents">The descendents.</param>
        /// <returns>
        /// 	<c>true</c> if [has any descendant] [the specified element]; otherwise, <c>false</c>.
        /// </returns>
        private bool hasAnyDescendant(HtmlNode element, List<HtmlNode> descendents)
        {
            /* return all nodes */
            HtmlNodeCollection elements = element.SelectNodes("*");

            foreach (HtmlNode node in descendents)
            {
                if (descendents.IndexOf(element) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes line breaks from the document.
        /// </summary>
        /// <param name="element">The element.</param>
        private void removeBreaks(HtmlNode element)
        {
            Regex rgx;

            rgx = new Regex(@"/((<br[^>]*>)[\s]*(<br[^>]*>)){1,}");

            element.InnerHtml = rgx.Replace(element.InnerHtml, "<br />");
        }

        private void removeElementStyles(HtmlNode element)
        {
            if (element == null)
            {
                return;
            }

            element.Attributes.Remove("function");
            element.Attributes.Remove("style");

            HtmlNode childElement = element.FirstChild;

            while (childElement != null)
            {
                if (childElement.NodeType == HtmlNodeType.Element)
                {
                    element.Attributes.Remove("function");
                    childElement.Attributes.Remove("style");

                    removeElementStyles(childElement);
                }

                childElement = childElement.NextSibling;
            }
        }

        /// <summary>
        /// Removes the non content elements from the document.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="tagName">Name of the tag.</param>
        private void RemoveNonContentElement(HtmlNode element, string tagName)
        {
            HtmlNodeCollection elements = element.SelectNodes(tagName);

            if (elements != null)
            {
                int numElements = elements.Count - 1;

                for (int i = numElements; i >= 0; i--)
                {
                    HtmlNode descendent = elements[i];

                    int p = descendent.SelectNodes("p") == null ? 0 : descendent.SelectNodes("p").Count;
                    int img = descendent.SelectNodes("img") == null ? 0 : descendent.SelectNodes("img").Count;
                    int li = descendent.SelectNodes("li") == null ? 0 : descendent.SelectNodes("li").Count;
                    int a = descendent.SelectNodes("a") == null ? 0 : descendent.SelectNodes("a").Count;
                    int embed = descendent.SelectNodes("embed") == null ? 0 : descendent.SelectNodes("embed").Count;

                    if (a == 0 && embed == 0 && img == 0 && li == 0 && p == 0)
                    {
                        HtmlNodeCollection children = descendent.SelectNodes("*");
                        bool containsOnlyText = true;

                        if (children != null)
                        {
                            for (int j = 0; j < children.Count; j++)
                            {
                                HtmlNode child = children[j];

                                if (child.NodeType == HtmlNodeType.Element)
                                {
                                    containsOnlyText = false;
                                    break;
                                }

                                if (!containsOnlyText)
                                {
                                    descendent.ParentNode.RemoveChild(descendent);
                                }

                                continue;
                            }
                        }
                    }
                    else
                    {
                        var badKeywords = new List<string>
                                              {
                                                  "ad",
                                                  "captcha",
                                                  "classified",
                                                  "clear",
                                                  "comment",
                                                  "footer",
                                                  "footnote",
                                                  "leftcolumn",
                                                  "listing",
                                                  "menu",
                                                  "meta",
                                                  "module",
                                                  "nav",
                                                  "navbar",
                                                  "rightcolumn",
                                                  "sidebar",
                                                  "sponsor",
                                                  "tab",
                                                  "tag",
                                                  "toolbar",
                                                  "tools",
                                                  "trackback",
                                                  "tweetback",
                                                  "widget",
                                              };

                        foreach (string j in badKeywords)
                        {
                            if (descendent.Id != null && descendent.Attributes["class"] != null)
                            {
                                if (descendent.Id.ToLower().IndexOf(j) >= 0 ||
                                    descendent.Attributes["class"].Value.ToLower().IndexOf(j) >= 0)
                                {
                                    descendent.ParentNode.RemoveChild(descendent);
                                    descendent = null;
                                    break;
                                }
                            }
                        }
                    }

                    if (descendent == null)
                    {
                        continue;
                    }

                    if (GetWordCount(descendent) < 25)
                    {
                        if (img > p || li > p || a > p || p == 0 || embed > 0)
                        {
                            descendent.ParentNode.RemoveChild(descendent);
                        }
                    }
                }
            }
        }

        private void RemoveElementByMinWords(HtmlNode element, string tagName, int minWords)
        {
            HtmlNodeCollection elements = element.SelectNodes("//" + tagName);

            if (elements != null)
            {
                int numElements = elements.Count - 1;

                for (int i = numElements; i >= 0; i--)
                {
                    HtmlNode target = elements[i];

                    if (GetWordCount(target) < minWords)
                    {
                        target.ParentNode.RemoveChild(target);
                    }
                }
            }
        }

        private int DetermineContentScore(int score, HtmlNode parentNode, HtmlNode element)
        {
            var goodKeywords = new List<string>
                                   {
                                       "article",
                                       "body",
                                       "content",
                                       "entry",
                                       "hentry",
                                       "post",
                                       "story",
                                       "text"
                                   };
            var semiGoodKeywords = new List<string>
                                       {
                                           "area",
                                           "container",
                                           "inner",
                                           "main",
                                       };
            var badKeywords = new List<string>
                                  {
                                      "ad",
                                      "captcha",
                                      "classified",
                                      "comment",
                                      "footer",
                                      "footnote",
                                      "leftcolumn",
                                      "listing",
                                      "menu",
                                      "meta",
                                      "module",
                                      "nav",
                                      "navbar",
                                      "rightcolumn",
                                      "sidebar",
                                      "sponsor",
                                      "tab",
                                      "toolbar",
                                      "tools",
                                      "trackback",
                                      "widget",
                                  };

            string className = parentNode.Attributes["class"] == null
                                   ? string.Empty
                                   : parentNode.Attributes["class"].Value.ToLower();
            string id = parentNode.Id == null ? string.Empty : parentNode.Id.ToLower();

            foreach (string goodKeyword in goodKeywords)
            {
                if (className.IndexOf(goodKeyword) >= 0)
                {
                    score++;
                }
                if (id.IndexOf(goodKeyword) >= 0)
                {
                    score++;
                }
            }

            if (score >= 1)
            {
                foreach (string semiGoodKeyword in semiGoodKeywords)
                {
                    if (className.IndexOf(semiGoodKeyword) >= 0)
                    {
                        score++;
                    }
                    if (id.IndexOf(semiGoodKeyword) >= 0)
                    {
                        score++;
                    }
                }
            }

            foreach (string badKeyword in badKeywords)
            {
                if (className.IndexOf(badKeyword) >= 0)
                {
                    score = score - 15;
                }
                if (id.IndexOf(badKeyword) >= 0)
                {
                    score = score - 15;
                }
            }

            if (element.Name.ToLower() == "p" && GetWordCount(element) > 20)
            {
                score++;
            }

            if (score > _highestScore)
            {
                _highestScore = score;
            }

            return score;
        }

        /// <summary>
        /// Gets the word count.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns></returns>
        private int GetWordCount(HtmlNode element)
        {
            return Trim(Normalize(GetText(element))).Split(Convert.ToChar(" ")).Length;
        }

        /// <summary>
        /// Trims the specified paragraph.
        /// </summary>
        /// <param name="paragraph">The paragraph.</param>
        /// <returns></returns>
        private string Trim(string paragraph)
        {
            var regex = new Regex(@"/^\s+|\s+$");
            paragraph = regex.Replace(paragraph, string.Empty);

            return paragraph;
        }

        /// <summary>
        /// Normalizes the specified paragraph.
        /// </summary>
        /// <param name="paragraph">The paragraph.</param>
        /// <returns></returns>
        private string Normalize(string paragraph)
        {
            var regex = new Regex(@"/\s{2,}");
            paragraph = regex.Replace(paragraph, " ");

            return paragraph;
        }

        /// <summary>
        /// Gets the text.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns></returns>
        private string GetText(HtmlNode element)
        {
            //TODO: This may be wrong
            //if (element.NodeType != HtmlNodeType.Comment)
            //{
            //    return element.InnerText;
            //}
            //else
            //{
            //    return element.
            //}

            return element.InnerText;
        }
    }
}