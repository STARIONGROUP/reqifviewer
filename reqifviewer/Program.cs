// -------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Starion Group S.A.">
//
//   Copyright 2021-2024 Starion Group S.A.
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

namespace reqifviewer
{
    using System.Threading.Tasks;

    using Blazor.Analytics;

    using BlazorStrap;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Builder;

    using ReqIFSharp;
    using ReqIFSharp.Extensions.Services;

    using Radzen;
    
    using Serilog;
    using Microsoft.Extensions.Logging;
    using ReqifViewer.Resources;
    using Microsoft.AspNetCore.HttpOverrides;

    /// <summary>
    /// The purpose of the <see cref="Program"/> class is to provide the
    /// main entry point of te application
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point of the application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>
        /// an awaitable <see cref="Task"/>
        /// </returns>
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            
            builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .WriteTo.Console();
            });

            builder.Services.AddSingleton<IResourceLoader, ResourceLoader>();
            builder.Services.AddScoped<DialogService>();
            builder.Services.AddScoped<NotificationService>();
            builder.Services.AddScoped<TooltipService>();
            builder.Services.AddScoped<ContextMenuService>();
            builder.Services.AddScoped<IReqIFDeSerializer, ReqIFDeserializer>();
            builder.Services.AddScoped<IReqIFLoaderService, ReqIFLoaderService>();
            builder.Services.AddGoogleAnalytics("295704041");
            builder.Services.AddBlazorStrap();
            builder.Services.AddHttpClient();

            var app = builder.Build();

            app.UseForwardedHeaders(
                new ForwardedHeadersOptions
                {
                    ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                }
            );

            var logger = app.Services.GetService<ILogger<Program>>();
            var resourceLoader = app.Services.GetService<IResourceLoader>();
            var logo = resourceLoader.QueryLogo();
            logger.LogInformation("################################################################");
            logger.LogInformation(logo);
            logger.LogInformation("################################################################");

            app.UseStaticFiles();
            app.UseRouting();
            app.MapBlazorHub();
            app.MapRazorPages();
            app.MapFallbackToPage("/_Host");

            await app.RunAsync();
        }
    }
}
