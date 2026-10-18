// -------------------------------------------------------------------------------------------------
// <copyright file="SpecElementWithAttributesExtensionTestFixture.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Tests.ReqIFExtensions
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;

    using NUnit.Framework;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using ReqifViewer.ReqIFExtensions;

    /// <summary>
    /// Suite of tests for the <see cref="SpecElementWithAttributesExtensions"/>
    /// </summary>
    [TestFixture]
    public class SpecElementWithAttributesExtensionTestFixture
    {
        private ReqIF reqIf;

        [SetUp]
        public async Task SetUp()
        {
            var reqIfDeserializer = new ReqIFDeserializer();

            var cts = new CancellationTokenSource();

            var reqifPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "ProR_Traceability-Template-v1.0.reqif");

            await using var fileStream = new FileStream(reqifPath, FileMode.Open);
            var reqIfLoaderService = new ReqIFLoaderService(reqIfDeserializer);
            await reqIfLoaderService.LoadAsync(fileStream, SupportedFileExtensionKind.Reqif, cts.Token);

            this.reqIf = reqIfLoaderService.ReqIFData.Single();
        }

        [Test]
        public void Verify_that_ExtractSpecificationName_returns_expected_result()
        {
            // The first attribute value is XHTML: it is sanitized and the namespace prefix is removed
            var specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.Identifier == "_o7scQ6dbEeafNduaIhMwQg");
            Assert.That(specObject.ExtractDisplayName(), Is.EqualTo(new MarkupString("<div>Stakeholder Requirements</div>")));

            var specification = this.reqIf.CoreContent.Specifications.Single(x => x.Identifier == "_o7scS6dbEeafNduaIhMwQg");
            Assert.That(specification.ExtractDisplayName(), Is.EqualTo(new MarkupString("Requirements Document")));

            specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.Identifier == "_DqMpsKddEeafNduaIhMwQg");
            Assert.That(specObject.ExtractDisplayName(), Is.EqualTo(new MarkupString("REQ-6")));
        }

        [Test]
        public void Verify_that_ExtractDisplayName_encodes_text_and_sanitizes_XHTML()
        {
            var withLongName = new SpecObject { Identifier = "object-1", LongName = "<img src=x onerror=alert(1)>" };
            Assert.That(withLongName.ExtractDisplayName().Value, Is.EqualTo("&lt;img src=x onerror=alert(1)&gt;"));

            var withIdentifierOnly = new SpecObject { Identifier = "<b>id</b>" };
            Assert.That(withIdentifierOnly.ExtractDisplayName().Value, Is.EqualTo("&lt;b&gt;id&lt;/b&gt;"));

            var withXhtml = new SpecObject { Identifier = "object-2" };
            withXhtml.Values.Add(new AttributeValueXHTML { TheValue = "<xhtml:div xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">Text<xhtml:img src=\"x\" onerror=\"alert(1)\" /></xhtml:div>" });
            Assert.That(withXhtml.ExtractDisplayName().Value, Is.EqualTo("<div>Text</div>"));

            var withString = new SpecObject { Identifier = "object-3" };
            withString.Values.Add(new AttributeValueString { TheValue = "<script>alert(1)</script>" });
            Assert.That(withString.ExtractDisplayName().Value, Is.EqualTo("&lt;script&gt;alert(1)&lt;/script&gt;"));
        }

        [Test]
        public void Verify_that_ExtractDisplayText_returns_plain_text_in_the_same_order_as_ExtractDisplayName()
        {
            // The first attribute value is XHTML: its text is returned without markup
            var specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.Identifier == "_o7scQ6dbEeafNduaIhMwQg");
            Assert.That(specObject.ExtractDisplayText(), Is.EqualTo("Stakeholder Requirements"));

            var specification = this.reqIf.CoreContent.Specifications.Single(x => x.Identifier == "_o7scS6dbEeafNduaIhMwQg");
            Assert.That(specification.ExtractDisplayText(), Is.EqualTo("Requirements Document"));

            specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.Identifier == "_DqMpsKddEeafNduaIhMwQg");
            Assert.That(specObject.ExtractDisplayText(), Is.EqualTo("REQ-6"));
        }

        [Test]
        public void Verify_that_ExtractDisplayText_falls_back_to_the_identifier()
        {
            var withoutValues = new Specification { Identifier = "spec-1" };
            Assert.That(withoutValues.ExtractDisplayText(), Is.EqualTo("spec-1"));

            var withEmptyXhtml = new SpecObject { Identifier = "object-1" };
            withEmptyXhtml.Values.Add(new AttributeValueXHTML { TheValue = "<xhtml:div xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">  </xhtml:div>" });
            Assert.That(withEmptyXhtml.ExtractDisplayText(), Is.EqualTo("object-1"));
        }

        [Test]
        public void Verify_that_ExtractDisplayText_collapses_whitespace()
        {
            var specObject = new SpecObject { Identifier = "object-1" };
            specObject.Values.Add(new AttributeValueXHTML { TheValue = "<xhtml:div xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">\n  First   line\n  <xhtml:b>bold</xhtml:b>\n</xhtml:div>" });

            Assert.That(specObject.ExtractDisplayText(), Is.EqualTo("First line bold"));
        }

        [Test]
        public void Verify_that_ExtractDisplayText_handles_XHTML_values_that_are_not_well_formed()
        {
            // Plain text without a root element: ReqIFSharp's ExtractUnformattedTextFromValue throws on this
            var textOnly = new SpecObject { Identifier = "object-1" };
            textOnly.Values.Add(new AttributeValueXHTML { TheValue = "plain text without an element" });
            Assert.That(textOnly.ExtractDisplayText(), Is.EqualTo("plain text without an element"));

            var twoParagraphs = new SpecObject { Identifier = "object-2" };
            twoParagraphs.Values.Add(new AttributeValueXHTML { TheValue = "<xhtml:p xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">first</xhtml:p><xhtml:p xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">second</xhtml:p>" });
            Assert.That(twoParagraphs.ExtractDisplayText(), Is.EqualTo("first second"));
        }

        [Test]
        public void Verify_that_ExtractDisplayName_falls_back_to_the_identifier_when_nothing_visible_is_left()
        {
            var emptyLongNameAndNullValue = new SpecObject { Identifier = "object-1", LongName = "" };
            emptyLongNameAndNullValue.Values.Add(new AttributeValueString { TheValue = null });
            Assert.That(emptyLongNameAndNullValue.ExtractDisplayName().Value, Is.EqualTo("object-1"));

            var onlyScript = new SpecObject { Identifier = "object-2" };
            onlyScript.Values.Add(new AttributeValueXHTML { TheValue = "<xhtml:div xmlns:xhtml=\"http://www.w3.org/1999/xhtml\"><xhtml:script>alert(1)</xhtml:script></xhtml:div>" });
            Assert.That(onlyScript.ExtractDisplayName().Value, Is.EqualTo("object-2"));

            var emptyString = new SpecObject { Identifier = "object-3" };
            emptyString.Values.Add(new AttributeValueString { TheValue = "   " });
            Assert.That(emptyString.ExtractDisplayName().Value, Is.EqualTo("object-3"));
        }

        [Test]
        public void Verify_that_ExtractDisplayName_keeps_safe_formatting_of_a_text_only_XHTML_value()
        {
            var textOnly = new SpecObject { Identifier = "object-1" };
            textOnly.Values.Add(new AttributeValueXHTML { TheValue = "plain & simple" });

            Assert.That(textOnly.ExtractDisplayName().Value, Is.EqualTo("plain &amp; simple"));
        }
    }
}
