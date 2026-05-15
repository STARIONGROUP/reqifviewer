// -------------------------------------------------------------------------------------------------
// <copyright file="RelationMatrixPage.razor.cs" company="Starion Group S.A.">
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

namespace reqifviewer.Pages.RelationMatrix
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Routing;
    using Microsoft.AspNetCore.WebUtilities;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using ReqifViewer.Navigation;

    using Serilog;

    /// <summary>
    /// Hosts the picker shell + URL state for the Relation Matrix view. The matrix itself —
    /// async build, busy indicator, cancellation, render caches, scroll restoration, dismissible
    /// tip — lives in <see cref="reqifviewer.Components.RelationMatrixComponent"/>, which receives
    /// the selectors as parameters and rebuilds whenever they change.
    /// </summary>
    public sealed partial class RelationMatrixPage : ComponentBase, IDisposable
    {
        /// <summary>The ReqIF header identifier from the route; selects which loaded document to show.</summary>
        [Parameter]
        public string Identifier { get; set; }

        /// <summary>The scoped service that owns the parsed ReqIF documents.</summary>
        [Inject]
        public IReqIFLoaderService ReqIfLoaderService { get; set; }

        /// <summary>Used to project picker state into the URL query string and to observe location changes.</summary>
        [Inject]
        public NavigationManager NavigationManager { get; set; }

        /// <summary>True during the brief ReqIF lookup window before the page resolves its document.</summary>
        public bool IsLoading { get; private set; } = true;

        /// <summary>The ReqIF matching <see cref="Identifier"/>, or null if none is loaded under that id.</summary>
        private ReqIF reqIf;

        /// <summary>The ReqIF resolved on the previous parameter set; used to detect a document change and reset the pickers.</summary>
        private ReqIF previousReqIf;

        /// <summary>The current document's <see cref="SpecObjectType"/>s, offered as the row/column picker options.</summary>
        private IReadOnlyList<SpecObjectType> specObjectTypes = Array.Empty<SpecObjectType>();

        /// <summary>The current document's <see cref="SpecRelationType"/>s, offered as the relation picker options.</summary>
        private IReadOnlyList<SpecRelationType> specRelationTypes = Array.Empty<SpecRelationType>();

        /// <summary>The selected row <see cref="SpecObjectType"/>, passed to the matrix component.</summary>
        private SpecObjectType RowType { get; set; }

        /// <summary>The selected column <see cref="SpecObjectType"/>, passed to the matrix component.</summary>
        private SpecObjectType ColumnType { get; set; }

        /// <summary>The selected <see cref="SpecRelationType"/>, passed to the matrix component.</summary>
        private SpecRelationType RelationType { get; set; }

        /// <summary>Whether the matrix should hide rows/columns that carry no relation in the current selection.</summary>
        private bool ShowOnlyRelated { get; set; } = true;

        /// <summary>Subscribes to <see cref="NavigationManager"/> location changes so address-bar edits re-seed the pickers.</summary>
        protected override void OnInitialized()
        {
            this.NavigationManager.LocationChanged += this.OnLocationChanged;
        }

        /// <summary>
        /// Resolves the ReqIF for the current <see cref="Identifier"/>, rebuilds the picker option
        /// lists, resets the selectors when the document changed, seeds defaults, and applies any
        /// picker state carried in the query string.
        /// </summary>
        protected override void OnParametersSet()
        {
            try
            {
                this.IsLoading = true;

                this.reqIf = this.ReqIfLoaderService.ReqIFData?
                    .SingleOrDefault(x => x.TheHeader.Identifier == this.Identifier);

                if (this.reqIf == null)
                {
                    return;
                }

                this.specObjectTypes = this.reqIf.CoreContent.SpecTypes
                    .OfType<SpecObjectType>().ToList();

                this.specRelationTypes = this.reqIf.CoreContent.SpecTypes
                    .OfType<SpecRelationType>().ToList();

                if (!ReferenceEquals(this.reqIf, this.previousReqIf))
                {
                    this.previousReqIf = this.reqIf;
                    this.RowType = null;
                    this.ColumnType = null;
                    this.RelationType = null;
                }

                this.RowType ??= this.specObjectTypes.Count > 0 ? this.specObjectTypes[0] : null;
                this.ColumnType ??= this.specObjectTypes.Count > 0 ? this.specObjectTypes[0] : null;
                this.RelationType ??= this.specRelationTypes.Count > 0 ? this.specRelationTypes[0] : null;

                this.ApplyPickerStateFromQueryString();
            }
            catch (Exception e)
            {
                Log.ForContext<RelationMatrixPage>().Error(e, "OnParametersSet Failed");
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        /// <summary>Projects the current picker selection into the URL (replacing the history entry) after a dropdown change.</summary>
        private void OnAxisChanged()
        {
            this.NavigationManager.NavigateTo(this.BuildMatrixUrl(), new NavigationOptions { ReplaceHistoryEntry = true });
        }

        /// <summary>Swaps the row and column types and projects the new selection into the URL.</summary>
        private void OnSwapAxes()
        {
            (this.RowType, this.ColumnType) = (this.ColumnType, this.RowType);
            this.NavigationManager.NavigateTo(this.BuildMatrixUrl(), new NavigationOptions { ReplaceHistoryEntry = true });
        }

        /// <summary>
        /// Reacts to URL changes that arrive *without* a remount (typically: user edits the address bar,
        /// or another component on the page issues a NavigateTo). Picker changes don't need this path —
        /// they mutate state directly and Blazor's automatic re-render passes the new selectors to the
        /// matrix component.
        /// </summary>
        private void OnLocationChanged(object sender, LocationChangedEventArgs args)
        {
            if (this.Identifier == null)
            {
                return;
            }

            var uri = this.NavigationManager.ToAbsoluteUri(args.Location);
            if (!uri.AbsolutePath.EndsWith($"/reqif/{this.Identifier}/relationmatrix", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var oldRow = this.RowType;
            var oldCol = this.ColumnType;
            var oldRel = this.RelationType;
            var oldShow = this.ShowOnlyRelated;

            this.ApplyPickerStateFromQueryString();

            if (!ReferenceEquals(oldRow, this.RowType)
                || !ReferenceEquals(oldCol, this.ColumnType)
                || !ReferenceEquals(oldRel, this.RelationType)
                || oldShow != this.ShowOnlyRelated)
            {
                _ = this.InvokeAsync(this.StateHasChanged);
            }
        }

        /// <summary>
        /// Overlays picker state from the <c>row</c>/<c>col</c>/<c>rel</c>/<c>related</c> query-string
        /// parameters, only when a value is present and resolves to a type in the current document.
        /// </summary>
        private void ApplyPickerStateFromQueryString()
        {
            if (this.NavigationManager.TryGetQueryString<string>("row", out var rowId))
            {
                var match = this.specObjectTypes.FirstOrDefault(t => t.Identifier == rowId);
                if (match != null)
                {
                    this.RowType = match;
                }
            }

            if (this.NavigationManager.TryGetQueryString<string>("col", out var colId))
            {
                var match = this.specObjectTypes.FirstOrDefault(t => t.Identifier == colId);
                if (match != null)
                {
                    this.ColumnType = match;
                }
            }

            if (this.NavigationManager.TryGetQueryString<string>("rel", out var relId))
            {
                var match = this.specRelationTypes.FirstOrDefault(t => t.Identifier == relId);
                if (match != null)
                {
                    this.RelationType = match;
                }
            }

            if (this.NavigationManager.TryGetQueryString<string>("related", out var relatedStr)
                && bool.TryParse(relatedStr, out var related))
            {
                this.ShowOnlyRelated = related;
            }
        }

        /// <summary>
        /// Builds a per-picker-view attachment identity for the matrix component. It is not a
        /// storage key — the top-left-corner anchor itself lives in the URL query string
        /// (<c>anchorRow</c>/<c>anchorCol</c>, written by the matrixScroll JS module). The
        /// component uses this string only to decide whether the current view is already
        /// wired (re-attaching scroll/fit once per distinct picker selection).
        /// </summary>
        private string BuildScrollKey()
        {
            return $"matrix-scroll:{this.Identifier}"
                 + $":{this.RowType?.Identifier ?? "_"}"
                 + $":{this.ColumnType?.Identifier ?? "_"}"
                 + $":{this.RelationType?.Identifier ?? "_"}"
                 + $":{(this.ShowOnlyRelated ? "rel" : "all")}";
        }

        /// <summary>
        /// Builds the matrix route URL carrying the current picker selection as query parameters.
        /// Deliberately omits the scroll anchor (<c>anchorRow</c>/<c>anchorCol</c>) so a picker
        /// change resets the new view to the top-left corner.
        /// </summary>
        private string BuildMatrixUrl()
        {
            var query = new Dictionary<string, string>();

            if (this.RowType != null)
            {
                query["row"] = this.RowType.Identifier;
            }

            if (this.ColumnType != null)
            {
                query["col"] = this.ColumnType.Identifier;
            }

            if (this.RelationType != null)
            {
                query["rel"] = this.RelationType.Identifier;
            }

            query["related"] = this.ShowOnlyRelated ? "true" : "false";

            return QueryHelpers.AddQueryString($"/reqif/{this.Identifier}/relationmatrix", query);
        }

        /// <summary>Unsubscribes from <see cref="NavigationManager"/> location changes.</summary>
        public void Dispose()
        {
            if (this.NavigationManager != null)
            {
                this.NavigationManager.LocationChanged -= this.OnLocationChanged;
            }
        }
    }
}
