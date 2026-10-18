// -------------------------------------------------------------------------------------------------
// <copyright file="MainLayoutTestFixture.cs" company="Starion Group S.A.">
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

namespace ReqifViewer.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using BlazorBlueprint.Components;

    using Bunit;
    using Bunit.TestDoubles;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    using Radzen;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using reqifviewer.Pages.Index;
    using reqifviewer.Shared;

    using TestContext = Bunit.TestContext;

    /// <summary>
    /// Suite of tests for the <see cref="MainLayout"/> component
    /// </summary>
    [TestFixture]
    public class MainLayoutTestFixture
    {
        private TestContext context;

        [SetUp]
        public void SetUp()
        {
            this.context = new TestContext();
            this.context.JSInterop.Mode = JSRuntimeMode.Loose;
            this.context.Services.AddBlazorBlueprintComponents();

            this.context.Services.AddScoped<Radzen.DialogService>();

            // The About dialog downloads the license text; answer it without going to the network
            this.context.Services.AddSingleton(new HttpClient(new StubHttpMessageHandler()));
            this.context.Services.AddScoped<NotificationService>();
            this.context.Services.AddScoped<TooltipService>();
            this.context.Services.AddScoped<ContextMenuService>();

            var reqIfLoaderService = new Mock<IReqIFLoaderService>();
            reqIfLoaderService.Setup(x => x.ReqIFData).Returns([]);
            this.context.Services.AddSingleton(reqIfLoaderService.Object);
        }

        [TearDown]
        public void TearDown()
        {
            this.context.Dispose();
        }

        /// <summary>
        /// Answers every request with a short text, so components that download files do not reach the network
        /// </summary>
        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("license text") });
            }
        }

        private IRenderedComponent<MainLayout> RenderLayout()
        {
            return this.context.RenderComponent<MainLayout>(parameters => parameters.Add(x => x.Body, (RenderFragment)(builder => builder.AddContent(0, "page content"))));
        }

        [Test]
        public void Verify_that_MainLayout_renders_the_page_body()
        {
            var renderer = this.RenderLayout();

            Assert.That(renderer.Find("main.app-content").TextContent, Does.Contain("page content"));
        }

        [Test]
        public void Verify_that_About_button_opens_the_About_dialog()
        {
            string openedTitle = null;
            Type openedType = null;

            var renderer = this.RenderLayout();

            this.context.Services.GetRequiredService<Radzen.DialogService>().OnOpen += (title, type, _, _) =>
            {
                openedTitle = title;
                openedType = type;
            };

            renderer.FindAll("header button").Single(x => x.TextContent.Trim() == "About").Click();

            Assert.That(openedTitle, Is.EqualTo("About ReqIF Viewer"));
            Assert.That(openedType, Is.EqualTo(typeof(AboutComponent)));
        }

        [Test]
        public void Verify_that_the_GitHub_link_opens_in_a_new_tab_without_opener_or_referrer()
        {
            var renderer = this.RenderLayout();

            var link = renderer.Find("a[href=\"https://github.com/STARIONGROUP/reqifviewer\"]");

            Assert.That(link.GetAttribute("target"), Is.EqualTo("_blank"));
            Assert.That(link.GetAttribute("rel")?.Split(' '), Is.SupersetOf(new[] { "noopener", "noreferrer" }));
        }

        [Test]
        public void Verify_that_the_mobile_sidebar_closes_after_navigating()
        {
            var renderer = this.RenderLayout();
            var provider = renderer.FindComponent<BbSidebarProvider>();

            Assert.That(provider.Instance.OpenMobile, Is.False);

            // The user opens the drawer on a small screen
            renderer.InvokeAsync(() => provider.Instance.OpenMobileChanged.InvokeAsync(true)).GetAwaiter().GetResult();
            renderer.Render();
            Assert.That(renderer.FindComponent<BbSidebarProvider>().Instance.OpenMobile, Is.True);

            // ... and follows a link
            this.context.Services.GetRequiredService<FakeNavigationManager>().NavigateTo("reqifstatistics/doc-a");

            renderer.WaitForAssertion(() => Assert.That(renderer.FindComponent<BbSidebarProvider>().Instance.OpenMobile, Is.False));
        }

        [Test]
        public void Verify_that_MainLayout_stops_listening_to_navigation_when_disposed()
        {
            this.RenderLayout();
            this.context.DisposeComponents();

            var navigationManager = this.context.Services.GetRequiredService<FakeNavigationManager>();

            Assert.DoesNotThrow(() => navigationManager.NavigateTo("reqifstatistics/doc-a"));
        }
    }
}
