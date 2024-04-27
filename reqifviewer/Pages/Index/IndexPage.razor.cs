// -------------------------------------------------------------------------------------------------
// <copyright file="IndexPage.razor.cs" company="Starion Group S.A.">
//
//   Copyright 2023-2024 Starion Group S.A.
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

namespace reqifviewer.Pages.Index
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.Extensions.Configuration;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using Serilog;

    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using System.Globalization;

    using Utilities;

    /// <summary>
    /// The code behind for the <see cref="IndexPage"/> component
    /// </summary>
    public partial class IndexPage : ComponentBase, IDisposable
    {
        /// <summary>
        /// The <see cref="IReqIFLoaderService"/>
        /// </summary>
        [Inject]
        public IReqIFLoaderService ReqIfLoaderService { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IConfiguration"/>
        /// </summary>
        [Inject]
        public IConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the error message that is displayed in the component
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// The value to check if the component is loading
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// The text of file selection
        /// </summary>
        private string fileSelectionText = "Select a file";

        /// <summary>
        /// The directory where uploaded files are stored
        /// </summary>
        private const string UploadsDirectory = "wwwroot/uploads/";

        /// <summary>
        /// The maximum file size to upload in megabytes
        /// </summary>
        private double MaxUploadFileSizeInMb => double.Parse(this.Configuration.GetSection(Constants.MaxUploadFileSizeInMbConfigurationKey).Value!, CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets or sets the <see cref="ReqIF"/> file path
        /// </summary>
        private string ReqIfFilePath { get; set; }

        /// <summary>
        /// The value to verify if the ReqIF upload is available
        /// </summary>
        private bool reqifisAvailable;

        /// <summary>
        /// A collection of <see cref="ReqIF"/>
        /// </summary>
        private IEnumerable<ReqIF> reqIfs;

        /// <summary>
        /// The <see cref="CancellationTokenSource"/> to cancel ReqIf loading
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            TryDeleteFile(this.ReqIfFilePath);
            this.cancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// Invoked when the component is initialized after having received its initial parameters
        /// </summary>
        protected override void OnInitialized()
        {
            if (this.ReqIfLoaderService.ReqIFData == null || !this.ReqIfLoaderService.ReqIFData.Any())
            {
                this.reqIfs = null;

                Log.ForContext<IndexPage>().Debug("no ReqIF loaded");
            }
            else
            {
                this.reqIfs = this.ReqIfLoaderService.ReqIFData;

                Log.ForContext<IndexPage>().Debug("a Total of {amount} ReqIF loaded", this.reqIfs.Count());
            }
        }

        /// <summary>
        /// handles file selection
        /// </summary>
        /// <param name="e">
        /// The <see cref="InputFileChangeEventArgs"/> to be used to handle the selected file
        /// </param>
        /// <returns>
        /// an awaitable <see cref="Task"/>
        /// </returns>
        private async Task HandleSelection(InputFileChangeEventArgs e)
        {
            var maxUploadFileSizeInBytes = (long)(this.MaxUploadFileSizeInMb * 1024 * 1024);

            if (e.File.Size > maxUploadFileSizeInBytes)
            {
                this.ErrorMessage = $"The max file size is {this.MaxUploadFileSizeInMb} MB";
                return;
            }

            if (!string.IsNullOrEmpty(this.ReqIfFilePath))
            {
                TryDeleteFile(this.ReqIfFilePath);
            }

            this.ErrorMessage = string.Empty;
            var sw = Stopwatch.StartNew();
            this.reqifisAvailable = false;

            this.ReqIfFilePath = Path.Combine(UploadsDirectory, Guid.NewGuid().ToString());
            this.fileSelectionText = e.File.Name;
            Directory.CreateDirectory(UploadsDirectory);

            await using (var fileStream = new FileStream(this.ReqIfFilePath, FileMode.Create))
            {
                await e.File.OpenReadStream(maxUploadFileSizeInBytes).CopyToAsync(fileStream);
            }

            this.reqifisAvailable = true;
            await this.InvokeAsync(this.StateHasChanged);

            Log.ForContext<IndexPage>().Information("file read into stream in {time} [ms]", sw.ElapsedMilliseconds);
        }

        /// <summary>
        /// Loads the <see cref="ReqIF"/> from the selected file
        /// </summary>
        /// <returns>
        /// an awaitable <see cref="Task"/>
        /// </returns>
        private async Task OnLoadReqIF()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                this.IsLoading = true;
                this.cancellationTokenSource = new CancellationTokenSource();
                this.reqIfs = null;
                this.ReqIfLoaderService.Reset();
                await Task.Delay(500);

                var convertPathToSupportedFileExtensionKind = this.fileSelectionText.ConvertPathToSupportedFileExtensionKind();

                await using (var reqIfFileMemoryStream = File.Open(this.ReqIfFilePath, FileMode.Open))
                {
                    await this.ReqIfLoaderService.Load(reqIfFileMemoryStream, convertPathToSupportedFileExtensionKind, this.cancellationTokenSource.Token);
                    this.reqIfs = this.ReqIfLoaderService.ReqIFData;
                }

                this.reqIfs = this.ReqIfLoaderService.ReqIFData;
                Log.ForContext<IndexPage>().Information("a total of {amount} ReqIF objects deserialized in {time} [ms]", this.reqIfs.Count(), sw.ElapsedMilliseconds);
            }
            catch (TaskCanceledException)
            {
                Log.ForContext<IndexPage>().Information("Load was cancelled");
            }
            catch (Exception e)
            {
                Log.ForContext<IndexPage>().Error(e, "load reqif failed");
            }
            finally
            {
                this.IsLoading = false;
                await this.InvokeAsync(this.StateHasChanged);
            }
        }

        /// <summary>
        /// Cancel loading the ReqIF file
        /// </summary>
        private async Task OnCancel()
        {
            if (this.cancellationTokenSource != null)
            {
                await this.cancellationTokenSource.CancelAsync();
                this.IsLoading = false;
                await this.InvokeAsync(this.StateHasChanged);
            }
        }

        /// <summary>
        /// Clear the ReqIF file and reset the <see cref="IReqIFLoaderService"/>
        /// </summary>
        private async Task OnClear()
        {
            this.reqIfs = null;
            this.ReqIfLoaderService.Reset();
            TryDeleteFile(this.ReqIfFilePath);
            this.IsLoading = false;
            await this.InvokeAsync(this.StateHasChanged);
        }

        /// <summary>
        /// Tries to delete a given physical file
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <returns>true if the file was removed, otherwise false</returns>
        private static bool TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}