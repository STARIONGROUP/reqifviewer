// -------------------------------------------------------------------------------------------------
// <copyright file="SpecElementWithAttributesExtensionTestFixture.cs" company="RHEA System S.A.">
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

namespace ReqifViewer.Tests.ReqIFExtensions
{
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;

    using NUnit.Framework;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using ReqifViewer.ReqIFExtensions;

    /// <summary>
    /// Suite of tests for the <see cref="SpecElementWithAttributesExtensions"/>
    /// </summary>
    [TestFixture]
    public class SpecElementWithAttributesExtensionTestFixture
    {
        private ReqIF reqIf;

        [SetUp]
        public async Task SetUp()
        {
            var reqIfDeserializer = new ReqIFDeserializer();

            var cts = new CancellationTokenSource();

            var reqifPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "ProR_Traceability-Template-v1.0.reqif");

            await using var fileStream = new FileStream(reqifPath, FileMode.Open);
            var reqIfLoaderService = new ReqIFLoaderService(reqIfDeserializer);
            await reqIfLoaderService.Load(fileStream, SupportedFileExtensionKind.Reqif, cts.Token);

            this.reqIf = reqIfLoaderService.ReqIFData.Single();
        }

        [Test]
        public void Verify_that_ExtractSpecificationName_returns_expected_result()
        {
            var specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.Identifier == "_o7scQ6dbEeafNduaIhMwQg");
            var markupString = new MarkupString("<xhtml:div xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">Stakeholder Requirements</xhtml:div>");
            Assert.That(specObject.ExtractDisplayName(), Is.EqualTo(markupString));

            var specification = this.reqIf.CoreContent.Specifications.Single(x => x.Identifier == "_o7scS6dbEeafNduaIhMwQg");
            Assert.That(specification.ExtractDisplayName(), Is.EqualTo("Requirements Document"));

            specObject = this.reqIf.CoreContent.SpecObjects.Single(x => x.Identifier == "_DqMpsKddEeafNduaIhMwQg");
            Assert.That(specObject.ExtractDisplayName(), Is.EqualTo("REQ-6"));
        }
    }
}
