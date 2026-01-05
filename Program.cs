using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.EntityFrameworkCore;
using YmmcContainerTrackerApi.Data;
using YmmcContainerTrackerApi.Services;

namespace YmmcContainerTrackerApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controllers (API)
            builder.Services.AddControllers();

            // LDAP Infrastructure
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ILdapService, LdapService>(); 
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IAuditService, AuditService>();

            //  Windows Authentication
            builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme); 
            builder.Services.AddAuthorization();

            // Razor Pages (UI)
            var razor = builder.Services.AddRazorPages();
            if (builder.Environment.IsDevelopment())
            {
                razor.AddRazorRuntimeCompilation();
            }

            // Swagger (API docs)
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // EF Core
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlServerOptions => sqlServerOptions.CommandTimeout(120)
                )
            );

            var app = builder.Build();

            // AD connectivity check for prod
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("=== APPLICATION STARTUP -AD Configuration Check ===");

            // Log which environment and config files are being used
            logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
            logger.LogInformation("Config files loaded: base settings + {Environment} settings (if exists)", builder.Environment.EnvironmentName);

            var authEnabled = builder.Configuration.GetValue<bool>("Authentication:Enabled");
            var domain = builder.Configuration.GetValue<string>("Authentication:LDAP:Domain");
            var ldapPath = builder.Configuration.GetValue<string>("Authentication:LDAP:Path");
            var requiredGroup = builder.Configuration.GetValue<string>("Authentication:RequiredAdGroup");

            logger.LogInformation("Authentication Enabled: {AuthEnabled}", authEnabled);
            logger.LogInformation("AD Domain: {Domain}", domain ?? "NULL");
            logger.LogInformation("LDAP Path: {LdapPath}", ldapPath ?? "NULL");
            logger.LogInformation("Required AD group: {Group}", requiredGroup ?? "NULL");
            




            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            //  Authentication happens here automatically
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapRazorPages();

            app.Run();
        }
    }
}
