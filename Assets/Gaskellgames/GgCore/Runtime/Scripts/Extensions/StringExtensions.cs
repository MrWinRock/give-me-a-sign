using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com, unless otherwise stated
    /// </summary>
    
    public static class StringExtensions
    {
        #region Nicify Name

        // Original code by ErnSur: https://gist.github.com/ErnSur/842d72606159fdd865979c4d9db21a18
        
        /// <summary>
        /// This function will insert spaces before capital letters and remove optional m_, _ or k followed by uppercase letter in front of the name.
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static string NicifyName(this string stringValue)
        {
            StringBuilder result = new StringBuilder(stringValue.Length * 2);

            bool prevIsLetter = false;
            bool prevIsLetterUpper = false;
            bool prevIsDigit = false;
            bool prevIsStartOfWord = false;
            bool prevIsNumberWord = false;

            int firstCharIndex = 0;
            if (stringValue.StartsWith('_'))
            {
                firstCharIndex = 1;
            }
            else if (stringValue.StartsWith("m_"))
            {
                firstCharIndex = 2;
            }

            for (int i = stringValue.Length - 1; i >= firstCharIndex; i--)
            {
                char currentChar = stringValue[i];
                bool currIsLetter = char.IsLetter(currentChar);
                if (i == firstCharIndex && currIsLetter)
                {
                    currentChar = char.ToUpper(currentChar);
                }
                bool currIsLetterUpper = char.IsUpper(currentChar);
                bool currIsDigit = char.IsDigit(currentChar);
                bool currIsSpacer = currentChar == ' ' || currentChar == '_';

                bool addSpace = (currIsLetter && !currIsLetterUpper && prevIsLetterUpper) ||
                                (currIsLetter && prevIsLetterUpper && prevIsStartOfWord) ||
                                (currIsDigit && prevIsStartOfWord) ||
                                (!currIsDigit && prevIsNumberWord) ||
                                (currIsLetter && !currIsLetterUpper && prevIsDigit);

                if (!currIsSpacer && addSpace)
                {
                    result.Insert(0, ' ');
                }

                result.Insert(0, currentChar);
                prevIsStartOfWord = currIsLetter && currIsLetterUpper && prevIsLetter && !prevIsLetterUpper;
                prevIsNumberWord = currIsDigit && prevIsLetter && !prevIsLetterUpper;
                prevIsLetterUpper = currIsLetter && currIsLetterUpper;
                prevIsLetter = currIsLetter;
                prevIsDigit = currIsDigit;
            }

            return result.ToString();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region SortListByLength
        
        /// <summary>
        /// Sort a list of strings by the string length.
        /// </summary>
        /// <param name="stringList"></param>
        /// <param name="longToShort"></param>
        /// <returns></returns>
        public static List<string> SortByLength(List<string> stringList, bool longToShort = false)
        {
            return longToShort
                ? stringList.OrderBy(s => -s.Length).ToList()
                : stringList.OrderBy(s => s.Length).ToList();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Get Bounds, Width or Height
        
        /// <summary>
        /// Get the width and height of a string using 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static Vector2 GetStringBounds(string stringValue, int fontSize = 0)
        {
            GUIStyle guiStyle = GUI.skin.GetStyle("Box");
            guiStyle.fontSize = fontSize;
    
            return guiStyle.CalcSize(new GUIContent(stringValue));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static float GetStringWidth(string stringValue, int fontSize = 0)
        {
            return GetStringBounds(stringValue, fontSize).x;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static float GetStringHeight(string stringValue, int fontSize = 0)
        {
            return GetStringBounds(stringValue, fontSize).y;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static float GetWrappedStringHeight(string stringValue, float maxWidth, int fontSize = 0)
        {
            GUIStyle guiStyle = GUI.skin.GetStyle("Box");
            guiStyle.fontSize = fontSize;
    
            return guiStyle.CalcHeight(new GUIContent(stringValue), maxWidth);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Nicify Number
        
        /// <summary>
        /// Return a three digit int as a 'nicified' string value for the number.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>0-9 will be returned as 000-009, 10-99 will be returned as 010-099</returns>
        public static string NicifyNumberAsString(int value)
        {
            // negatives
            if (value < 0) { return value.ToString(); }

            // 000-009
            if (value < 10) { return $"00{value}"; }

            // 010-099
            if (value < 100) { return $"0{value}"; }

            // 100+
            return value.ToString();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GetUntilOrEmpty
        
        /// <summary>
        /// Get substring upto a set character or full string, whichever comes first.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="stopAt"></param>
        /// <returns></returns>
        public static string GetUntilOrEmpty(this string text, string stopAt)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                int charLocation = text.IndexOf(stopAt, StringComparison.Ordinal);

                if (charLocation > 0)
                {
                    return text.Substring(0, charLocation);
                }
            }

            return string.Empty;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region RemoveBetween
        
        /// <summary>
        /// Remove the substring between two tags. Optionally keep or remove the tags.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="startTag"></param>
        /// <param name="endTag"></param>
        /// <param name="keepTags"></param>
        /// <returns></returns>
        public static string RemoveBetween(this string input, string startTag, string endTag, bool keepTags = false)
        {
            Regex regex = new Regex($"{Regex.Escape(startTag)}(.*?){Regex.Escape(endTag)}", RegexOptions.RightToLeft);
            string replacement = keepTags ? startTag + endTag : string.Empty;
            return regex.Replace(input, replacement);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region SplitByKeyword
        
        /// <summary>
        /// Split a string by keywords. (Keeps the keywords!)
        /// </summary>
        /// <param name="input"></param>
        /// <param name="keywords"></param>
        /// <returns></returns>
        public static string[] SplitByKeywords(string input, List<string> keywords)
        {
            string[] split = SplitByKeyword(input, keywords[0]);
            for (int i = 1; i < keywords.Count; i++)
            {
                string keyword = keywords[i];
                List<string> thisStepText = new List<string>();
                for (int j = 0; j < split.Length; j++)
                {
                    string[] substep = keywords.Contains(split[j])
                        ? new []{ split[j] }
                        : SplitByKeyword(split[j], keyword);
                    thisStepText.AddRange(substep);
                }
                split = thisStepText.ToArray();
            }
            return split;
        }
        
        /// <summary>
        /// Split a string by a keyword. (Keeps the keyword!)
        /// </summary>
        /// <param name="input"></param>
        /// <param name="keyword"></param>
        /// <returns></returns>
        public static string[] SplitByKeyword(string input, string keyword)
        {
            string pattern = "(" + keyword + ")";
            return Regex.Split(input, pattern);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region AddColorRichTextTagsToKeywords
        
        /// <summary>
        /// Adds color rich text tags to a string for specified keywords.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="keywordsDictionary"></param>
        /// <returns></returns>
        public static string AddColorRichTextTagsToKeywords(this string input, Dictionary<string, Color32> keywordsDictionary)
        {
            if (keywordsDictionary == null || keywordsDictionary.Count == 0) { return input; }
            
            // split string by keywords
            List<string> keywords = new List<string>();
            foreach (KeyValuePair<string, Color32> keyValuePair in keywordsDictionary)
            {
                keywords.Add(keyValuePair.Key);
            }
            keywords = SortByLength(keywords, true);
            string[] stepText = SplitByKeywords(input, keywords);
            
            // add colour to keywords
            StringBuilder sb = new StringBuilder();
            foreach (string substring in stepText)
            {
                sb.Append(keywordsDictionary.TryGetValue(substring, out Color32 color) ? AddColorRichTextTag(substring, color) : substring);
            }
            return sb.ToString();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region AddBoldRichTextTagsToKeywords
        
        /// <summary>
        /// Adds bold rich text tags to a string for specified keywords.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="keywords"></param>
        /// <returns></returns>
        public static string AddBoldRichTextTagsToKeywords(this string input, List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0) { return input; }
            
            // split string by keywords
            keywords = SortByLength(keywords, true);
            string[] stepText = SplitByKeywords(input, keywords);
            
            // add colour to keywords
            StringBuilder sb = new StringBuilder();
            foreach (string substring in stepText)
            {
                sb.Append(keywords.Contains(substring) ? substring.AddRichTextTag(RichTextTags.Bold) : substring);
            }
            return sb.ToString();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region RichTextTags

        /// <summary>
        /// Adds color rich text tags to a string.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public static string AddColorRichTextTag(string message, Color32 color)
        {
            string hexColor = $"#{color.r:X2}{color.g:X2}{color.b:X2}";
            string coloredString = $"<color={hexColor}>{message}</color>";
            return coloredString;
        }
        
        /// <summary>
        /// Adds rich text tags to a string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static string AddRichTextTag(this string input, RichTextTags tag)
        {
            string prefix = tag.GetRichTextTagPrefix(false) + ">";
            string suffix = tag.GetRichTextTagPrefix(true) + ">";
            
            return $"{prefix}{input}{suffix}";
        }
        
        /// <summary>
        /// Removes all rich text tags from a string.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string RemoveAllRichTextTags(this string input)
        {
            Regex rich = new Regex(@"<[^>]*>");
            if (rich.IsMatch(input))
            {
                input = rich.Replace(input, string.Empty);
            }
            return input;
        }
        
        /// <summary>
        /// Removes specified rich text tags from a string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static string RemoveRichTextTags(this string input, RichTextTags[] tags)
        {
            if (tags == null || tags.Length == 0) { return input; }
            if (string.IsNullOrEmpty(input)) { return input; }
            
            foreach (RichTextTags tag in tags)
            {
                input = input.RemoveRichTextTag(tag);
            }
            return input;
        }
        
        /// <summary>
        /// Removes the specified rich text tag from a string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static string RemoveRichTextTag(this string input, RichTextTags tag)
        {
            string prefix = tag.GetRichTextTagPrefix(false);
            string suffix = tag.GetRichTextTagPrefix(true);
            
            Regex regexPrefix = new Regex(@$"{prefix}[^>]*>");
            Regex regexSuffix = new Regex(@$"{suffix}[^>]*>");
            
            if (regexPrefix.IsMatch(input)) { input = regexPrefix.Replace(input, string.Empty); }
            if (regexSuffix.IsMatch(input)) { input = regexSuffix.Replace(input, string.Empty); }
            
            return input;
        }
        
        private static string GetRichTextTagPrefix(this RichTextTags tag, bool isSuffix)
        {
            switch (tag)
            {
                case RichTextTags.Align:
                    return (isSuffix ? "</" : "<") + "align";
                case RichTextTags.AllCaps:
                    return (isSuffix ? "</" : "<") + "allcaps";
                case RichTextTags.Alpha:
                    return (isSuffix ? "</" : "<") + "alpha";
                case RichTextTags.Bold:
                    return (isSuffix ? "</" : "<") + "b";
                case RichTextTags.LineBreak:
                    return (isSuffix ? "</" : "<") + "br";
                case RichTextTags.Color:
                    return (isSuffix ? "</" : "<") + "color";
                case RichTextTags.CSpace:
                    return (isSuffix ? "</" : "<") + "cspace";
                case RichTextTags.Font:
                    return (isSuffix ? "</" : "<") + "font";
                case RichTextTags.FontWeight:
                    return (isSuffix ? "</" : "<") + "font-weight";
                case RichTextTags.Gradient:
                    return (isSuffix ? "</" : "<") + "gradient";
                case RichTextTags.Italic:
                    return (isSuffix ? "</" : "<") + "i";
                case RichTextTags.Indent:
                    return (isSuffix ? "</" : "<") + "indent";
                case RichTextTags.LineHeight:
                    return (isSuffix ? "</" : "<") + "line-height";
                case RichTextTags.LineIndent:
                    return (isSuffix ? "</" : "<") + "line-indent";
                case RichTextTags.Link:
                    return (isSuffix ? "</" : "<") + "link";
                case RichTextTags.Lowercase:
                    return (isSuffix ? "</" : "<") + "lowercase";
                case RichTextTags.Margin:
                    return (isSuffix ? "</" : "<") + "margin";
                case RichTextTags.Mark:
                    return (isSuffix ? "</" : "<") + "mark";
                case RichTextTags.MSpace:
                    return (isSuffix ? "</" : "<") + "mspace";
                case RichTextTags.NoLineBreak:
                    return (isSuffix ? "</" : "<") + "nobr";
                case RichTextTags.NoParse:
                    return (isSuffix ? "</" : "<") + "noparse";
                case RichTextTags.Page:
                    return (isSuffix ? "</" : "<") + "page";
                case RichTextTags.Pos:
                    return (isSuffix ? "</" : "<") + "pos";
                case RichTextTags.Rotate:
                    return (isSuffix ? "</" : "<") + "rotate";
                case RichTextTags.Strike:
                    return (isSuffix ? "</" : "<") + "s";
                case RichTextTags.Size:
                    return (isSuffix ? "</" : "<") + "size";
                case RichTextTags.SmallCaps:
                    return (isSuffix ? "</" : "<") + "smallcaps";
                case RichTextTags.Space:
                    return (isSuffix ? "</" : "<") + "space";
                case RichTextTags.Sprite:
                    return (isSuffix ? "</" : "<") + "sprite";
                case RichTextTags.Strikethrough:
                    return (isSuffix ? "</" : "<") + "strikethrough";
                case RichTextTags.Style:
                    return (isSuffix ? "</" : "<") + "style";
                case RichTextTags.Subscript:
                    return (isSuffix ? "</" : "<") + "sub";
                case RichTextTags.Superscript:
                    return (isSuffix ? "</" : "<") + "sup";
                case RichTextTags.Underline:
                    return (isSuffix ? "</" : "<") + "u";
                case RichTextTags.Uppercase:
                    return (isSuffix ? "</" : "<") + "uppercase";
                case RichTextTags.VOffset:
                    return (isSuffix ? "</" : "<") + "voffset";
                case RichTextTags.Width:
                    return isSuffix ? "</width>" : "<width";
                default:
                    return (isSuffix ? "</" : "<") + "";
            }
        }
        
        #endregion
        
    } // class end
}
