// -------------------------------------------------------------------------------------------------
// <copyright file="RelationMatrixPageTestFixture.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Tests.Pages.RelationMatrix
{
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Bunit;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    using Radzen.Blazor;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using reqifviewer.Components;
    using reqifviewer.Pages.RelationMatrix;

    using TestContext = Bunit.TestContext;

    /// <summary>
    /// Suite of tests for the <see cref="RelationMatrixPage"/> Blazor component. Tests that
    /// exercise the matrix itself (Virtualize, scroll-restoration JS interop, large-matrix tip
    /// dismissal) live in <see cref="ReqifViewer.Tests.Components.RelationMatrixComponentTestFixture"/>.
    /// </summary>
    [TestFixture]
    public class RelationMatrixPageTestFixture
    {
        private TestContext context;
        private Mock<IReqIFLoaderService> reqIfLoaderService;
        private ReqIF reqIf;

        [SetUp]
        public async Task SetUp()
        {
            this.context = new TestContext();
            this.context.JSInterop.Mode = JSRuntimeMode.Loose;
            this.reqIfLoaderService = new Mock<IReqIFLoaderService>();

            var reqifPath = Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestData", "ProR_Traceability-Template-v1.0.reqif");
            var cts = new CancellationTokenSource();

            await using var fileStream = new FileStream(reqifPath, FileMode.Open);
            var loader = new ReqIFLoaderService(new ReqIFDeserializer());
            await loader.LoadAsync(fileStream, SupportedFileExtensionKind.Reqif, cts.Token);
            this.reqIf = loader.ReqIFData.Single();

            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns(loader.ReqIFData);

            this.context.Services.AddSingleton(this.reqIfLoaderService.Object);
        }

        [TearDown]
        public void TearDown()
        {
            this.context.Dispose();
        }

        [Test]
        public void Verify_that_page_renders_pickers_and_hosts_the_matrix_component()
        {
            var renderer = this.context.RenderComponent<RelationMatrixPage>(p =>
                p.Add(x => x.Identifier, this.reqIf.TheHeader.Identifier));

            var dropDowns = renderer.FindComponents<RadzenDropDown<SpecObjectType>>();
            Assert.That(dropDowns, Has.Count.EqualTo(2), "Expected one row and one column SpecObjectType picker");

            var relationDropDowns = renderer.FindComponents<RadzenDropDown<SpecRelationType>>();
            Assert.That(relationDropDowns, Has.Count.EqualTo(1), "Expected exactly one SpecRelationType picker");

            var swapButtons = renderer.FindComponents<RadzenButton>();
            Assert.That(swapButtons, Is.Not.Empty, "Swap-axes button should render");

            var matrixComponents = renderer.FindComponents<RelationMatrixComponent>();
            Assert.That(matrixComponents, Has.Count.EqualTo(1),
                "Page must host exactly one RelationMatrixComponent below the picker card");
        }

        [Test]
        public void Verify_that_changing_the_ReqIF_identifier_resets_the_picker_selectors()
        {
            // Blazor reuses the page instance across /reqif/A/... -> /reqif/B/...; the
            // selectors must follow the new document, not keep A's type instances
            // (the builder compares SpecObject.Type by reference).
            var reqIfA = this.reqIf;
            var reqIfB = new ReqIFDeserializer().Deserialize(
                Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestData", "ProR_Traceability-Template-v1.0.reqif")).Single();
            reqIfB.TheHeader.Identifier = "reqif-switch-B";

            this.reqIfLoaderService.Setup(x => x.ReqIFData).Returns(new[] { reqIfA, reqIfB });

            var aObjectTypes = reqIfA.CoreContent.SpecTypes.OfType<SpecObjectType>().ToList();
            var bObjectTypes = reqIfB.CoreContent.SpecTypes.OfType<SpecObjectType>().ToList();
            var bRelationTypes = reqIfB.CoreContent.SpecTypes.OfType<SpecRelationType>().ToList();

            var renderer = this.context.RenderComponent<RelationMatrixPage>(p =>
                p.Add(x => x.Identifier, reqIfA.TheHeader.Identifier));

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var pageType = typeof(RelationMatrixPage);
            var rowProp = pageType.GetProperty("RowType", flags)!;
            var colProp = pageType.GetProperty("ColumnType", flags)!;
            var relProp = pageType.GetProperty("RelationType", flags)!;

            Assert.That(rowProp.GetValue(renderer.Instance), Is.SameAs(aObjectTypes.First()),
                "Pre-condition: page initially selects the first SpecObjectType of ReqIF A");

            renderer.SetParametersAndRender(p => p.Add(x => x.Identifier, reqIfB.TheHeader.Identifier));

            Assert.Multiple(() =>
            {
                Assert.That(bObjectTypes, Has.Member(rowProp.GetValue(renderer.Instance)),
                    "RowType must be reset to an instance from ReqIF B after the identifier changed");
                Assert.That(bObjectTypes, Has.Member(colProp.GetValue(renderer.Instance)),
                    "ColumnType must be reset to an instance from ReqIF B");
                Assert.That(bRelationTypes, Has.Member(relProp.GetValue(renderer.Instance)),
                    "RelationType must be reset to an instance from ReqIF B");
                Assert.That(aObjectTypes, Has.No.Member(rowProp.GetValue(renderer.Instance)),
                    "RowType must no longer reference a type instance from the previous ReqIF A");
            });
        }

        [Test]
        public void Verify_that_page_shows_a_friendly_message_when_the_ReqIF_identifier_is_unknown()
        {
            var renderer = this.context.RenderComponent<RelationMatrixPage>(p =>
                p.Add(x => x.Identifier, "no-such-id"));

            Assert.That(renderer.Markup, Does.Contain("No ReqIF with identifier"));
        }

        [Test]
        public void Verify_that_query_string_seeds_picker_state_on_initial_render()
        {
            var content = this.reqIf.CoreContent;
            var relation = content.SpecRelations.First(r => r.Source != null && r.Target != null);

            var nav = this.context.Services.GetRequiredService<NavigationManager>();
            nav.NavigateTo(
                $"/reqif/{this.reqIf.TheHeader.Identifier}/relationmatrix"
                + $"?row={relation.Source.Type.Identifier}"
                + $"&col={relation.Target.Type.Identifier}"
                + $"&rel={relation.Type.Identifier}"
                + "&related=false");

            var renderer = this.context.RenderComponent<RelationMatrixPage>(p =>
                p.Add(x => x.Identifier, this.reqIf.TheHeader.Identifier));

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var pageType = typeof(RelationMatrixPage);

            Assert.Multiple(() =>
            {
                Assert.That(pageType.GetProperty("RowType", flags)!.GetValue(renderer.Instance),
                    Is.SameAs(relation.Source.Type), "row query parameter must seed RowType");
                Assert.That(pageType.GetProperty("ColumnType", flags)!.GetValue(renderer.Instance),
                    Is.SameAs(relation.Target.Type), "col query parameter must seed ColumnType");
                Assert.That(pageType.GetProperty("RelationType", flags)!.GetValue(renderer.Instance),
                    Is.SameAs(relation.Type), "rel query parameter must seed RelationType");
                Assert.That(pageType.GetProperty("ShowOnlyRelated", flags)!.GetValue(renderer.Instance),
                    Is.EqualTo(false), "related=false in the URL must turn the ShowOnlyRelated filter off");
            });
        }

        [Test]
        public async Task Verify_that_changing_a_picker_pushes_query_string_to_the_URL()
        {
            var renderer = this.context.RenderComponent<RelationMatrixPage>(p =>
                p.Add(x => x.Identifier, this.reqIf.TheHeader.Identifier));

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var pageType = typeof(RelationMatrixPage);

            var onAxisChanged = pageType.GetMethod("OnAxisChanged", flags)!;
            await renderer.InvokeAsync(() => onAxisChanged.Invoke(renderer.Instance, null));

            var nav = this.context.Services.GetRequiredService<NavigationManager>();
            var rowType = (SpecObjectType)pageType.GetProperty("RowType", flags)!.GetValue(renderer.Instance)!;
            var colType = (SpecObjectType)pageType.GetProperty("ColumnType", flags)!.GetValue(renderer.Instance)!;
            var relType = (SpecRelationType)pageType.GetProperty("RelationType", flags)!.GetValue(renderer.Instance)!;

            Assert.Multiple(() =>
            {
                Assert.That(nav.Uri, Does.Contain($"/reqif/{this.reqIf.TheHeader.Identifier}/relationmatrix?"),
                    "URL must point back at the matrix page route");
                Assert.That(nav.Uri, Does.Contain($"row={rowType.Identifier}"),
                    "Row type identifier must be encoded in the URL");
                Assert.That(nav.Uri, Does.Contain($"col={colType.Identifier}"),
                    "Column type identifier must be encoded in the URL");
                Assert.That(nav.Uri, Does.Contain($"rel={relType.Identifier}"),
                    "Relation type identifier must be encoded in the URL");
                Assert.That(nav.Uri, Does.Contain("related="),
                    "ShowOnlyRelated flag must always be encoded after a picker has been touched");
            });
        }
    }
}
