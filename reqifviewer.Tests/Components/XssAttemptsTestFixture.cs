// -------------------------------------------------------------------------------------------------
// <copyright file="XssAttemptsTestFixture.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Tests.Components
{
    using System.IO;
    using System.Linq;

    using Bunit;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    using ReqIFSharp;

    using reqifviewer.Components;

    using TestContext = Bunit.TestContext;

    /// <summary>
    /// Renders the content of TestData/xss-attempts.reqif, a ReqIF file whose names and texts contain script
    /// injection attempts, and checks that nothing executable reaches the page
    /// </summary>
    [TestFixture]
    public class XssAttemptsTestFixture
    {
        private TestContext context;

        private ReqIF reqIf;

        [SetUp]
        public void SetUp()
        {
            this.context = new TestContext();
            this.context.JSInterop.Mode = JSRuntimeMode.Loose;

            var path = Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestData", "xss-attempts.reqif");
            this.reqIf = new ReqIFDeserializer().Deserialize(path).Single();
        }

        [TearDown]
        public void TearDown()
        {
            this.context.Dispose();
        }

        /// <summary>
        /// Checks the rendered DOM of the grid cells, where the content of the file is shown: no executable
        /// elements, no event handler attributes, no javascript: links and no fixed positioning. Markup that is
        /// shown as text is fine. The grid's own buttons (filter, pager) are outside the cells.
        /// </summary>
        private static void AssertNothingExecutable<TComponent>(IRenderedComponent<TComponent> renderer) where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            var forbiddenElements = new[] { "script", "img", "svg", "iframe", "object", "embed", "form", "style" };

            var cells = renderer.FindAll("td");
            Assert.That(cells, Is.Not.Empty, "no grid cells rendered");

            foreach (var element in cells.Concat(cells.SelectMany(cell => cell.QuerySelectorAll("*"))))
            {
                var name = element.LocalName;

                Assert.That(forbiddenElements, Does.Not.Contain(name), $"<{name}> element rendered");

                foreach (var attribute in element.Attributes)
                {
                    Assert.That(attribute.Name.StartsWith("on", System.StringComparison.OrdinalIgnoreCase), Is.False, $"{attribute.Name} attribute on <{name}>");
                    Assert.That(attribute.Value, Does.Not.Contain("javascript:").IgnoreCase, $"javascript: in {attribute.Name} on <{name}>");

                    if (attribute.Name == "style")
                    {
                        Assert.That(attribute.Value, Does.Not.Contain("position").IgnoreCase, $"positioning style on <{name}>");
                    }
                }
            }
        }

        [Test]
        public void Verify_that_attribute_values_from_the_file_render_without_executable_content()
        {
            foreach (var specObject in this.reqIf.CoreContent.SpecObjects)
            {
                var renderer = this.context.RenderComponent<AttributeValuesComponent>(parameters => parameters.Add(x => x.AttributeValues, specObject.Values));

                AssertNothingExecutable(renderer);
            }
        }

        [Test]
        public void Verify_that_display_names_from_the_file_render_without_executable_content()
        {
            var renderer = this.context.RenderComponent<SpecObjectsComponent>(parameters => parameters.Add(x => x.SpecObjects, this.reqIf.CoreContent.SpecObjects));

            AssertNothingExecutable(renderer);

            // The markup in the long names is shown as text
            Assert.That(renderer.Markup, Does.Contain("&lt;img src=x onerror="));
        }

        [Test]
        public void Verify_that_legitimate_formatting_still_renders()
        {
            var legitimate = this.reqIf.CoreContent.SpecObjects.Single(x => x.LongName == "Legitimate formatting");

            var renderer = this.context.RenderComponent<AttributeValuesComponent>(parameters => parameters.Add(x => x.AttributeValues, legitimate.Values));

            Assert.That(renderer.Markup, Does.Contain("<b>brew</b>"));
            Assert.That(renderer.Markup, Does.Contain("<td>a</td>"));
            Assert.That(renderer.Markup, Does.Contain("<a href=\"https://reqif.academy\">a safe link</a>"));
        }

        /// <summary>
        /// Checks a part of the rendered DOM that only contains file content: no executable elements, no event
        /// handler attributes and no javascript: urls
        /// </summary>
        private static void AssertNothingExecutableIn(AngleSharp.Dom.IElement root)
        {
            var forbiddenElements = new[] { "script", "img", "svg", "iframe", "object", "embed", "form", "style" };

            foreach (var element in new[] { root }.Concat(root.QuerySelectorAll("*")))
            {
                Assert.That(forbiddenElements, Does.Not.Contain(element.LocalName), $"<{element.LocalName}> element rendered");

                foreach (var attribute in element.Attributes)
                {
                    Assert.That(attribute.Name.StartsWith("on", System.StringComparison.OrdinalIgnoreCase), Is.False, $"{attribute.Name} attribute on <{element.LocalName}>");
                    Assert.That(attribute.Value, Does.Not.Contain("javascript:").IgnoreCase, $"javascript: in {attribute.Name} on <{element.LocalName}>");
                }
            }
        }

        private Moq.Mock<ReqIFSharp.Extensions.Services.IReqIFLoaderService> RegisterLoaderService()
        {
            var loader = new Moq.Mock<ReqIFSharp.Extensions.Services.IReqIFLoaderService>();
            loader.Setup(x => x.ReqIFData).Returns([this.reqIf]);
            loader.Setup(x => x.QueryDataAsync(Moq.It.IsAny<ExternalObject>(), Moq.It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync("data:image/png;base64,iVBORw0KGgo=");
            this.context.Services.AddSingleton(loader.Object);
            return loader;
        }

        [TestCase("Event handler on an image", "Image:")]
        [TestCase("Script element", "Before")]
        [TestCase("SVG onload", "SVG")]
        public void Verify_that_the_selected_XHTML_value_renders_without_executable_content(string longName, string expectedText)
        {
            var specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.LongName == longName);

            var renderer = this.context.RenderComponent<AttributeValuesComponent>(parameters => parameters.Add(x => x.AttributeValues, specObject.Values));

            // Select the row of the XHTML value: its value cell is the second cell of the row
            var row = renderer.FindAll("tbody tr").Single(tr => tr.QuerySelectorAll("td").Length > 1 && tr.QuerySelectorAll("td")[1].TextContent.Contains(expectedText));
            row.QuerySelectorAll("td")[1].Click();

            var card = renderer.FindAll(".rz-card").Single(x => x.TextContent.Contains("Selected Value:"));

            Assert.That(card.TextContent, Does.Contain(expectedText));
            AssertNothingExecutableIn(card);
        }

        [Test]
        public void Verify_that_a_selected_string_value_is_shown_as_text()
        {
            var specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.Identifier == "_so-7");

            var renderer = this.context.RenderComponent<AttributeValuesComponent>(parameters => parameters.Add(x => x.AttributeValues, specObject.Values));

            var row = renderer.FindAll("tbody tr").Single(tr => tr.QuerySelectorAll("td").Length > 1 && tr.QuerySelectorAll("td")[1].TextContent.Contains("<script>"));
            row.QuerySelectorAll("td")[1].Click();

            var card = renderer.FindAll(".rz-card").Single(x => x.TextContent.Contains("Selected Value:"));

            Assert.That(card.TextContent, Does.Contain("<script>window.__xss"));
            Assert.That(card.InnerHtml, Does.Contain("&lt;script&gt;"));
            AssertNothingExecutableIn(card);
        }

        [Test]
        public void Verify_that_the_specification_tree_and_header_render_without_executable_content()
        {
            this.RegisterLoaderService();

            var renderer = this.context.RenderComponent<reqifviewer.Pages.Specification.SpecificationDetailsPage>(parameters => parameters
                .Add(x => x.ReqIF, this.reqIf.TheHeader.Identifier)
                .Add(x => x.Specification, "_spec"));

            var tree = renderer.Find(".rz-tree");

            Assert.That(tree.TextContent, Does.Contain("Legitimate formatting"));
            Assert.That(tree.TextContent, Does.Contain("<img src=x onerror="), "markup in a long name is shown as text");
            Assert.That(tree.TextContent, Does.Contain("Unnamed"), "the XHTML name of an object without a long name");
            AssertNothingExecutableIn(tree);

            var header = renderer.FindAll(".rz-card").First();
            Assert.That(header.TextContent, Does.Contain("XSS attempts"));
            AssertNothingExecutableIn(header);
        }

        [Test]
        public void Verify_that_an_XHTML_default_value_renders_without_executable_content()
        {
            var specType = this.reqIf.CoreContent.SpecTypes.Single(x => x.Identifier == "_sot");
            var textAttribute = (AttributeDefinitionXHTML)specType.SpecAttributes.Single(x => x.Identifier == "_ad-text");
            textAttribute.DefaultValue = new AttributeValueXHTML
            {
                TheValue = "<xhtml:div xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">Default <xhtml:b>text</xhtml:b><xhtml:script>window.__xss=1</xhtml:script><xhtml:img src=\"x\" onerror=\"window.__xss=1\" /></xhtml:div>"
            };

            this.RegisterLoaderService();

            var renderer = this.context.RenderComponent<reqifviewer.Pages.AttributeDefinition.AttributeDefinitionDetailsPage>(parameters => parameters
                .Add(x => x.ReqIF, this.reqIf.TheHeader.Identifier)
                .Add(x => x.SpecType, "_sot")
                .Add(x => x.Identifier, "_ad-text"));

            var defaultValue = renderer.FindAll("div").Last(x => x.TextContent.Trim().StartsWith("Default") && x.QuerySelector("b") != null);

            Assert.That(defaultValue.InnerHtml, Does.Contain("<b>text</b>"));
            AssertNothingExecutableIn(defaultValue);
        }

        [Test]
        public void Verify_that_the_caption_of_an_embedded_object_is_shown_as_text()
        {
            const string xhtml = "<xhtml:div xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">Figure &amp; caption &lt;b&gt;not bold&lt;/b&gt;<xhtml:object data=\"media/figure.png\" type=\"image/png\">alt</xhtml:object></xhtml:div>";
            var content = this.reqIf.CoreContent;
            var type = (SpecObjectType)content.SpecTypes.Single(x => x.Identifier == "_sot");
            var textAttribute = (AttributeDefinitionXHTML)type.SpecAttributes.Single(x => x.Identifier == "_ad-text");

            var withPicture = new SpecObject { Identifier = "_so-picture", LongName = "With a picture", Type = type, ReqIFContent = content };
            withPicture.Values.Add(new AttributeValueXHTML { Definition = textAttribute, TheValue = xhtml });
            content.SpecObjects.Add(withPicture);

            // Read the object back through the deserializer so its external objects and references are set as in a real file
            using var stream = new MemoryStream();
            new ReqIFSerializer().Serialize([this.reqIf], stream, SupportedFileExtensionKind.Reqif);
            this.reqIf = new ReqIFDeserializer().Deserialize(stream, SupportedFileExtensionKind.Reqif).Single();

            this.RegisterLoaderService();

            var renderer = this.context.RenderComponent<reqifviewer.Pages.SpecObject.SpecObjectDetailsPage>(parameters => parameters
                .Add(x => x.ReqIF, this.reqIf.TheHeader.Identifier)
                .Add(x => x.SpecObject, "_so-picture"));

            renderer.WaitForAssertion(() => Assert.That(renderer.Markup, Does.Contain("Embedded Objects")));

            var embedded = renderer.FindAll(".rz-card").Single(x => x.TextContent.Contains("Embedded Objects"));

            Assert.That(embedded.TextContent, Does.Contain("Figure & caption <b>not bold</b>"), "the caption is text, not markup");
            Assert.That(embedded.QuerySelector("b"), Is.Null);
        }
    }
}
