// -------------------------------------------------------------------------------------------------
// <copyright file="SideMenuTestFixture.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using BlazorBlueprint.Components;

    using Bunit;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using reqifviewer.Shared;

    using TestContext = Bunit.TestContext;

    /// <summary>
    /// Suite of tests for the <see cref="SideMenu"/> component
    /// </summary>
    [TestFixture]
    public class SideMenuTestFixture
    {
        private Mock<IReqIFLoaderService> reqIfLoaderService;

        private TestContext context;

        [SetUp]
        public void SetUp()
        {
            this.context = new TestContext();
            this.context.JSInterop.Mode = JSRuntimeMode.Loose;
            this.context.Services.AddBlazorBlueprintComponents();

            this.reqIfLoaderService = new Mock<IReqIFLoaderService>();
            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns([]);
            this.context.Services.AddSingleton(this.reqIfLoaderService.Object);
        }

        [TearDown]
        public void TearDown()
        {
            this.context.Dispose();
        }

        /// <summary>
        /// Renders the <see cref="SideMenu"/> inside the <see cref="BbSidebarProvider"/> that it needs
        /// </summary>
        private IRenderedComponent<BbSidebarProvider> RenderSideMenu()
        {
            return this.context.RenderComponent<BbSidebarProvider>(parameters => parameters.AddChildContent<SideMenu>());
        }

        /// <summary>
        /// Creates a <see cref="ReqIF"/> with one <see cref="Specification"/>, with the references between the objects set
        /// as the deserializer sets them
        /// </summary>
        private static ReqIF CreateReqIf(string identifier, string title, string specificationLongName = "Requirements")
        {
            var content = new ReqIFContent();
            var reqIf = new ReqIF
            {
                TheHeader = new ReqIFHeader { Identifier = identifier, Title = title },
                CoreContent = content
            };

            content.DocumentRoot = reqIf;
            content.Specifications.Add(new Specification { Identifier = $"{identifier}-spec", LongName = specificationLongName, ReqIFContent = content });

            return reqIf;
        }

        /// <summary>
        /// Opens the collapsible "Types" group, whose items are only rendered when it is open
        /// </summary>
        private static void OpenTypesGroup(IRenderedComponent<BbSidebarProvider> renderer)
        {
            foreach (var button in renderer.FindAll("[data-sidebar=\"menu-button\"]").Where(x => x.TextContent.Trim() == "Types").ToList())
            {
                button.Click();
            }
        }

        private static List<string> GroupLabels(IRenderedComponent<BbSidebarProvider> renderer)
        {
            return renderer.FindAll("[data-sidebar=\"group-label\"]").Select(x => x.TextContent.Trim()).ToList();
        }

        private static List<string> Hrefs(IRenderedComponent<BbSidebarProvider> renderer)
        {
            return renderer.FindAll("a[href]").Select(x => x.GetAttribute("href")).ToList();
        }

        [Test]
        public void Verify_that_SideMenu_renders_links_for_each_loaded_ReqIF()
        {
            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns([CreateReqIf("doc-a", "Document A"), CreateReqIf("doc-b", "Document B")]);

            var renderer = this.RenderSideMenu();
            OpenTypesGroup(renderer);

            Assert.That(GroupLabels(renderer), Is.EqualTo(new[] { "Document A", "Document B" }));

            var hrefs = Hrefs(renderer);

            foreach (var id in new[] { "doc-a", "doc-b" })
            {
                Assert.That(hrefs, Does.Contain($"reqifstatistics/{id}"));
                Assert.That(hrefs, Does.Contain($"/reqif/{id}/specification/{id}-spec"));

                foreach (var page in new[] { "specobjects", "specrelations", "relationmatrix", "externalobjects", "datatypedefinitions" })
                {
                    Assert.That(hrefs, Does.Contain($"reqif/{id}/{page}"));
                }

                foreach (var type in new[] { "SpecObjectType", "SpecificationType", "SpecRelationType", "RelationGroupType" })
                {
                    Assert.That(hrefs, Does.Contain($"reqif/{id}/spectypes?type={type}"));
                }
            }
        }

        [Test]
        public async Task Verify_that_SideMenu_shows_documents_when_ReqIfChanged_is_raised()
        {
            var renderer = this.RenderSideMenu();

            Assert.That(GroupLabels(renderer), Is.Empty);

            IEnumerable<ReqIF> loaded = [CreateReqIf("doc-a", "Document A")];

            // Loads finish on a thread pool thread, not on the renderer's thread
            await Task.Run(() => this.reqIfLoaderService.Raise(x => x.ReqIfChanged += null, this.reqIfLoaderService.Object, loaded));

            renderer.WaitForAssertion(() =>
            {
                Assert.That(GroupLabels(renderer), Is.EqualTo(new[] { "Document A" }));
                Assert.That(Hrefs(renderer), Does.Contain("reqifstatistics/doc-a"));
            });

            await Task.Run(() => this.reqIfLoaderService.Raise(x => x.ReqIfChanged += null, this.reqIfLoaderService.Object, (IEnumerable<ReqIF>)null));
            renderer.WaitForAssertion(() => Assert.That(GroupLabels(renderer), Is.Empty));

            await Task.Run(() => this.reqIfLoaderService.Raise(x => x.ReqIfChanged += null, this.reqIfLoaderService.Object, loaded));
            renderer.WaitForAssertion(() => Assert.That(GroupLabels(renderer), Has.Count.EqualTo(1)));

            await Task.Run(() => this.reqIfLoaderService.Raise(x => x.ReqIfChanged += null, this.reqIfLoaderService.Object, Enumerable.Empty<ReqIF>()));
            renderer.WaitForAssertion(() => Assert.That(GroupLabels(renderer), Is.Empty));
        }

        [Test]
        public void Verify_that_SideMenu_unsubscribes_from_ReqIfChanged_on_dispose()
        {
            this.RenderSideMenu();

            this.reqIfLoaderService.VerifyAdd(x => x.ReqIfChanged += It.IsAny<EventHandler<IEnumerable<ReqIF>>>(), Times.Once);
            this.reqIfLoaderService.VerifyRemove(x => x.ReqIfChanged -= It.IsAny<EventHandler<IEnumerable<ReqIF>>>(), Times.Never);

            this.context.DisposeComponents();

            this.reqIfLoaderService.VerifyRemove(x => x.ReqIfChanged -= It.IsAny<EventHandler<IEnumerable<ReqIF>>>(), Times.Once);
        }

        [Test]
        public async Task Verify_that_SideMenu_ignores_null_ReqIF_entries()
        {
            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns([null, CreateReqIf("doc-a", "Document A")]);

            var renderer = this.RenderSideMenu();

            Assert.That(GroupLabels(renderer), Is.EqualTo(new[] { "Document A" }));

            IEnumerable<ReqIF> onlyNull = [null];
            await Task.Run(() => this.reqIfLoaderService.Raise(x => x.ReqIfChanged += null, this.reqIfLoaderService.Object, onlyNull));

            renderer.WaitForAssertion(() => Assert.That(GroupLabels(renderer), Is.Empty));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Verify_that_SideMenu_shows_fallback_label_when_title_is_empty(string title)
        {
            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns([CreateReqIf("doc-a", title)]);

            var renderer = this.RenderSideMenu();

            Assert.That(GroupLabels(renderer), Is.EqualTo(new[] { "ReqIF document" }));
        }

        [Test]
        public void Verify_that_SideMenu_shows_the_title_when_it_is_set()
        {
            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns([CreateReqIf("doc-a", "Coffee machine")]);

            var renderer = this.RenderSideMenu();

            Assert.That(GroupLabels(renderer), Is.EqualTo(new[] { "Coffee machine" }));
        }

        [TestCase("Requirements", "Requirements")]
        [TestCase("", "doc-a-spec")]
        [TestCase(null, "doc-a-spec")]
        public void Verify_that_SideMenu_shows_the_display_text_of_specifications(string longName, string expected)
        {
            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns([CreateReqIf("doc-a", "Document A", longName)]);

            var renderer = this.RenderSideMenu();

            var specificationItem = renderer.Find("a[href=\"/reqif/doc-a/specification/doc-a-spec\"]");

            Assert.That(specificationItem.TextContent.Trim(), Is.EqualTo(expected));
        }

        [Test]
        public void Verify_that_SideMenu_footer_shows_version_without_source_revision()
        {
            var renderer = this.RenderSideMenu();

            var version = renderer.Find(".side-menu-footer span").TextContent.Trim();

            Assert.That(version, Does.Match(@"^Version \d+\.\d+\.\d+(-[\w.]+)?$"));
            Assert.That(version, Does.Not.Contain("+"));
        }

        [Test]
        public void Verify_that_only_Open_a_file_is_active_on_the_home_page()
        {
            var renderer = this.RenderSideMenu();

            var active = renderer.FindAll("[data-sidebar=\"menu-button\"][data-active=\"true\"]").Select(x => x.TextContent.Trim()).ToList();

            Assert.That(active, Is.EqualTo(new[] { "Open a file" }));
        }
    }
}
