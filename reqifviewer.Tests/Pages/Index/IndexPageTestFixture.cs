// -------------------------------------------------------------------------------------------------
// <copyright file="IndexPageTestFixture.cs" company="RHEA System S.A.">
//
//   Copyright 2021-2022 RHEA System S.A.
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
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Bunit;

    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    using Radzen.Blazor;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using reqifviewer.Pages.Index;

    using TestContext = Bunit.TestContext;

    /// <summary>
    /// Suite of tests for the <see cref="IndexPageTestFixture"/>
    /// </summary>
    [TestFixture]
    public class IndexPageTestFixture
    {
        private Mock<IReqIFLoaderService> reqIfLoaderService;
        private TestContext context;
        private const long MaxFileSize = 5 * 1024 * 1024;

        [SetUp]
        public void SetUp()
        {
            this.context = new TestContext();
            this.reqIfLoaderService = new Mock<IReqIFLoaderService>();

            this.context.Services.AddSingleton(this.reqIfLoaderService.Object);
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

            var file = new Mock<IBrowserFile>();
            file.Setup(x => x.Size).Returns(MaxFileSize + 1);
            file.Setup(x => x.Name).Returns("file.reqif");
            file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(new MemoryStream());

            await renderer.InvokeAsync(() => uploadComponent.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([file.Object])));
            file.Verify(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);

            file.Setup(x => x.Size).Returns(MaxFileSize - 1);
            await renderer.InvokeAsync(() => uploadComponent.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([file.Object])));
            file.Verify(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);

            var loadButton = renderer.FindComponent<RadzenButton>();
            await renderer.InvokeAsync(loadButton.Instance.Click.InvokeAsync);
            this.reqIfLoaderService.Verify(x => x.Load(It.IsAny<Stream>(), It.IsAny<SupportedFileExtensionKind>(), It.IsAny<CancellationToken>()), Times.Once);

            var cancelButton = renderer.FindComponents<RadzenButton>()[1];
            await renderer.InvokeAsync(cancelButton.Instance.Click.InvokeAsync);
            Assert.That(renderer.Instance.IsLoading, Is.EqualTo(false));

            var clearButton = renderer.FindComponents<RadzenButton>()[2];
            await renderer.InvokeAsync(clearButton.Instance.Click.InvokeAsync);
            this.reqIfLoaderService.Verify(x => x.Reset(), Times.AtLeastOnce);
        }
    }
}
