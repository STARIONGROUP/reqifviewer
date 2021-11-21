// -------------------------------------------------------------------------------------------------
// <copyright file="ReqIFLoaderServiceTestFixture.cs" company="RHEA System S.A.">
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

namespace ReqifViewer.Infrastructure.Tests.Services
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;

    using ReqifViewer.Infrastructure.Services;

    /// <summary>
    /// Suite of tests for the <see cref="ReqIFLoaderService"/> class
    /// </summary>
    public class ReqIFLoaderServiceTestFixture
    {
        private ReqIFLoaderService reqIfLoaderService;

        [SetUp]
        public void Setup()
        {
            this.reqIfLoaderService = new ReqIFLoaderService();
        }

        [Test]
        public async Task Verify_that_ReqIF_data_is_loaded_and_set_to_ReqIFData()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var reqifPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "ProR_Traceability-Template-v1.0.reqif");

            using var fileStream = new FileStream(reqifPath, FileMode.Open);
            await this.reqIfLoaderService.Load(fileStream);

            Assert.That(this.reqIfLoaderService.ReqIFData, Is.Not.Empty);

            var reqIF = this.reqIfLoaderService.ReqIFData.First();

            Assert.That(reqIF.TheHeader.Title, Is.EqualTo("Traceability Template"));
        }

        [Test]
        public async Task Verify_that_ReqIF_data_with_objects_is_loaded_and_set_to_ReqIFData()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var reqifPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "requirements-and-objects.reqifz");

            using var fileStream = new FileStream(reqifPath, FileMode.Open);
            await this.reqIfLoaderService.Load(fileStream);

            Assert.That(this.reqIfLoaderService.ReqIFData, Is.Not.Empty);

            var reqIF = this.reqIfLoaderService.ReqIFData.First();

            Assert.That(reqIF.TheHeader.Title, Is.EqualTo("Subset026"));
        }
    }
}