using System.Text;
using System.Text.Json;
using DotNetEnv;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Common;
using FreightLink.Api.Middleware;
using FreightLink.Api.Services;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Load .env into the process environment (if present) before the builder reads configuration,
// since AddEnvironmentVariables() snapshots env vars at builder-creation time.
var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFilePath))
{
    Env.Load(envFilePath);
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Override MVC's default validation-failure response so DTO validation errors match the
// project-wide error envelope shape instead of the default ValidationProblemDetails.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(error => new ValidationErrorItemDto
                {
                    Field = kvp.Key,
                    Issue = error.ErrorMessage
                }))
                .ToList();

            var envelope = new ErrorEnvelopeDto(new ErrorDetailDto
            {
                Code = ErrorCode.VALIDATION_ERROR.ToString(),
                Message = "One or more validation errors occurred.",
                Details = details
            });

            return new BadRequestObjectResult(envelope);
        };
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Connection string comes only from ConnectionStrings:DefaultConnection (env var
// CONNECTIONSTRINGS__DEFAULTCONNECTION) — never hardcoded in appsettings.json. The mixed-case
// section/key names here vs. .env.example's SCREAMING_CASE are not a mismatch to fix: ASP.NET
// Core's environment-variable configuration provider maps "__" to ":" and every configuration
// lookup (GetSection, GetConnectionString, the [] indexer) is case-insensitive by design, so
// "ConnectionStrings:DefaultConnection" and "CONNECTIONSTRINGS__DEFAULTCONNECTION" are the same
// key — deliberately keeping C# lookups in the PascalCase that matches the bound POCO property
// names (e.g. JwtOptions.Issuer below), rather than forcing ALL-CAPS C# to visually match the env
// var spelling, which the "__"-to-":" translation would make misleading anyway.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT settings (Jwt:* / JWT__* env vars) used both to bind JwtOptions for DI and, immediately
// below, to configure the JwtBearer handler's signing-key/issuer/audience validation. Same
// case-insensitive "__"-to-":" mapping as the connection string above.
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

// Microsoft.IdentityModel.Tokens enforces a minimum 256-bit (32-byte) key for HS256 signing —
// a shorter JWT__KEY doesn't fail here, it fails later inside TokenService.GenerateAccessToken
// on the first successful login, surfacing as an opaque 500. Fail fast at startup instead.
const int minimumJwtKeyBytes = 32;
if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < minimumJwtKeyBytes)
{
    throw new InvalidOperationException(
        $"JWT__KEY must be at least {minimumJwtKeyBytes} bytes (256 bits) for HS256 signing; " +
        $"the configured key is only {Encoding.UTF8.GetByteCount(jwtOptions.Key)} bytes. " +
        "Generate a longer key, e.g. `openssl rand -base64 32`.");
}

// Admin-seed credentials are flat keys (no "__" section), so bind them manually rather than
// via GetSection().Bind().
builder.Services.Configure<AdminSeedOptions>(options =>
{
    options.Email = builder.Configuration["ADMIN_USER_EMAIL"];
    options.Password = builder.Configuration["ADMIN_USER_PASSWORD"];
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Without these, a missing/invalid/expired token or a failed role/policy check is handled
        // directly by the JwtBearer/authorization middleware — never as a thrown exception — so it
        // bypasses ExceptionHandlingMiddleware entirely and falls back to ASP.NET's bare,
        // envelope-less default response instead of this project's standard error shape.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                // Suppresses the default response (which only sets a WWW-Authenticate header and
                // an empty 401 body) so we can write the standard envelope in its place.
                context.HandleResponse();
                return WriteAuthErrorAsync(context.Response, StatusCodes.Status401Unauthorized, ErrorCode.UNAUTHORIZED,
                    "Authentication is required, or the supplied token is missing, invalid, or expired.");
            },
            OnForbidden = context =>
                WriteAuthErrorAsync(context.Response, StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN,
                    "You do not have permission to perform this action.")
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Apply any pending EF Core migrations once at startup outside Production. Migrate() only
// applies migrations not yet recorded in __EFMigrationsHistory — it never drops/recreates
// existing tables. Production schema changes are a separate, deliberate manual step.
if (!app.Environment.IsProduction())
{
    using var migrationScope = app.Services.CreateScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Seed the default Admin account (from ADMIN_USER_EMAIL/ADMIN_USER_PASSWORD) once per startup,
// in every environment — there is no public admin registration endpoint.
using (var seedScope = app.Services.CreateScope())
{
    var authService = seedScope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.SeedAdminIfNotExistsAsync();
}

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>
/// Writes the standard <c>{ "error": { code, message } }</c> envelope directly to an in-flight
/// authentication/authorization response — used by the <c>JwtBearerEvents</c> handlers above,
/// which run outside <see cref="FreightLink.Api.Middleware.ExceptionHandlingMiddleware"/>'s
/// exception-catching scope since no exception is thrown for a 401/403 auth failure.
/// </summary>
/// <param name="response">The in-flight HTTP response to write to.</param>
/// <param name="statusCode">The HTTP status code to set.</param>
/// <param name="code">The machine-readable error code.</param>
/// <param name="message">The human-readable error message.</param>
static async Task WriteAuthErrorAsync(HttpResponse response, int statusCode, ErrorCode code, string message)
{
    response.ContentType = "application/json";
    response.StatusCode = statusCode;

    var envelope = new ErrorEnvelopeDto(new ErrorDetailDto { Code = code.ToString(), Message = message });
    var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    await response.WriteAsync(json);
}

/// <summary>
/// Marker partial class exposing this top-level-statement entry point to
/// <c>WebApplicationFactory&lt;Program&gt;</c> in the integration test project.
/// </summary>
public partial class Program;
