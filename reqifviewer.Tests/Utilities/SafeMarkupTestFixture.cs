// -------------------------------------------------------------------------------------------------
// <copyright file="SafeMarkupTestFixture.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Tests.Utilities
{
    using NUnit.Framework;

    using reqifviewer.Utilities;

    /// <summary>
    /// Suite of tests for the <see cref="SafeMarkup"/> class
    /// </summary>
    [TestFixture]
    public class SafeMarkupTestFixture
    {
        private const string Xhtml = "xmlns:xhtml=\"http://www.w3.org/1999/xhtml\"";

        [Test]
        public void Verify_that_ReqIF_formatting_is_kept_without_namespace_prefixes()
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:div {Xhtml}><xhtml:p>The machine shall <xhtml:b>brew</xhtml:b> <xhtml:i>coffee</xhtml:i>.</xhtml:p><xhtml:ul><xhtml:li>one</xhtml:li></xhtml:ul></xhtml:div>");

            Assert.That(markup.Value, Is.EqualTo("<div><p>The machine shall <b>brew</b> <i>coffee</i>.</p><ul><li>one</li></ul></div>"));
        }

        [Test]
        public void Verify_that_tables_and_their_spans_are_kept()
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:table {Xhtml}><xhtml:tr><xhtml:th colspan=\"2\">Head</xhtml:th></xhtml:tr><xhtml:tr><xhtml:td>a</xhtml:td><xhtml:td>b</xhtml:td></xhtml:tr></xhtml:table>");

            Assert.That(markup.Value, Does.Contain("<th colspan=\"2\">Head</th>"));
            Assert.That(markup.Value, Does.Contain("<td>a</td><td>b</td>"));
        }

        [TestCase("<div xmlns=\"http://www.w3.org/1999/xhtml\"><p>default namespace</p></div>", "<div><p>default namespace</p></div>")]
        [TestCase("<reqif-xhtml:div xmlns:reqif-xhtml=\"http://www.w3.org/1999/xhtml\"><reqif-xhtml:p>other prefix</reqif-xhtml:p></reqif-xhtml:div>", "<div><p>other prefix</p></div>")]
        public void Verify_that_other_namespace_forms_are_handled(string xhtml, string expected)
        {
            Assert.That(SafeMarkup.FromXhtml(xhtml).Value, Is.EqualTo(expected));
        }

        [Test]
        public void Verify_that_scripts_are_removed_with_their_content()
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:div {Xhtml}>before<xhtml:script>alert('xss')</xhtml:script>after</xhtml:div>");

            Assert.That(markup.Value, Is.EqualTo("<div>beforeafter</div>"));
        }

        [TestCase("<xhtml:img src=\"x\" onerror=\"alert(1)\" />")]
        [TestCase("<xhtml:p onclick=\"alert(1)\" onmouseover=\"alert(2)\">text</xhtml:p>")]
        [TestCase("<xhtml:svg><xhtml:script>alert(1)</xhtml:script></xhtml:svg>")]
        [TestCase("<svg onload=\"alert(1)\"></svg>")]
        [TestCase("<xhtml:iframe src=\"https://example.com\"></xhtml:iframe>")]
        [TestCase("<xhtml:a href=\"javascript:alert(1)\">link</xhtml:a>")]
        [TestCase("<xhtml:a href=\"JaVaScRiPt:alert(1)\">link</xhtml:a>")]
        [TestCase("<xhtml:a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">link</xhtml:a>")]
        [TestCase("<xhtml:span style=\"background-image: url(javascript:alert(1))\">text</xhtml:span>")]
        [TestCase("<xhtml:form action=\"https://example.com\"><xhtml:input name=\"password\" /></xhtml:form>")]
        [TestCase("<xhtml:span style=\"color: expression(alert(1))\">text</xhtml:span>")]
        [TestCase("<xhtml:span style=\"background-color: url(javascript:alert(1))\">text</xhtml:span>")]
        [TestCase("<xhtml:a href=\"&#106;avascript:alert(1)\">entity encoded</xhtml:a>")]
        [TestCase("<xhtml:a href=\" javascript:alert(1)\">leading space</xhtml:a>")]
        [TestCase("<xhtml:blockquote cite=\"javascript:alert(1)\">quote</xhtml:blockquote>")]
        public void Verify_that_executable_content_is_removed(string attack)
        {
            var value = SafeMarkup.FromXhtml($"<xhtml:div {Xhtml}>{attack}</xhtml:div>").Value.ToLowerInvariant();

            Assert.That(value, Does.Not.Contain("<script"));
            Assert.That(value, Does.Not.Contain("alert("));
            Assert.That(value, Does.Not.Contain(" on"));
            Assert.That(value, Does.Not.Contain("javascript:"));
            Assert.That(value, Does.Not.Contain("data:"));
            Assert.That(value, Does.Not.Contain("<iframe"));
            Assert.That(value, Does.Not.Contain("<svg"));
            Assert.That(value, Does.Not.Contain("<img"));
            Assert.That(value, Does.Not.Contain("<form"));
            Assert.That(value, Does.Not.Contain("<input"));
            Assert.That(value, Does.Not.Contain("expression("));
            Assert.That(value, Does.Not.Contain("url("));
        }

        [Test]
        public void Verify_that_safe_links_are_kept()
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:a {Xhtml} href=\"https://reqif.academy\">reqif.academy</xhtml:a>");

            Assert.That(markup.Value, Is.EqualTo("<a href=\"https://reqif.academy\">reqif.academy</a>"));
        }

        [Test]
        public void Verify_that_classes_and_positioning_styles_are_removed_and_formatting_styles_kept()
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:div {Xhtml} class=\"fixed inset-0\" id=\"main-content\" style=\"position: fixed; top: 0; z-index: 9999; color: red; font-weight: bold\">text</xhtml:div>");

            Assert.That(markup.Value, Does.Not.Contain("class="));
            Assert.That(markup.Value, Does.Not.Contain("id="));
            Assert.That(markup.Value, Does.Not.Contain("position"));
            Assert.That(markup.Value, Does.Not.Contain("z-index"));
            Assert.That(markup.Value, Does.Contain("color: rgba(255, 0, 0, 1)").Or.Contain("color: red"));
            Assert.That(markup.Value, Does.Contain("font-weight: bold"));
        }

        [Test]
        public void Verify_that_the_fallback_text_of_objects_is_kept()
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:div {Xhtml}><xhtml:object data=\"media/figure.png\" type=\"image/png\">Figure 1</xhtml:object></xhtml:div>");

            Assert.That(markup.Value, Is.EqualTo("<div>Figure 1</div>"));
        }

        [Test]
        public void Verify_that_escaped_markup_stays_text()
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:p {Xhtml}>&lt;script&gt;alert(1)&lt;/script&gt;</xhtml:p>");

            Assert.That(markup.Value, Is.EqualTo("<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>"));
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        public void Verify_that_empty_XHTML_gives_empty_markup(string xhtml, string expected)
        {
            Assert.That(SafeMarkup.FromXhtml(xhtml).Value, Is.EqualTo(expected));
        }

        [TestCase("<img src=x onerror=alert(1)>", "&lt;img src=x onerror=alert(1)&gt;")]
        [TestCase("Brakes & <steering>", "Brakes &amp; &lt;steering&gt;")]
        [TestCase(null, "")]
        public void Verify_that_text_is_encoded(string text, string expected)
        {
            Assert.That(SafeMarkup.FromText(text).Value, Is.EqualTo(expected));
        }

        [TestCase("style", "body { display: none }")]
        [TestCase("noscript", "<xhtml:p>no script</xhtml:p>")]
        [TestCase("textarea", "typed text")]
        [TestCase("title", "a title")]
        [TestCase("button", "Click")]
        [TestCase("svg", "<xhtml:text>drawing text</xhtml:text>")]
        public void Verify_that_non_text_elements_are_removed_with_their_content(string element, string content)
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:div {Xhtml}>before<xhtml:{element}>{content}</xhtml:{element}>after</xhtml:div>");

            Assert.That(markup.Value, Is.EqualTo("<div>beforeafter</div>"));
        }

        [TestCase("<xhtml:ol start=\"3\"><xhtml:li>x</xhtml:li></xhtml:ol>", "<ol start=\"3\"><li>x</li></ol>")]
        [TestCase("<xhtml:pre>a  b</xhtml:pre>", "<pre>a  b</pre>")]
        [TestCase("<xhtml:code>x = 1</xhtml:code>", "<code>x = 1</code>")]
        [TestCase("<xhtml:p>H<xhtml:sub>2</xhtml:sub>O and x<xhtml:sup>2</xhtml:sup></xhtml:p>", "<p>H<sub>2</sub>O and x<sup>2</sup></p>")]
        [TestCase("<xhtml:p><xhtml:u>u</xhtml:u><xhtml:s>s</xhtml:s><xhtml:del>del</xhtml:del><xhtml:ins>ins</xhtml:ins></xhtml:p>", "<p><u>u</u><s>s</s><del>del</del><ins>ins</ins></p>")]
        [TestCase("<xhtml:h1>Chapter</xhtml:h1>", "<h1>Chapter</h1>")]
        [TestCase("<xhtml:dl><xhtml:dt>term</xhtml:dt><xhtml:dd>definition</xhtml:dd></xhtml:dl>", "<dl><dt>term</dt><dd>definition</dd></dl>")]
        [TestCase("<xhtml:table><xhtml:tr><xhtml:td rowspan=\"2\">a</xhtml:td></xhtml:tr></xhtml:table>", "<table><tbody><tr><td rowspan=\"2\">a</td></tr></tbody></table>")]
        [TestCase("<xhtml:blockquote cite=\"https://example.com\">quote</xhtml:blockquote>", "<blockquote cite=\"https://example.com\">quote</blockquote>")]
        public void Verify_that_allowed_XHTML_elements_are_kept(string xhtml, string expected)
        {
            var markup = SafeMarkup.FromXhtml($"<xhtml:div {Xhtml}>{xhtml}</xhtml:div>");

            Assert.That(markup.Value, Is.EqualTo($"<div>{expected}</div>"));
        }

        [TestCase("mailto:a@b.c")]
        [TestCase("#section-2")]
        [TestCase("other.html")]
        [TestCase("/reqif/doc/specobject/req-1")]
        public void Verify_that_mailto_and_relative_links_are_kept(string href)
        {
            // Relative links stay within the viewer: an unknown address shows the not-found page
            var markup = SafeMarkup.FromXhtml($"<xhtml:a {Xhtml} href=\"{href}\">link</xhtml:a>");

            Assert.That(markup.Value, Is.EqualTo($"<a href=\"{href}\">link</a>"));
        }

        [TestCase("plain text without an element", "plain text without an element")]
        [TestCase("<xhtml:p " + Xhtml + ">first</xhtml:p><xhtml:p " + Xhtml + ">second</xhtml:p>", "first second")]
        [TestCase("<xhtml:div " + Xhtml + ">brew a cup in <xhtml:b>under 30 seconds</xhtml:b>.</xhtml:div>", "brew a cup in under 30 seconds.")]
        [TestCase("<xhtml:div " + Xhtml + ">\n  First   line\n  <xhtml:br />second &amp; third\n</xhtml:div>", "First line second & third")]
        [TestCase("<xhtml:div " + Xhtml + ">a<xhtml:script>alert(1)</xhtml:script>b</xhtml:div>", "ab")]
        [TestCase("<xhtml:div " + Xhtml + "><xhtml:object data=\"f.png\" type=\"image/png\">Figure 1</xhtml:object></xhtml:div>", "Figure 1")]
        [TestCase("<xhtml:div " + Xhtml + "><xhtml:script>alert(1)</xhtml:script></xhtml:div>", "")]
        [TestCase(null, "")]
        public void Verify_that_ToPlainText_returns_the_visible_text(string xhtml, string expected)
        {
            Assert.That(SafeMarkup.ToPlainText(xhtml), Is.EqualTo(expected));
        }
    }
}
