// -------------------------------------------------------------------------------------------------
// <copyright file="HostingTestFixture.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Tests
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc.Testing;

    using NUnit.Framework;

    using reqifviewer;

    /// <summary>
    /// Starts the whole application in memory and checks the host page and the not-found page
    /// </summary>
    [TestFixture]
    public class HostingTestFixture
    {
        private WebApplicationFactory<Program> factory;

        private HttpClient client;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            this.factory = new WebApplicationFactory<Program>();
            this.client = this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            this.client.Dispose();
            this.factory.Dispose();
        }

        [Test]
        public async Task Verify_that_the_root_returns_the_host_page()
        {
            var response = await this.client.GetAsync("/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("_framework/blazor.web.js"));
            Assert.That(html, Does.Contain("ReqIF Viewer"));
        }

        [Test]
        public async Task Verify_that_the_theme_is_loaded_before_the_Blazor_Blueprint_stylesheet()
        {
            var html = await this.client.GetStringAsync("/");

            var theme = html.IndexOf("css/theme.", System.StringComparison.Ordinal);
            var blueprint = html.IndexOf("blazorblueprint.css", System.StringComparison.Ordinal);

            Assert.That(theme, Is.GreaterThanOrEqualTo(0), "theme.css is not referenced");
            Assert.That(blueprint, Is.GreaterThan(theme), "theme.css must be loaded before blazorblueprint.css");
        }

        [Test]
        public async Task Verify_that_an_unknown_address_returns_the_not_found_page()
        {
            var response = await this.client.GetAsync("/this-page-does-not-exist");
            var html = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(html, Does.Contain("Sorry, there&#x27;s nothing at this address.").Or.Contain("Sorry, there's nothing at this address."));
            Assert.That(html, Does.Contain("_framework/blazor.web.js"), "the not-found page is rendered inside the app");
        }
    }
}
