// -------------------------------------------------------------------------------------------------
// <copyright file="IndexPageTestFixture.cs" company="Starion Group S.A.">
//
//   Copyright 2023-2025 Starion Group S.A.
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

namespace ReqifViewer.Tests.Pages.Index
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Bunit;

    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    using Radzen.Blazor;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using reqifviewer.Pages.Index;
    using reqifviewer.Utilities;
    using TestContext = Bunit.TestContext;

    /// <summary>
    /// Suite of tests for the <see cref="IndexPageTestFixture"/>
    /// </summary>
    [TestFixture]
    public class IndexPageTestFixture
    {
        private Mock<IReqIFLoaderService> reqIfLoaderService;
        private TestContext context;
        private IConfiguration configuration;
        private const double MaxFileSizeInMb = 5;

        [SetUp]
        public void SetUp()
        {
            this.context = new TestContext();
            this.reqIfLoaderService = new Mock<IReqIFLoaderService>();

            this.configuration = new ConfigurationBuilder()
                .AddInMemoryCollection([new KeyValuePair<string, string>(Constants.MaxUploadFileSizeInMbConfigurationKey, MaxFileSizeInMb.ToString(CultureInfo.InvariantCulture))])
                .Build();

            this.context.Services.AddSingleton(this.reqIfLoaderService.Object);
            this.context.Services.AddSingleton(this.configuration);
        }

        [TearDown]
        public void TearDown()
        {
            this.context.Dispose();
        }

        [Test]
        public async Task VerifyComponent()
        {
            var renderer = this.context.RenderComponent<IndexPage>();
            var uploadComponent = renderer.FindComponent<InputFile>();
            var maxFileSizeInBytes = (long)(MaxFileSizeInMb * 1024 * 1024);

            var file = new Mock<IBrowserFile>();
            file.Setup(x => x.Size).Returns(maxFileSizeInBytes + 1);
            file.Setup(x => x.Name).Returns("file.reqif");
            file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(new MemoryStream());

            await renderer.InvokeAsync(() => uploadComponent.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([file.Object])));
            file.Verify(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);

            file.Setup(x => x.Size).Returns(maxFileSizeInBytes - 1);
            await renderer.InvokeAsync(() => uploadComponent.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([file.Object])));
            file.Verify(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);

            var loadButton = renderer.FindComponent<RadzenButton>();
            await renderer.InvokeAsync(loadButton.Instance.Click.InvokeAsync);
            this.reqIfLoaderService.Verify(x => x.LoadAsync(It.IsAny<Stream>(), It.IsAny<SupportedFileExtensionKind>(), It.IsAny<CancellationToken>()), Times.Once);

            var cancelButton = renderer.FindComponents<RadzenButton>()[1];
            await renderer.InvokeAsync(cancelButton.Instance.Click.InvokeAsync);
            Assert.That(renderer.Instance.IsLoading, Is.EqualTo(false));

            var clearButton = renderer.FindComponents<RadzenButton>()[2];
            await renderer.InvokeAsync(clearButton.Instance.Click.InvokeAsync);
            this.reqIfLoaderService.Verify(x => x.Reset(), Times.AtLeastOnce);
        }
    }
}
