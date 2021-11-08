using AutoMapper;
using Common.Features;
using Common.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weir.Notification.Service.DTO;
using Weir.Notification.Service.Model;
using Weir.Notification.Service;
using Weir.Notification.Service.RepositoryContract;
using Weir.Notification.Service.RepositoryImplementation;
using Weir.Notification.Service.SqlTableDependencies;
using Weir.Notification.API.Hubs;
using Weir.Notification.Service.SqlTableDependency;
using Weir.Notification.Service.ServiceContract;
using Weir.Notification.Service.ServiceImplementation;

namespace Notification
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;

            var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }
        private const string AllowAllCors = "AllowAll";

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var mapperConfig = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new MapperProfile());
            });
            IMapper mapper = mapperConfig.CreateMapper();
            services.AddSingleton(mapper);
            services.AddSignalR();
            services.AddControllers();
            services.AddSingleton<ILog, LogService>();
            services.AddCors(options =>
            {
                options.AddPolicy(AllowAllCors,
                                  builder =>
                                  {
                                      builder.AllowAnyHeader()
                                      .AllowAnyMethod()
                                      .SetIsOriginAllowed((host) => true)
                                      .AllowCredentials();
                                  });
            });
            services.AddApiVersioning(x =>
            {
                x.DefaultApiVersion = new ApiVersion(1, 0);
                x.AssumeDefaultVersionWhenUnspecified = true;
                x.ReportApiVersions = true;
            });
            services.AddSwaggerGen();            
            services.AddDbContextPool<NotificationDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("NotificationDb")));
            services.AddDbContextFactory<NotificationDbContext>(Configuration.GetConnectionString("NotificationDb"));
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddSingleton<ISqlTableDependencyRpository, SqlTableDependencyRepository>();
            services.AddSingleton<IDatabaseSubscription, NotificationHub>();
            services.AddScoped<INotification, NotificationService>();
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILog logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.ConfigureExceptionHandler(logger);
            app.UseRequestResponseLogging();
            app.UseCors(AllowAllCors);
            app.UseRouting();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("../swagger/v1/swagger.json", "Notification Hub");
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<NotificationHub>("/Hubs");
                endpoints.MapGet("/", async context =>
                {
                    if (Configuration.GetSection("AppKeys").GetSection("ProductionMode").Value == "True")
                        context.Response.Redirect("Error.html");
                    else
                        await context.Response.WriteAsync("Notification Hub is running...");
                });
            });
            app.UseSqlTableDependency<IDatabaseSubscription>(Configuration.GetConnectionString("NotificationDb"));
        }
    }
}
