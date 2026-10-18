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
    }
}
