// -------------------------------------------------------------------------------------------------
// <copyright file="RelationMatrixExtensionsTestFixture.cs" company="Starion Group S.A.">
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
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using ReqifViewer.ReqIFExtensions;

    /// <summary>
    /// Suite of tests for <see cref="RelationMatrixExtensions"/>.
    /// </summary>
    [TestFixture]
    public class RelationMatrixExtensionsTestFixture
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
        public void Verify_that_BuildRelationMatrix_returns_empty_when_inputs_are_null()
        {
            var matrix = this.reqIf.CoreContent.BuildRelationMatrix(null, null, null);

            Assert.Multiple(() =>
            {
                Assert.That(matrix.Rows, Is.Empty);
                Assert.That(matrix.Columns, Is.Empty);
                Assert.That(matrix.Cells, Is.Empty);
            });
        }

        [Test]
        public void Verify_that_BuildRelationMatrix_filters_rows_and_columns_by_type()
        {
            var content = this.reqIf.CoreContent;
            var rowType = content.SpecTypes.OfType<SpecObjectType>().First();
            var columnType = content.SpecTypes.OfType<SpecObjectType>().First();
            var relationType = content.SpecTypes.OfType<SpecRelationType>().FirstOrDefault();

            var matrix = content.BuildRelationMatrix(rowType, columnType, relationType);

            var expectedRowCount = content.SpecObjects.Count(o => o.Type == rowType);

            Assert.Multiple(() =>
            {
                Assert.That(matrix.Rows, Has.Count.EqualTo(expectedRowCount));
                Assert.That(matrix.Columns, Has.Count.EqualTo(expectedRowCount));
                Assert.That(matrix.Rows, Is.All.Matches<SpecObject>(o => o.Type == rowType));
            });
        }

        [Test]
        public void Verify_that_BuildRelationMatrix_marks_Forward_for_existing_relation_and_Backward_when_axes_are_swapped()
        {
            var content = this.reqIf.CoreContent;

            var sampleRelation = content.SpecRelations.FirstOrDefault(r => r.Source != null && r.Target != null);
            Assume.That(sampleRelation, Is.Not.Null, "Sample ReqIF must contain at least one fully-formed SpecRelation");

            var rowType = sampleRelation.Source.Type;
            var columnType = sampleRelation.Target.Type;
            var relationType = sampleRelation.Type;

            var matrix = content.BuildRelationMatrix(rowType, columnType, relationType);

            Assert.That(
                matrix.Cells.TryGetValue((sampleRelation.Source.Identifier, sampleRelation.Target.Identifier), out var cell),
                Is.True,
                "Expected a cell for the sample relation's (source, target) pair");
            Assert.That(cell.Direction, Is.EqualTo(RelationDirection.Forward));
            Assert.That(cell.Forward, Is.SameAs(sampleRelation));

            var swapped = content.BuildRelationMatrix(columnType, rowType, relationType);

            Assert.That(
                swapped.Cells.TryGetValue((sampleRelation.Target.Identifier, sampleRelation.Source.Identifier), out var swappedCell),
                Is.True,
                "After swapping axes, the same relation must populate the (target, source) cell");
            Assert.That(swappedCell.Direction, Is.EqualTo(RelationDirection.Backward));
            Assert.That(swappedCell.Backward, Is.SameAs(sampleRelation));
        }

        [Test]
        public void Verify_that_ShowOnlyRelated_filter_keeps_exactly_the_rows_and_columns_present_in_cells()
        {
            var content = this.reqIf.CoreContent;

            var sampleRelation = content.SpecRelations.FirstOrDefault(r => r.Source != null && r.Target != null);
            Assume.That(sampleRelation, Is.Not.Null);

            var rowType = sampleRelation.Source.Type;
            var columnType = sampleRelation.Target.Type;
            var relationType = sampleRelation.Type;

            var matrix = content.BuildRelationMatrix(rowType, columnType, relationType);

            // Mirror the page's "Show only related" filter exactly.
            var connectedRowIds = new System.Collections.Generic.HashSet<string>(matrix.Cells.Keys.Select(k => k.rowId));
            var connectedColIds = new System.Collections.Generic.HashSet<string>(matrix.Cells.Keys.Select(k => k.columnId));
            var visibleRows = matrix.Rows.Where(r => connectedRowIds.Contains(r.Identifier)).ToList();
            var visibleColumns = matrix.Columns.Where(c => connectedColIds.Contains(c.Identifier)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(visibleRows.Count, Is.EqualTo(connectedRowIds.Count),
                    "Number of visible rows must equal number of distinct row identifiers across all cells");
                Assert.That(visibleColumns.Count, Is.EqualTo(connectedColIds.Count),
                    "Number of visible columns must equal number of distinct column identifiers across all cells");

                foreach (var row in visibleRows)
                {
                    Assert.That(
                        matrix.Cells.Keys.Any(k => k.rowId == row.Identifier),
                        Is.True,
                        $"Visible row {row.Identifier} must appear in at least one populated cell");
                }

                foreach (var column in visibleColumns)
                {
                    Assert.That(
                        matrix.Cells.Keys.Any(k => k.columnId == column.Identifier),
                        Is.True,
                        $"Visible column {column.Identifier} must appear in at least one populated cell");
                }

                // No row that is *not* connected sneaks through the filter.
                Assert.That(visibleRows.All(r => connectedRowIds.Contains(r.Identifier)), Is.True);
                Assert.That(visibleColumns.All(c => connectedColIds.Contains(c.Identifier)), Is.True);

                // Sanity bound: visible row count cannot exceed the count of distinct sources/targets that fall on the row axis.
                var sourcesOfRowType = content.SpecRelations
                    .Where(r => r.Type == relationType && r.Source != null && r.Source.Type == rowType)
                    .Select(r => r.Source.Identifier);
                var targetsOfRowType = content.SpecRelations
                    .Where(r => r.Type == relationType && r.Target != null && r.Target.Type == rowType)
                    .Select(r => r.Target.Identifier);
                var expectedRowCap = new System.Collections.Generic.HashSet<string>(sourcesOfRowType.Concat(targetsOfRowType)).Count;
                Assert.That(visibleRows.Count, Is.LessThanOrEqualTo(expectedRowCap),
                    "Filter must not produce more visible rows than there are distinct row-typed SpecObjects participating in any relation of the chosen type");
            });
        }

        [Test]
        public void Verify_that_BuildRelationMatrix_throws_when_cancellation_is_requested()
        {
            var content = this.reqIf.CoreContent;
            var rowType = content.SpecTypes.OfType<SpecObjectType>().First();
            var columnType = rowType;
            var relationType = content.SpecTypes.OfType<SpecRelationType>().FirstOrDefault();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.That(
                () => content.BuildRelationMatrix(rowType, columnType, relationType, cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void Verify_that_BuildRelationMatrix_only_includes_relations_of_the_requested_type()
        {
            var content = this.reqIf.CoreContent;
            var relationTypes = content.SpecTypes.OfType<SpecRelationType>().ToList();
            Assume.That(relationTypes, Is.Not.Empty);

            var pickedType = relationTypes.First();
            var rowType = content.SpecTypes.OfType<SpecObjectType>().First();
            var columnType = rowType;

            var matrix = content.BuildRelationMatrix(rowType, columnType, pickedType);

            foreach (var cell in matrix.Cells.Values)
            {
                if (cell.Forward != null)
                {
                    Assert.That(cell.Forward.Type, Is.SameAs(pickedType));
                }

                if (cell.Backward != null)
                {
                    Assert.That(cell.Backward.Type, Is.SameAs(pickedType));
                }
            }
        }
    }
}
