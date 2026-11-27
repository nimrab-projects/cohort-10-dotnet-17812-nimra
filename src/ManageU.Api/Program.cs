using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ManageU.Api.Middleware;
using ManageU.Application.Common.Interfaces;
using ManageU.Core.Constants;
using ManageU.Infrastructure;
using ManageU.Infrastructure.Authentication;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/manageu-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ManageU API host...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/manageu-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14));

    builder.Services.AddInfrastructureServices(builder.Configuration);

    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
        ?? throw new InvalidOperationException("JwtSettings section is missing from configuration.");

    if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
    {
        throw new InvalidOperationException("JwtSettings:Secret must be configured via User Secrets or environment variables.");
    }

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdminRole", policy => policy.RequireRole(Roles.Admin));
        options.AddPolicy("RequireUserRole", policy => policy.RequireRole(Roles.User));
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    var app = builder.Build();

    // Seed initial roles idempotently on startup
    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IIdentitySeeder>();
        await seeder.SeedAsync();
    }

    app.UseMiddleware<GlobalExceptionMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not Microsoft.Extensions.Hosting.HostAbortedException)
{
    Log.Fatal(ex, "ManageU API host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
