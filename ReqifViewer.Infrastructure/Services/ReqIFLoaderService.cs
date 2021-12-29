// -------------------------------------------------------------------------------------------------
// <copyright file="ReqIFLoaderService.cs" company="RHEA System S.A.">
//
//   Copyright 2021 RHEA System S.A.
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

namespace ReqifViewer.Infrastructure.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using ReqIFSharp;

    /// <summary>
    /// The purpose of the <see cref="IReqIFLoaderService"/> is to load a ReqIF from a provided <see cref="Stream"/>
    /// and make the loaded <see cref="ReqIF"/> objects available
    /// </summary>
    public class ReqIFLoaderService : IReqIFLoaderService
    {
        private Stream sourceStream;

        /// <summary>
        /// a thread safe cache where the data associated to an <see cref="ExternalObject"/> is stored
        /// </summary>
        private ConcurrentDictionary<ExternalObject, string> externalObjectDataCache =
            new ConcurrentDictionary<ExternalObject, string>();

        /// <summary>
        /// Gets the instances of <see cref="ReqIF"/>
        /// </summary>
        public IEnumerable<ReqIF> ReqIFData { get; private set; }

        /// <summary>
        /// Gets a copy of the <see cref="Stream"/> from which the ReqIF is loaded
        /// </summary>
        public async Task<Stream> GetSourceStream(CancellationToken token)
        {
            if (this.sourceStream.Position != 0)
            {
                this.sourceStream.Seek(0, SeekOrigin.Begin);
            }

            var stream = new MemoryStream();
            await this.sourceStream.CopyToAsync(stream, token);
            if (stream.Position != 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            return stream;
        }

        /// <summary>
        /// Loads the <see cref="ReqIF"/> objects from the provided <see cref="Stream"/>
        /// and stores the result in the <see cref="ReqIFData"/> property.
        /// </summary>
        /// <param name="reqIFStream">
        /// a <see cref="Stream"/> that contains <see cref="ReqIF"/> content
        /// </param>
        /// <param name="token">
        /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
        /// </param>
        /// <returns>
        /// an awaitable <see cref="Task"/>
        /// </returns>
        public async Task Load(Stream reqIFStream, CancellationToken token)
        {
            this.externalObjectDataCache.Clear();

            this.sourceStream = new MemoryStream();

            var deserializationStream = new MemoryStream();

            reqIFStream.Seek(0, SeekOrigin.Begin);
            await reqIFStream.CopyToAsync(deserializationStream, token);
            if (deserializationStream.Position != 0)
            {
                deserializationStream.Seek(0, SeekOrigin.Begin);
            }

            reqIFStream.Seek(0, SeekOrigin.Begin);
            await reqIFStream.CopyToAsync(this.sourceStream, token);
            if (this.sourceStream.Position != 0)
            {
                this.sourceStream.Seek(0, SeekOrigin.Begin);
            }

            IEnumerable<ReqIF> result = null;

            var reqIfDeserializer = new ReqIFDeserializer();
            result = await reqIfDeserializer.DeserializeAsync(deserializationStream, token);
            
            await deserializationStream.DisposeAsync();

            this.ReqIFData = result;

            ReqIfChanged?.Invoke(this, this.ReqIFData);
        }

        /// <summary>
        /// Query the data object from associated to the <see cref="ExternalObject"/>
        /// </summary>
        /// <param name="externalObject">
        /// The <see cref="ExternalObject"/> that holds a reference to the data
        /// </param>
        /// <param name="token">
        /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
        /// </param>
        /// <returns>
        /// a Base64 encoded string that can be used in an HTML image element
        /// </returns>
        /// <remarks>
        /// The <see cref="ReqIFLoaderService"/> caches the data for fast
        /// </remarks>
        public async Task<string> QueryData(ExternalObject externalObject, CancellationToken token)
        {
            if (externalObject == null)
            {
                throw new ArgumentNullException($"The {nameof(externalObject)} may not be null");
            }

            if (externalObjectDataCache.TryGetValue(externalObject, out var result))
            {
                return result;
            }

            var stream = await this.GetSourceStream(token);
            
            var targetStream = new MemoryStream();

            externalObject.QueryLocalData(stream, targetStream);

            result = $"data:{externalObject.MimeType};base64,{Convert.ToBase64String(targetStream.ToArray())}";

            this.externalObjectDataCache.TryAdd(externalObject, result);

            return result;
        }

        /// <summary>
        /// Resets the <see cref="ReqIFLoaderService"/> by clearing <see cref="ReqIFData"/> and
        /// <see cref="SourceStream"/>
        /// </summary>
        public void Reset()
        {
            this.sourceStream = null;
            this.ReqIFData = null;
            this.externalObjectDataCache.Clear();

            ReqIfChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Event Handler that is invoked when the <see cref="ReqIFLoaderService"/> has either loaded data or has been reset
        /// </summary>
        public event EventHandler<IEnumerable<ReqIF>> ReqIfChanged;
    }
}
