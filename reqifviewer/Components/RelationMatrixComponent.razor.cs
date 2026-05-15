// -------------------------------------------------------------------------------------------------
// <copyright file="RelationMatrixComponent.razor.cs" company="Starion Group S.A.">
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

namespace reqifviewer.Components
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    using ReqIFSharp;

    using ReqifViewer.ReqIFExtensions;

    using Serilog;

    /// <summary>
    /// Renders a SpecObject × SpecObject relation matrix for a given ReqIF + selectors. Owns the
    /// async build (run on the threadpool with a <see cref="CancellationToken"/>), the busy
    /// indicator, the render caches, the scroll-restoration JS interop, and the dismissible
    /// large-matrix tip. The hosting page just supplies parameters and re-renders on changes.
    /// </summary>
    public sealed partial class RelationMatrixComponent : ComponentBase, IDisposable
    {
        /// <summary>Soft cap for the cell count above which the user is nudged toward "Show only related".</summary>
        private const int LargeMatrixCellCount = 5_000;

        /// <summary>Pixel height of a single matrix row. Must stay in sync with the Virtualize ItemSize attribute and the <c>.matrix-row</c> height in scoped CSS — used to translate between scrollTop and row index.</summary>
        private const int RowHeightPx = 32;

        /// <summary>Pixel width of a single matrix data cell. Must stay in sync with <c>--cell-width</c> in scoped CSS — used to translate between scrollLeft and column index.</summary>
        private const int CellWidthPx = 36;

        /// <summary>How often the per-axis/label/cell loops in the builder check the cancellation token. Power-of-two so the check is a bitmask, not a modulo.</summary>
        private const int BuilderCancellationCheckEvery = 256;

        /// <summary>The scrollable matrix wrapper element, handed to the matrixScroll JS module so attach/detach are scoped to this component instance rather than a shared selector.</summary>
        private ElementReference wrapperRef;

        /// <summary>The JS runtime used to drive the matrixScroll module (viewport fit + scroll-anchor restore).</summary>
        [Inject]
        public IJSRuntime JSRuntime { get; set; }

        /// <summary>The loaded ReqIF whose <see cref="ReqIFContent"/> the matrix is built from.</summary>
        [Parameter]
        public ReqIF ReqIf { get; set; }

        /// <summary>The <see cref="SpecObjectType"/> whose <see cref="SpecObject"/>s form the matrix rows.</summary>
        [Parameter]
        public SpecObjectType RowType { get; set; }

        /// <summary>The <see cref="SpecObjectType"/> whose <see cref="SpecObject"/>s form the matrix columns.</summary>
        [Parameter]
        public SpecObjectType ColumnType { get; set; }

        /// <summary>The <see cref="SpecRelationType"/> whose relations populate the matrix cells.</summary>
        [Parameter]
        public SpecRelationType RelationType { get; set; }

        /// <summary>When true, rows/columns with no relation in the current selection are hidden.</summary>
        [Parameter]
        public bool ShowOnlyRelated { get; set; }

        /// <summary>Per-picker-view identity supplied by the host page; used to wire the scroll/fit JS interop exactly once per distinct selection.</summary>
        [Parameter]
        public string ScrollKey { get; set; }

        /// <summary>True while a build is running on the threadpool; drives the progress bar + Cancel button.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>The most recently committed build output, or null before the first successful build.</summary>
        private RelationMatrixData matrix;

        /// <summary>The row <see cref="SpecObject"/>s actually rendered (after the show-only-related filter), in display order.</summary>
        public ICollection<SpecObject> VisibleRows { get; private set; } = Array.Empty<SpecObject>();

        /// <summary>The column <see cref="SpecObject"/>s actually rendered (after the show-only-related filter), in display order.</summary>
        public ICollection<SpecObject> VisibleColumns { get; private set; } = Array.Empty<SpecObject>();

        /// <summary>Cache of row <see cref="SpecObject.Identifier"/> → display label.</summary>
        public Dictionary<string, string> RowLabels { get; private set; } = new();

        /// <summary>Cache of column <see cref="SpecObject.Identifier"/> → display label.</summary>
        public Dictionary<string, string> ColumnLabels { get; private set; } = new();

        /// <summary>Cache of row <see cref="SpecObject.Identifier"/> → drill-down URL.</summary>
        public Dictionary<string, string> RowUrls { get; private set; } = new();

        /// <summary>Cache of column <see cref="SpecObject.Identifier"/> → drill-down URL.</summary>
        public Dictionary<string, string> ColumnUrls { get; private set; } = new();

        /// <summary>Cache of (rowId, columnId) → the pre-rendered cell bundle; absence means an empty cell.</summary>
        public Dictionary<(string rowId, string columnId), RenderedCell> RenderedCells { get; private set; } = new();

        /// <summary>Set to true once the user clicks the X on the large-matrix tip. Per-mount only — resets when the component is remounted.</summary>
        private bool isLargeMatrixHintDismissed;

        /// <summary>Cancellation source for the in-flight build; its reference identity is the build "generation" used to gate commits.</summary>
        private CancellationTokenSource cts;

        /// <summary>The <see cref="ScrollKey"/> the matrixScroll JS module was last wired for. Gates the (potentially large) identifier-array interop to once per matrix view rather than once per render.</summary>
        private string attachedScrollKey;

        /// <summary>True when the matrix is large enough to nudge the user toward "Show only related" and the tip has not been dismissed.</summary>
        public bool ShowLargeMatrixHint => !this.ShowOnlyRelated
            && !this.isLargeMatrixHintDismissed
            && (long)this.VisibleRows.Count * this.VisibleColumns.Count > LargeMatrixCellCount;

        /// <summary>
        /// On any parameter change: snapshots the inputs, cancels any in-flight build, runs a
        /// fresh build on the threadpool, and commits it only if this run still owns the current
        /// generation (so a superseded build never clobbers newer state or the busy indicator).
        /// </summary>
        protected override async Task OnParametersSetAsync()
        {
            // Snapshot inputs — the build runs on the threadpool and must not race a fresh
            // parameter set arriving while it is mid-flight.
            var reqIf = this.ReqIf;
            var rowType = this.RowType;
            var columnType = this.ColumnType;
            var relationType = this.RelationType;
            var showOnlyRelated = this.ShowOnlyRelated;

            // Cancel any in-flight build and start a new generation we own a handle to.
            if (this.cts != null)
            {
                await this.cts.CancelAsync();
                this.cts.Dispose();
            }

            var operationCts = new CancellationTokenSource();
            this.cts = operationCts;
            var ct = operationCts.Token;

            this.IsBusy = true;
            await this.InvokeAsync(this.StateHasChanged);

            try
            {
                await Task.Yield();
                var result = await Task.Run(
                    () => BuildResult(reqIf, rowType, columnType, relationType, showOnlyRelated, ct),
                    ct);

                // Commit only if we still own the current generation — a newer
                // OnParametersSetAsync run will have replaced this.cts.
                if (ReferenceEquals(this.cts, operationCts))
                {
                    this.ApplyResult(result);
                }
            }
            catch (OperationCanceledException)
            {
                Log.ForContext<RelationMatrixComponent>().Information("Matrix build cancelled (parameters changed or user clicked Cancel)");
            }
            catch (Exception e)
            {
                Log.ForContext<RelationMatrixComponent>().Error(e, "Matrix build failed");
            }
            finally
            {
                // Only the owning generation clears IsBusy; a stale finally must not hide
                // the progress bar while a newer build is still running.
                if (ReferenceEquals(this.cts, operationCts))
                {
                    this.IsBusy = false;
                    await this.InvokeAsync(this.StateHasChanged);
                }
            }
        }

        /// <summary>
        /// Once a matrix has rendered for a new <see cref="ScrollKey"/>, wires the matrixScroll
        /// JS module: fits the wrapper to the viewport, then attaches scroll-anchor restoration
        /// (passing the ordered identifier arrays). Runs at most once per distinct view.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (this.matrix != null
                && this.VisibleRows.Count > 0
                && !string.IsNullOrEmpty(this.ScrollKey)
                && this.ScrollKey != this.attachedScrollKey)
            {
                var rowIds = this.VisibleRows.Select(o => o.Identifier).ToArray();
                var colIds = this.VisibleColumns.Select(o => o.Identifier).ToArray();

                try
                {
                    // Fit before attach so the wrapper has its final clientHeight
                    // when the scroll-restore math runs.
                    await this.JSRuntime.InvokeVoidAsync("matrixScroll.fit", this.wrapperRef);

                    await this.JSRuntime.InvokeVoidAsync(
                        "matrixScroll.attach",
                        this.wrapperRef,
                        this.ScrollKey,
                        RowHeightPx,
                        CellWidthPx,
                        rowIds,
                        colIds);

                    // Only mark the key wired once both interop calls succeeded, so a
                    // failed setup is retried on the next render rather than suppressed.
                    this.attachedScrollKey = this.ScrollKey;
                }
                catch (Exception e)
                {
                    Log.ForContext<RelationMatrixComponent>().Warning(e, "Failed to wire matrix scroll restoration");
                }
            }
        }

        /// <summary>Cancels the in-flight build in response to the user clicking the Cancel button.</summary>
        private void OnCancel()
        {
            Log.ForContext<RelationMatrixComponent>().Information("Cancel clicked");
            this.cts?.Cancel();
        }

        /// <summary>Permanently hides the large-matrix tip for this component mount.</summary>
        private void OnDismissLargeMatrixHint()
        {
            this.isLargeMatrixHintDismissed = true;
        }

        /// <summary>
        /// Pure, threadpool-safe build. Produces the matrix and every per-cell cache from the
        /// snapshotted inputs only — reads no <c>this.*</c> — so the renderer can commit the
        /// result back onto the component only when this operation still owns the generation.
        /// Observes <paramref name="ct"/> in every loop that grows with the ReqIF.
        /// </summary>
        private static MatrixBuildResult BuildResult(ReqIF reqIf, SpecObjectType rowType, SpecObjectType columnType, SpecRelationType relationType, bool showOnlyRelated, CancellationToken ct)
        {
            if (reqIf == null || rowType == null || columnType == null || relationType == null)
            {
                return MatrixBuildResult.Empty;
            }

            var matrix = reqIf.CoreContent.BuildRelationMatrix(rowType, columnType, relationType, ct);
            ct.ThrowIfCancellationRequested();

            var visibleRows = FilterVisible(matrix.Rows, matrix.Cells.Keys, k => k.rowId, showOnlyRelated, matrix.Cells.Count, ct);
            var visibleColumns = FilterVisible(matrix.Columns, matrix.Cells.Keys, k => k.columnId, showOnlyRelated, matrix.Cells.Count, ct);

            var (rowLabels, rowUrls) = BuildLabelsAndUrls(visibleRows, ct);
            var (columnLabels, columnUrls) = BuildLabelsAndUrls(visibleColumns, ct);

            var renderedCells = BuildRenderedCells(matrix.Cells, rowLabels, columnLabels, relationType, ct);

            return new MatrixBuildResult(matrix, visibleRows, visibleColumns, rowLabels, columnLabels, rowUrls, columnUrls, renderedCells);
        }

        /// <summary>
        /// Filters the matrix axis to the visible <see cref="SpecObject"/>s, honouring the
        /// "show only related" toggle, checking <paramref name="ct"/> periodically.
        /// </summary>
        private static ICollection<SpecObject> FilterVisible(IReadOnlyList<SpecObject> all, IEnumerable<(string rowId, string columnId)> cellKeys, Func<(string rowId, string columnId), string> idSelector, bool showOnlyRelated, int cellCount, CancellationToken ct)
        {
            if (showOnlyRelated && cellCount == 0)
            {
                return Array.Empty<SpecObject>();
            }

            HashSet<string> connected = null;
            if (showOnlyRelated)
            {
                connected = new HashSet<string>();
                var keyIndex = 0;
                foreach (var key in cellKeys)
                {
                    if ((++keyIndex & (BuilderCancellationCheckEvery - 1)) == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                    }

                    connected.Add(idSelector(key));
                }
            }

            var result = new List<SpecObject>(all.Count);
            var i = 0;
            foreach (var o in all)
            {
                if ((++i & (BuilderCancellationCheckEvery - 1)) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                }

                if (connected == null || connected.Contains(o.Identifier))
                {
                    result.Add(o);
                }
            }

            return result;
        }

        /// <summary>
        /// Builds the display-name and drill-down URL caches for an axis, checking
        /// <paramref name="ct"/> periodically.
        /// </summary>
        private static (Dictionary<string, string> labels, Dictionary<string, string> urls) BuildLabelsAndUrls(ICollection<SpecObject> objects, CancellationToken ct)
        {
            var labels = new Dictionary<string, string>(objects.Count);
            var urls = new Dictionary<string, string>(objects.Count);
            var i = 0;
            foreach (var o in objects)
            {
                if ((++i & (BuilderCancellationCheckEvery - 1)) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                }

                labels[o.Identifier] = ExtractDisplayNameOf(o);
                urls[o.Identifier] = o.CreateUrl();
            }

            return (labels, urls);
        }

        /// <summary>
        /// Builds the per-cell render bundles (glyph, css class, tooltip, drill URL), checking
        /// <paramref name="ct"/> periodically.
        /// </summary>
        private static Dictionary<(string, string), RenderedCell> BuildRenderedCells(IReadOnlyDictionary<(string rowId, string columnId), MatrixCell> cells, Dictionary<string, string> rowLabels, Dictionary<string, string> columnLabels, SpecRelationType relationType, CancellationToken ct)
        {
            var rendered = new Dictionary<(string, string), RenderedCell>(cells.Count);
            var relName = relationType.LongName ?? relationType.Identifier;
            var i = 0;
            foreach (var ((rowId, colId), cell) in cells)
            {
                if ((++i & (BuilderCancellationCheckEvery - 1)) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                }

                if (!rowLabels.TryGetValue(rowId, out var rowLabel)
                    || !columnLabels.TryGetValue(colId, out var colLabel))
                {
                    continue;
                }

                var (glyph, css, tooltip) = cell.Direction switch
                {
                    RelationDirection.Forward => ("→", "matrix-cell forward", $"{rowLabel} —{relName}→ {colLabel}"),
                    RelationDirection.Backward => ("←", "matrix-cell backward", $"{rowLabel} ←{relName}— {colLabel}"),
                    RelationDirection.Both => ("↔", "matrix-cell both", $"{rowLabel} ↔{relName}↔ {colLabel}"),
                    _ => (string.Empty, "matrix-cell empty", string.Empty)
                };

                var target = (cell.Forward ?? cell.Backward)?.CreateUrl();

                rendered[(rowId, colId)] = new RenderedCell(glyph, css, tooltip, target);
            }

            return rendered;
        }

        /// <summary>
        /// Copies a completed <see cref="MatrixBuildResult"/> onto the component. Called only
        /// when the producing operation still owns the current generation, so a cancelled or
        /// superseded build never mutates component state.
        /// </summary>
        private void ApplyResult(MatrixBuildResult result)
        {
            this.matrix = result.Matrix;
            this.VisibleRows = result.VisibleRows;
            this.VisibleColumns = result.VisibleColumns;
            this.RowLabels = result.RowLabels;
            this.ColumnLabels = result.ColumnLabels;
            this.RowUrls = result.RowUrls;
            this.ColumnUrls = result.ColumnUrls;
            this.RenderedCells = result.RenderedCells;
        }

        /// <summary>
        /// Cancels any in-flight build and tears down the matrixScroll JS wiring (scroll listener
        /// + viewport-fit observers) for this component's wrapper element.
        /// </summary>
        public void Dispose()
        {
            this.cts?.Cancel();
            this.cts?.Dispose();
            this.cts = null;

            try
            {
                _ = this.JSRuntime?.InvokeVoidAsync("matrixScroll.detach", this.wrapperRef).AsTask();
                _ = this.JSRuntime?.InvokeVoidAsync("matrixScroll.unfit", this.wrapperRef).AsTask();
            }
            catch
            {
                // circuit may be gone; nothing to do
            }
        }

        /// <summary>Resolves a <see cref="SpecObject"/>'s display label, falling back to its identifier.</summary>
        private static string ExtractDisplayNameOf(SpecObject specObject)
        {
            return specObject.ExtractDisplayName()?.ToString() ?? specObject.Identifier;
        }

        /// <summary>
        /// Per-cell rendering bundle: glyph (→ ← ↔), css class, tooltip text, and optional drill URL.
        /// Built once in <see cref="BuildRenderedCells"/>; the template just emits these.
        /// </summary>
        public sealed record RenderedCell(string Glyph, string CssClass, string Tooltip, string TargetUrl);

        /// <summary>
        /// Immutable output of <see cref="BuildResult"/>. Produced entirely from snapshotted
        /// inputs so the renderer can commit it back onto the component (via
        /// <see cref="ApplyResult"/>) only when the build still owns the current generation.
        /// </summary>
        private sealed record MatrixBuildResult(
            RelationMatrixData Matrix,
            ICollection<SpecObject> VisibleRows,
            ICollection<SpecObject> VisibleColumns,
            Dictionary<string, string> RowLabels,
            Dictionary<string, string> ColumnLabels,
            Dictionary<string, string> RowUrls,
            Dictionary<string, string> ColumnUrls,
            Dictionary<(string rowId, string columnId), RenderedCell> RenderedCells)
        {
            /// <summary>The committed-empty result used when inputs are incomplete (no rows/columns/cells).</summary>
            public static MatrixBuildResult Empty { get; } = new(
                null,
                Array.Empty<SpecObject>(),
                Array.Empty<SpecObject>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<(string, string), RenderedCell>());
        }
    }
}
