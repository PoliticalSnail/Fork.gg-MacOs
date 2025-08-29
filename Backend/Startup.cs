using System;
using System.IO;
using Fork.Logic.Managers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Fork.Adapters.Mojang;
using Fork.Logic.Services.StateServices;
using Microsoft.EntityFrameworkCore.Query;
using Fork.Adapters.PaperMc;
using Fork.Logic.Services.WebServices;
using Fork.Adapters.Waterfall;
using Fork.Adapters.Purpur;
using Fork.Adapters.Fork;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Fork.Logic.Persistence;
using Fork.Logic.Services.AuthenticationServices;
using Fork.Logic.Notification;
using Fork.Logic.Services.FileServices;
using Fork.Logic.Services.EntityServices;

namespace Fork
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // CORS
            services.AddCors(policy =>
            {
                policy.AddPolicy("CorsPolicy", opt =>
                    opt.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            });

            // Managers & services
            services.AddSingleton<ApplicationManager>();
            services.AddScoped<EntityPostProcessingService>();
            
            services.AddSingleton<TokenManager>();
            services.AddScoped<AuthenticationService>();
            services.AddSingleton<NotificationCenter>();
            services.AddSingleton<ApplicationStateService>();
            services.AddControllers();
            services.AddSingleton<EntityManager>();
            services.AddSingleton<MojangApiAdapter>();
            services.AddSingleton<PaperMcApiAdapter>();
            services.AddSingleton<WaterfallApiAdapter>();
            services.AddSingleton<PurpurApiAdapter>();
            services.AddSingleton<ForkApiAdapter>();
            services.AddSingleton<ServerVersionManager>();

            // Web services
            services.AddTransient<DownloadService>();

            // File services
            services.AddTransient<FileReaderService>();
            services.AddTransient<FileWriterService>();

            // Entity services
            services.AddTransient<ConsoleService>();
            services.AddTransient<ServerService>();
            services.AddTransient<ConsoleInterpreter>();
            services.AddTransient<EntityService>();
            services.AddTransient<PlayerService>();

            // Database
            SqliteConnectionStringBuilder builder = new(Configuration.GetConnectionString("DefaultConnection"));
            builder.DataSource = builder.DataSource.Replace("|datadirectory|",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ForkApp", "persistence"));
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(builder.ToString())
            );

            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Initialize NotificationCenter eagerly
            app.ApplicationServices.GetService<NotificationCenter>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("CorsPolicy");

            // Serve Blazor WebAssembly published folder
            string wasmRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents/GitHub/Fork/Frontend/bin/Release/net8.0/publish/wwwroot"
            );

            if (!Directory.Exists(wasmRoot))
                throw new DirectoryNotFoundException($"Blazor WASM folder not found: {wasmRoot}");

            var fileProvider = new PhysicalFileProvider(wasmRoot);

            // Serve _framework and other static files
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = ""
            });

            app.UseRouting();

            // Custom authentication middleware for API endpoints
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    var authService = context.RequestServices.GetRequiredService<AuthenticationService>();

                    if (!context.Request.Headers.TryGetValue("X-Fork-Token", out var tokenValues)
                        || string.IsNullOrWhiteSpace(tokenValues))
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Unauthorized: Missing token");
                        return;
                    }

                    string token = tokenValues.ToString();

                    try
                    {
                        authService.AuthenticateToken(token);
                    }
                    catch
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Unauthorized: Invalid token");
                        return;
                    }
                }

                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // SPA fallback for Blazor: manually serve index.html
                endpoints.MapFallback(async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync(Path.Combine(wasmRoot, "index.html"));
                });
            });
        }
    }
}
