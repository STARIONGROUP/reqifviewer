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
    public partial class RelationMatrixPage : ComponentBase, IDisposable
    {
        [Parameter]
        public string Identifier { get; set; }

        [Inject]
        public IReqIFLoaderService ReqIfLoaderService { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        public bool IsLoading { get; private set; } = true;

        private ReqIF reqIf;

        private IReadOnlyList<SpecObjectType> specObjectTypes = Array.Empty<SpecObjectType>();

        private IReadOnlyList<SpecRelationType> specRelationTypes = Array.Empty<SpecRelationType>();

        private SpecObjectType RowType { get; set; }

        private SpecObjectType ColumnType { get; set; }

        private SpecRelationType RelationType { get; set; }

        private bool ShowOnlyRelated { get; set; } = true;

        protected override void OnInitialized()
        {
            this.NavigationManager.LocationChanged += this.OnLocationChanged;
        }

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

                this.RowType ??= this.specObjectTypes.FirstOrDefault();
                this.ColumnType ??= this.specObjectTypes.FirstOrDefault();
                this.RelationType ??= this.specRelationTypes.FirstOrDefault();

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

        private void OnAxisChanged()
        {
            this.NavigationManager.NavigateTo(this.BuildMatrixUrl(), new NavigationOptions { ReplaceHistoryEntry = true });
        }

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
        /// Builds the sessionStorage key under which this picker view's top-left-corner anchor
        /// (row + column index) is remembered. Mirrors <see cref="BuildMatrixUrl"/>'s key set so
        /// switching the picker gives each view its own remembered anchor.
        /// </summary>
        private string BuildScrollKey()
        {
            return $"matrix-scroll:{this.Identifier}"
                 + $":{this.RowType?.Identifier ?? "_"}"
                 + $":{this.ColumnType?.Identifier ?? "_"}"
                 + $":{this.RelationType?.Identifier ?? "_"}"
                 + $":{(this.ShowOnlyRelated ? "rel" : "all")}";
        }

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

        public void Dispose()
        {
            if (this.NavigationManager != null)
            {
                this.NavigationManager.LocationChanged -= this.OnLocationChanged;
            }
        }
    }
}
