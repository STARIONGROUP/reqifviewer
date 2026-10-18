// -------------------------------------------------------------------------------------------------
// <copyright file="SafeMarkup.cs" company="Starion Group S.A.">
//
//   Copyright 2021-2026 Starion Group S.A.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//
// </copyright>
// -------------------------------------------------------------------------------------------------

namespace reqifviewer.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text.RegularExpressions;

    using Ganss.Xss;

    using Microsoft.AspNetCore.Components;

    /// <summary>
    /// Turns content from a ReqIF file into markup that is safe to render.
    /// </summary>
    /// <remarks>
    /// Everything in a ReqIF file is untrusted: the file may come from anyone. Never wrap file content in
    /// <see cref="MarkupString"/> directly; use <see cref="FromXhtml"/> for XHTML and <see cref="FromText"/>
    /// for plain text.
    /// </remarks>
    public static partial class SafeMarkup
    {
        /// <summary>
        /// The elements of the XHTML modules that ReqIF allows (text, hypertext, list, presentation, edit and
        /// table), plus u and s, which tools commonly emit. The object module is left out: objects are shown
        /// by their fallback content until attachments are rendered as images.
        /// </summary>
        private static readonly string[] AllowedTags =
        [
            "abbr", "acronym", "address", "blockquote", "br", "cite", "code", "dfn", "div", "em", "h1", "h2", "h3",
            "h4", "h5", "h6", "kbd", "p", "pre", "q", "samp", "span", "strong", "var",
            "a",
            "dl", "dt", "dd", "ol", "ul", "li",
            "b", "big", "hr", "i", "small", "sub", "sup", "tt", "u", "s",
            "del", "ins",
            "caption", "table", "td", "th", "tr", "col", "colgroup", "tbody", "thead", "tfoot"
        ];

        /// <summary>
        /// The attributes that are kept. class and id are left out, so file content cannot pick up or
        /// interfere with the styles and elements of the application.
        /// </summary>
        private static readonly string[] AllowedAttributes =
        [
            "href", "title", "lang", "dir", "style", "abbr", "align", "valign", "colspan", "rowspan", "span",
            "headers", "scope", "summary", "cite", "datetime", "start", "type", "width", "height"
        ];

        /// <summary>
        /// The CSS properties that are kept in style attributes. Positioning and layering properties are left
        /// out, so file content cannot cover or restyle the rest of the page.
        /// </summary>
        private static readonly string[] AllowedCssProperties =
        [
            "color", "background-color", "font", "font-family", "font-size", "font-style", "font-weight",
            "font-variant", "text-decoration", "text-decoration-line", "text-decoration-style", "text-align",
            "text-indent", "text-transform", "vertical-align", "letter-spacing", "word-spacing", "line-height",
            "white-space", "list-style-type", "list-style-position",
            "margin", "margin-top", "margin-right", "margin-bottom", "margin-left",
            "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
            "border", "border-top", "border-right", "border-bottom", "border-left", "border-color",
            "border-style", "border-width", "border-collapse", "border-spacing",
            "width", "height", "min-width", "max-width"
        ];

        /// <summary>
        /// Elements whose content is not text that should be shown, and which are therefore removed together
        /// with their content
        /// </summary>
        private static readonly HashSet<string> RemovedWithContent = new(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "template", "noscript", "iframe", "frame", "frameset", "embed", "applet",
            "svg", "math", "head", "title", "textarea", "select", "button"
        };

        /// <summary>
        /// The configured sanitizer; <see cref="HtmlSanitizer.Sanitize(string, string, AngleSharp.IMarkupFormatter)"/>
        /// is safe to call from several threads once configured
        /// </summary>
        private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

        /// <summary>
        /// Creates markup from a plain text value: the text is HTML encoded, so it is shown as text
        /// </summary>
        /// <param name="text">the text, may be null</param>
        /// <returns>a <see cref="MarkupString"/> that renders the text</returns>
        public static MarkupString FromText(string text)
        {
            return new MarkupString(WebUtility.HtmlEncode(text ?? string.Empty));
        }

        /// <summary>
        /// Creates markup from an XHTML value of a ReqIF file: namespace prefixes are removed and everything
        /// outside the allow-list (scripts, event handlers, javascript: links, unknown elements, class names,
        /// positioning styles) is removed
        /// </summary>
        /// <param name="xhtml">the XHTML, for example the TheValue of an AttributeValueXHTML, may be null</param>
        /// <returns>a <see cref="MarkupString"/> that is safe to render</returns>
        public static MarkupString FromXhtml(string xhtml)
        {
            if (string.IsNullOrWhiteSpace(xhtml))
            {
                return new MarkupString(string.Empty);
            }

            var html = NamespacePrefix().Replace(xhtml, "<$1");

            return new MarkupString(Sanitizer.Sanitize(html));
        }

        /// <summary>
        /// Matches the namespace prefix of an element name in a start or end tag, for example the "xhtml:" of
        /// &lt;xhtml:p&gt; and &lt;/xhtml:p&gt;
        /// </summary>
        [GeneratedRegex(@"<(/?)[A-Za-z_][\w.-]*:(?=[A-Za-z_])")]
        private static partial Regex NamespacePrefix();

        /// <summary>
        /// Creates and configures the <see cref="HtmlSanitizer"/>
        /// </summary>
        /// <returns>the configured <see cref="HtmlSanitizer"/></returns>
        private static HtmlSanitizer CreateSanitizer()
        {
            var options = new HtmlSanitizerOptions
            {
                AllowedTags = new HashSet<string>(AllowedTags, StringComparer.OrdinalIgnoreCase),
                AllowedAttributes = new HashSet<string>(AllowedAttributes, StringComparer.OrdinalIgnoreCase),
                AllowedCssProperties = new HashSet<string>(AllowedCssProperties, StringComparer.OrdinalIgnoreCase),
                AllowedSchemes = new HashSet<string>(["http", "https", "mailto"], StringComparer.OrdinalIgnoreCase),
                UriAttributes = new HashSet<string>(["href", "cite"], StringComparer.OrdinalIgnoreCase)
            };

            // Keep the text of removed elements, so unknown formatting does not hide requirement text
            var sanitizer = new HtmlSanitizer(options) { KeepChildNodes = true };

            sanitizer.RemovingTag += (_, e) =>
            {
                if (RemovedWithContent.Contains(e.Tag.LocalName))
                {
                    e.Tag.Remove();
                    e.Cancel = true;
                }
            };

            return sanitizer;
        }
    }
}
