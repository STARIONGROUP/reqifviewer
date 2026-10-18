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
    }
}
