using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Text;
using TrigramBooking.API.Data;
using TrigramBooking.API.Middleware;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services;

namespace TrigramBooking.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            try
            {
                builder.Host.UseSerilog();

                // 2. Configure JWT settings
                builder.Services.Configure<JwtSettings>(
                    builder.Configuration.GetSection("Jwt"));

                var jwtSettings = builder.Configuration
                    .GetSection("Jwt")
                    .Get<JwtSettings>();

                // 3. Validate JWT signing key before configuring authentication
                if (jwtSettings is null
                    || string.IsNullOrWhiteSpace(jwtSettings.Key)
                    || Encoding.UTF8.GetBytes(jwtSettings.Key).Length < 32)
                {
                    throw new InvalidOperationException(
                        "JWT signing key is missing or shorter than 32 bytes. Check Jwt:Key in user-secrets.");
                }

                // 4. Authentication
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,

                            ValidIssuer = jwtSettings.Issuer,
                            ValidAudience = jwtSettings.Audience,

                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtSettings.Key))
                        };
                    });

                // 5. Authorization
                builder.Services.AddAuthorization();

                // 6. Controllers
                builder.Services.AddControllers();
                builder.Services.AddScoped<IResourceService, ResourceService>();
                builder.Services.AddScoped<IBookingService, BookingService>();
                builder.Services.AddScoped<IAuthService, AuthService>();

                // 7. Exception handling
                builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
                builder.Services.AddProblemDetails();

                // 8. Database
                builder.Services.AddDbContext<TrigramDbContext>(options =>
                    options.UseSqlServer(
                        builder.Configuration.GetConnectionString("DefaultConnection")));

                // 9. Swagger
                builder.Services.AddSwaggerGen(options =>
                {
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "Enter: Bearer {your token}"
                    });

                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    });
                });

                // 10. Build application
                var app = builder.Build();

                // 11. Initialize database
                using (var scope = app.Services.CreateScope())
                {
                    var context = scope.ServiceProvider
                        .GetRequiredService<TrigramDbContext>();

                    DbInitializer.Initialize(context);
                }

                // 12. Serilog request logging
                app.UseSerilogRequestLogging(options =>
                {
                    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                    {
                        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        diagnosticContext.Set("RemoteIp", ip);
                        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
                    };
                });

                // 13. Swagger
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                // 14. Exception handling
                app.UseExceptionHandler();

                // 15. HTTPS redirection
                app.UseHttpsRedirection();

                // 16. Authentication and authorization
                app.UseAuthentication();
                app.UseAuthorization();

                // 17. Map controller endpoints
                app.MapControllers();

                Log.Information("Starting TrigramBooking API host...");

                // 18. Start application
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The application host terminated unexpectedly during startup!");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}