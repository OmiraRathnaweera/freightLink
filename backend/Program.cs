using System.Text;
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
// CONNECTIONSTRINGS__DEFAULTCONNECTION) — never hardcoded in appsettings.json.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT settings (Jwt:* / JWT__* env vars) used both to bind JwtOptions for DI and, immediately
// below, to configure the JwtBearer handler's signing-key/issuer/audience validation.
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
/// Marker partial class exposing this top-level-statement entry point to
/// <c>WebApplicationFactory&lt;Program&gt;</c> in the integration test project.
/// </summary>
public partial class Program;
