// -------------------------------------------------------------------------------------------------
// <copyright file="RelationMatrixComponentTestFixture.cs" company="Starion Group S.A.">
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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Bunit;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web.Virtualization;
    using Microsoft.Extensions.DependencyInjection;

    using NUnit.Framework;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using reqifviewer.Components;

    using TestContext = Bunit.TestContext;

    /// <summary>
    /// Tests for <see cref="RelationMatrixComponent"/>: the self-contained matrix view that owns
    /// the async build, render caches, scroll-restoration JS interop, and the dismissible
    /// large-matrix tip.
    /// </summary>
    [TestFixture]
    public class RelationMatrixComponentTestFixture
    {
        private TestContext context;
        private ReqIF reqIf;

        [SetUp]
        public async Task SetUp()
        {
            this.context = new TestContext();
            this.context.JSInterop.Mode = JSRuntimeMode.Loose;

            var reqifPath = Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestData", "ProR_Traceability-Template-v1.0.reqif");
            var cts = new CancellationTokenSource();

            await using var fileStream = new FileStream(reqifPath, FileMode.Open);
            var loader = new ReqIFLoaderService(new ReqIFDeserializer());
            await loader.LoadAsync(fileStream, SupportedFileExtensionKind.Reqif, cts.Token);
            this.reqIf = loader.ReqIFData.Single();
        }

        [TearDown]
        public void TearDown()
        {
            this.context.Dispose();
        }

        [Test]
        public void Verify_that_component_renders_Virtualize_for_a_loaded_relation_set()
        {
            var relation = this.reqIf.CoreContent.SpecRelations.First(r => r.Source != null && r.Target != null);

            var renderer = this.context.RenderComponent<RelationMatrixComponent>(p => p
                .Add(x => x.ReqIf, this.reqIf)
                .Add(x => x.RowType, relation.Source.Type)
                .Add(x => x.ColumnType, relation.Target.Type)
                .Add(x => x.RelationType, relation.Type)
                .Add(x => x.ShowOnlyRelated, false));

            // OnParametersSetAsync runs the build via Task.Run; wait for it to commit
            // visible rows before asserting on the rendered Virtualize element.
            renderer.WaitForState(() => !renderer.Instance.IsBusy && renderer.Instance.VisibleRows.Count > 0);
            renderer.Render();

            var virtualizeComponents = renderer.FindComponents<Virtualize<SpecObject>>();
            Assert.That(virtualizeComponents, Is.Not.Empty,
                "Row axis must be rendered through Virtualize<SpecObject> for large-matrix performance");
        }

        [Test]
        public void Verify_that_component_calls_matrixScroll_attach_when_a_matrix_renders()
        {
            var relation = this.reqIf.CoreContent.SpecRelations.First(r => r.Source != null && r.Target != null);

            var expectedKey =
                $"matrix-scroll:{this.reqIf.TheHeader.Identifier}"
                + $":{relation.Source.Type.Identifier}"
                + $":{relation.Target.Type.Identifier}"
                + $":{relation.Type.Identifier}"
                + ":all";

            var renderer = this.context.RenderComponent<RelationMatrixComponent>(p => p
                .Add(x => x.ReqIf, this.reqIf)
                .Add(x => x.RowType, relation.Source.Type)
                .Add(x => x.ColumnType, relation.Target.Type)
                .Add(x => x.RelationType, relation.Type)
                .Add(x => x.ShowOnlyRelated, false)
                .Add(x => x.ScrollKey, expectedKey));

            renderer.WaitForState(() => !renderer.Instance.IsBusy && renderer.Instance.VisibleRows.Count > 0);

            // After the matrix commits, OnAfterRenderAsync should have called matrixScroll.attach.
            renderer.WaitForState(() => this.context.JSInterop.Invocations.Any(i => i.Identifier == "matrixScroll.attach"));

            var attach = this.context.JSInterop.Invocations
                .Where(i => i.Identifier == "matrixScroll.attach")
                .Last();

            Assert.Multiple(() =>
            {
                Assert.That(attach.Arguments[0], Is.Not.EqualTo(".relation-matrix-wrapper"),
                    "First argument must no longer be the legacy shared class selector");
                Assert.That(attach.Arguments[0], Is.InstanceOf<ElementReference>(),
                    "First argument must be the component instance's wrapper ElementReference");
                Assert.That(attach.Arguments[1], Is.EqualTo(expectedKey),
                    "Scroll key must come from the ScrollKey parameter");
                Assert.That(attach.Arguments[2], Is.EqualTo(32),
                    "Third argument is the row-height-in-px used by JS for index <-> scrollTop math");
                Assert.That(attach.Arguments[3], Is.EqualTo(36),
                    "Fourth argument is the cell-width-in-px used by JS for index <-> scrollLeft math");
                Assert.That(attach.Arguments[4], Is.EqualTo(
                        renderer.Instance.VisibleRows.Select(o => o.Identifier).ToArray()),
                    "Fifth argument is the ordered row SpecObject identifiers for index<->id mapping");
                Assert.That(attach.Arguments[5], Is.EqualTo(
                        renderer.Instance.VisibleColumns.Select(o => o.Identifier).ToArray()),
                    "Sixth argument is the ordered column SpecObject identifiers for index<->id mapping");
            });
        }

        [Test]
        public void Verify_that_component_dismisses_the_large_matrix_hint_after_OnDismiss()
        {
            // A loaded ReqIF satisfies the component's "matrix is loaded" gating; we don't
            // care which selectors — we'll overwrite VisibleRows/Columns via reflection to
            // force the hint into the "would render" state regardless of the real fixture size.
            var relation = this.reqIf.CoreContent.SpecRelations.First(r => r.Source != null && r.Target != null);

            var renderer = this.context.RenderComponent<RelationMatrixComponent>(p => p
                .Add(x => x.ReqIf, this.reqIf)
                .Add(x => x.RowType, relation.Source.Type)
                .Add(x => x.ColumnType, relation.Target.Type)
                .Add(x => x.RelationType, relation.Type)
                .Add(x => x.ShowOnlyRelated, false));

            renderer.WaitForState(() => !renderer.Instance.IsBusy);

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var componentType = typeof(RelationMatrixComponent);

            // Force a state where the hint *would* render: 80 x 80 = 6_400 visible cells
            // (above the 5_000 LargeMatrixCellCount soft cap) with Show-only-related off.
            var largeList = (ICollection<SpecObject>)Enumerable.Repeat<SpecObject>(null, 80).ToList();
            componentType.GetProperty("VisibleRows", flags)!.SetValue(renderer.Instance, largeList);
            componentType.GetProperty("VisibleColumns", flags)!.SetValue(renderer.Instance, largeList);

            var showHint = componentType.GetProperty("ShowLargeMatrixHint", flags)!;
            Assert.That((bool)showHint.GetValue(renderer.Instance)!, Is.True,
                "Pre-condition: with 6_400 cells visible and Show-only-related off, the hint must be shown");

            var dismiss = componentType.GetMethod("OnDismissLargeMatrixHint", flags)!;
            dismiss.Invoke(renderer.Instance, null);

            Assert.That((bool)showHint.GetValue(renderer.Instance)!, Is.False,
                "After OnDismissLargeMatrixHint fires, ShowLargeMatrixHint must stay false even though the matrix is still large");
        }

        [Test]
        public void Verify_that_component_fits_the_wrapper_to_the_viewport_when_a_matrix_renders()
        {
            var relation = this.reqIf.CoreContent.SpecRelations.First(r => r.Source != null && r.Target != null);

            var renderer = this.context.RenderComponent<RelationMatrixComponent>(p => p
                .Add(x => x.ReqIf, this.reqIf)
                .Add(x => x.RowType, relation.Source.Type)
                .Add(x => x.ColumnType, relation.Target.Type)
                .Add(x => x.RelationType, relation.Type)
                .Add(x => x.ShowOnlyRelated, false)
                .Add(x => x.ScrollKey, "k"));

            renderer.WaitForState(() => !renderer.Instance.IsBusy && renderer.Instance.VisibleRows.Count > 0);
            renderer.WaitForState(() => this.context.JSInterop.Invocations.Any(i => i.Identifier == "matrixScroll.fit"));

            var fit = this.context.JSInterop.Invocations
                .Last(i => i.Identifier == "matrixScroll.fit");

            Assert.That(fit.Arguments[0], Is.InstanceOf<ElementReference>(),
                "fit must target the component's wrapper ElementReference");
        }
    }
}
