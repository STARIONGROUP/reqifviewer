// -------------------------------------------------------------------------------------------------
// <copyright file="RelationMatrixExtensions.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.ReqIFExtensions
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using ReqIFSharp;

    /// <summary>
    /// Direction of the <see cref="SpecRelation"/>(s) in a <see cref="MatrixCell"/> relative to the
    /// row <see cref="SpecObject"/> and the column <see cref="SpecObject"/>.
    /// </summary>
    public enum RelationDirection
    {
        /// <summary>No relation between row and column.</summary>
        None,

        /// <summary>A relation exists with row as <see cref="SpecRelation.Source"/> and column as <see cref="SpecRelation.Target"/>.</summary>
        Forward,

        /// <summary>A relation exists with column as <see cref="SpecRelation.Source"/> and row as <see cref="SpecRelation.Target"/>.</summary>
        Backward,

        /// <summary>Relations exist in both directions.</summary>
        Both
    }

    /// <summary>
    /// Single cell of a <see cref="RelationMatrixData"/>: the direction(s) of the underlying <see cref="SpecRelation"/>(s)
    /// between a row <see cref="SpecObject"/> and a column <see cref="SpecObject"/>.
    /// </summary>
    public sealed class MatrixCell
    {
        /// <summary>The relation with row as <see cref="SpecRelation.Source"/>, column as <see cref="SpecRelation.Target"/>; null if absent.</summary>
        public SpecRelation Forward { get; set; }

        /// <summary>The relation with column as <see cref="SpecRelation.Source"/>, row as <see cref="SpecRelation.Target"/>; null if absent.</summary>
        public SpecRelation Backward { get; set; }

        /// <summary>Direction summary derived from <see cref="Forward"/> and <see cref="Backward"/>.</summary>
        public RelationDirection Direction =>
            (this.Forward, this.Backward) switch
            {
                (null, null) => RelationDirection.None,
                (not null, null) => RelationDirection.Forward,
                (null, not null) => RelationDirection.Backward,
                _ => RelationDirection.Both
            };
    }

    /// <summary>
    /// The result of <see cref="RelationMatrixExtensions.BuildRelationMatrix"/>: row and column
    /// <see cref="SpecObject"/>s and the populated <see cref="MatrixCell"/>s. Cells with no relation
    /// are not stored — absence in <see cref="Cells"/> means <see cref="RelationDirection.None"/>.
    /// </summary>
    public sealed class RelationMatrixData
    {
        public IReadOnlyList<SpecObject> Rows { get; init; }

        public IReadOnlyList<SpecObject> Columns { get; init; }

        public IReadOnlyDictionary<(string rowId, string columnId), MatrixCell> Cells { get; init; }
    }

    /// <summary>
    /// Provides the matrix computation that backs the relation-matrix page: for a chosen row
    /// <see cref="SpecObjectType"/>, column <see cref="SpecObjectType"/> and <see cref="SpecRelationType"/>
    /// it returns the row/column <see cref="SpecObject"/>s and the directional <see cref="MatrixCell"/>s.
    /// </summary>
    public static class RelationMatrixExtensions
    {
        /// <summary>
        /// How often the relation scan checks the <see cref="CancellationToken"/>. Power-of-two so the
        /// loop can use a bitmask instead of modulo — keeps the inner check ~1 cycle on a 1 M-relation file.
        /// </summary>
        private const int CancellationCheckEvery = 512;

        /// <summary>
        /// Build a row × column matrix of <see cref="SpecObject"/>s for the given types.
        /// </summary>
        /// <remarks>
        /// The relation scan checks <paramref name="cancellationToken"/> every <see cref="CancellationCheckEvery"/>
        /// iterations so a caller running this on the threadpool can interrupt it on a long ReqIF.
        /// </remarks>
        public static RelationMatrixData BuildRelationMatrix(this ReqIFContent content, SpecObjectType rowType, SpecObjectType columnType, SpecRelationType relationType, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rows = (rowType == null
                ? Enumerable.Empty<SpecObject>()
                : content.SpecObjects.Where(o => o.Type == rowType)).ToList();

            var columns = (columnType == null
                ? Enumerable.Empty<SpecObject>()
                : content.SpecObjects.Where(o => o.Type == columnType)).ToList();

            var cells = new Dictionary<(string, string), MatrixCell>();

            if (relationType == null || rows.Count == 0 || columns.Count == 0)
            {
                return new RelationMatrixData
                {
                    Rows = rows,
                    Columns = columns,
                    Cells = cells
                };
            }

            var rowIds = new HashSet<string>(rows.Select(r => r.Identifier));
            var columnIds = new HashSet<string>(columns.Select(c => c.Identifier));

            var index = 0;
            foreach (var relation in content.SpecRelations.Where(r => r.Type == relationType))
            {
                if ((++index & (CancellationCheckEvery - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var sourceId = relation.Source?.Identifier;
                var targetId = relation.Target?.Identifier;

                if (sourceId == null || targetId == null)
                {
                    continue;
                }

                if (rowIds.Contains(sourceId) && columnIds.Contains(targetId))
                {
                    var key = (sourceId, targetId);
                    if (!cells.TryGetValue(key, out var cell))
                    {
                        cell = new MatrixCell();
                        cells[key] = cell;
                    }

                    cell.Forward ??= relation;
                }

                if (rowIds.Contains(targetId) && columnIds.Contains(sourceId))
                {
                    var key = (targetId, sourceId);
                    if (!cells.TryGetValue(key, out var cell))
                    {
                        cell = new MatrixCell();
                        cells[key] = cell;
                    }

                    cell.Backward ??= relation;
                }
            }

            return new RelationMatrixData
            {
                Rows = rows,
                Columns = columns,
                Cells = cells
            };
        }
    }
}
