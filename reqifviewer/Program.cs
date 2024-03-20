// -------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="RHEA System S.A.">
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
    using Serilog.Events;

    /// <summary>
    /// The purpose of the <see cref="Program"/> class is to provide the
    /// main entry point of te application
    /// </summary>
    public static class Program
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
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.BrowserConsole()
                .CreateLogger();

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            
            builder.Services.AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true));

            builder.Services.AddScoped<DialogService>();
            builder.Services.AddScoped<NotificationService>();
            builder.Services.AddScoped<TooltipService>();
            builder.Services.AddScoped<ContextMenuService>();
            builder.Services.AddScoped<IReqIFDeSerializer, ReqIFDeserializer>();
            builder.Services.AddScoped<IReqIFLoaderService, ReqIFLoaderService>();
            builder.Services.AddGoogleAnalytics("295704041");
            builder.Services.AddBlazorStrap();

            var app = builder.Build();

            app.UseStaticFiles();
            app.UseRouting();
            app.MapBlazorHub();
            app.MapRazorPages();
            app.MapFallbackToPage("/_Host");

            await app.RunAsync();
        }
    }
}
