using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Configuration;
using System;
using System.Text;
using TrigramBooking.API.Data;
using TrigramBooking.API.Middleware;
using TrigramBooking.API.Models;
namespace TrigramBooking.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Logger first, so everything below can log
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            try
            {
                builder.Host.UseSerilog();

                builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
                var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();

                // 2. The key check, before AddAuthentication uses the key
                if (jwtSettings is null
                    || string.IsNullOrWhiteSpace(jwtSettings.Key)
                    || Encoding.UTF8.GetBytes(jwtSettings.Key).Length < 32)
                {
                    throw new InvalidOperationException(
                        "JWT signing key is missing or shorter than 32 bytes. Check Jwt:Key in user-secrets.");
                }

                builder.Services.AddAuthentication(/* ...your existing block... */);

                builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
                builder.Services.AddProblemDetails();
                // ...rest of your service registrations, once each...

                var app = builder.Build();

                app.UseSerilogRequestLogging(/* ...your enriched version only... */);
                // ...DbInitializer, swagger, UseExceptionHandler, auth, MapControllers...

                Log.Information("Starting TrigramBooking API host...");
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The application host terminated unexpectedly during startup!");
                throw; // 3. non-zero exit code
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
