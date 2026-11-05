using System.Security.Claims;
using System.Text;
using GestionCitas.Api.Data;
using GestionCitas.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<CommunicationSettings>(
    builder.Configuration.GetSection("Communications"));
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<ProfessionalAgendaService>();
builder.Services.AddScoped<AppointmentCommunicationService>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<EmailOutboxService>();
builder.Services.AddHostedService<AppointmentCommunicationWorker>();
builder.Services.AddHostedService<EmailOutboxWorker>();

// HU-37: validación de DNI con servicio externo (token en DniApi:Token) y límite de consultas por IP.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<DniLookupService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DniApi:BaseUrl"] ?? "https://dniruc.apisperu.com/api/v1/");
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("dni", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1)
        }));
    options.OnRejected = async (context, token) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Hiciste muchas consultas seguidas. Espera un minuto e intenta otra vez." },
            token);
});

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Configura Jwt:Key en appsettings.Development.json con al menos 32 caracteres. " +
        "Usa appsettings.Development.example.json como referencia.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();

// HU-03/HU-06: evita que un JWT antiguo conserve acceso después de desactivar
// al usuario o cambiarle el rol. En ese caso se exige iniciar sesión nuevamente.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tokenRole = context.User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(idValue, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var currentUser = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.IsActive, x.Role })
            .FirstOrDefaultAsync();

        if (currentUser is null || !currentUser.IsActive || !string.Equals(currentUser.Role, tokenRole, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Tu acceso cambió. Inicia sesión nuevamente." });
            return;
        }
    }

    await next();
});

app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbInitializer.SeedAsync(db);
}

app.Run();
