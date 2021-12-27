﻿// -------------------------------------------------------------------------------------------------
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
        /// <summary>
        /// Gets the instances of <see cref="ReqIF"/>
        /// </summary>
        public IEnumerable<ReqIF> ReqIFData { get; private set; }

        /// <summary>
        /// Gets the <see cref="Stream"/> from which the ReqIF is loaded
        /// </summary>
        public Stream SourceStream { get; private set; }

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
            this.SourceStream = new MemoryStream();
            await reqIFStream.CopyToAsync(this.SourceStream, token);
            
            if (this.SourceStream.Position != 0)
            {
                this.SourceStream.Seek(0, SeekOrigin.Begin);
            }

            IEnumerable<ReqIF> result = null;

            var reqIfDeserializer = new ReqIFDeserializer();
            result = await reqIfDeserializer.DeserializeAsync(this.SourceStream, token);

            this.ReqIFData = result;

            ReqIfChanged?.Invoke(this, this.ReqIFData);
        }

        /// <summary>
        /// Resets the <see cref="ReqIFLoaderService"/> by clearing <see cref="ReqIFData"/> and
        /// <see cref="SourceStream"/>
        /// </summary>
        public void Reset()
        {
            this.SourceStream = null;
            this.ReqIFData = null;

            ReqIfChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Event Handler that is invoked when the <see cref="ReqIFLoaderService"/> has either loaded data or has been reset
        /// </summary>
        public event EventHandler<IEnumerable<ReqIF>> ReqIfChanged;
    }
}
