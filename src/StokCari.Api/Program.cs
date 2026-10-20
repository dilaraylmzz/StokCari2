using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using StokCari.Api.Infrastructure;
using StokCari.Api.Middleware;
using StokCari.Core.Interfaces;
using StokCari.Data;
using StokCari.Data.Interceptors;
using StokCari.Services.Caching;
using StokCari.Services.Implementations;
using StokCari.Services.Mapping;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ---------------------------------------------------------------- Serilog (yapılandırılmış JSON log)
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ---------------------------------------------------------------- Veritabanı
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
    builder.Services.AddScoped<AuditSaveChangesInterceptor>();
    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
        options
            .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

    // ---------------------------------------------------------------- Redis (cache-aside)
    // abortConnect=false: Redis kapalıysa uygulama açılır, istekler veritabanından çalışmaya devam eder.
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false,connectTimeout=2000,syncTimeout=1000,asyncTimeout=1000";
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();

    // ---------------------------------------------------------------- Kimlik doğrulama (JWT) ve yetkilendirme (RBAC)
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
    var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:Key en az 32 karakter olmalıdır. Geliştirmede 'dotnet user-secrets set \"Jwt:Key\" \"<32+ karakter>\"' kullanın.");
    }

    builder.Services.AddSingleton<ITokenService, JwtTokenService>();
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // "sub", "role" gibi claim adları olduğu gibi kalır
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                RoleClaimType = "role",
                NameClaimType = "name"
            };
        });
    builder.Services.AddAuthorization();

    // ---------------------------------------------------------------- İş katmanı
    // Entity -> DTO eşlemeleri. Lisans anahtarı repoya konmaz; user-secrets veya AutoMapper__LicenseKey ortam değişkeniyle verilir.
    builder.Services.AddAutoMapper(cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"], typeof(MappingProfile));
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ICompanyService, CompanyService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IStockService, StockService>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<IReceiptService, ReceiptService>();
    builder.Services.AddScoped<IReceiptSeriesService, ReceiptSeriesService>();

    // ---------------------------------------------------------------- Hata yönetimi (RFC 7807) ve doğrulama
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["correlationId"] =
                ctx.HttpContext.Items["CorrelationId"]?.ToString() ?? ctx.HttpContext.TraceIdentifier);

    builder.Services.AddControllers()
        // Enum'lar API'de sayı yerine metin olarak gelir/gider ("Sale", "Customer" ...).
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .ConfigureApiBehaviorOptions(options =>
        {
            // Data Annotations doğrulama hataları iş kurallarına ulaşmadan 400 döner; loglanır ve correlationId eklenir.
            options.InvalidModelStateResponseFactory = context =>
            {
                var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? context.HttpContext.TraceIdentifier;
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Doğrulama hatası",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["correlationId"] = correlationId;

                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Validation");
                logger.LogWarning("Doğrulama hatası {Path}: {Errors}", context.HttpContext.Request.Path, problem.Errors);

                return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            };
        });

    // ---------------------------------------------------------------- Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Stok, Cari ve Fiş API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Giriş yaptıktan sonra dönen token'ı yapıştırın."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
        c.OperationFilter<SwaggerCompanyHeaderFilter>();
    });

    var app = builder.Build();

    // ---------------------------------------------------------------- Middleware sırası
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnosticContext, http) =>
        {
            diagnosticContext.Set("UserId", http.User.FindFirst("sub")?.Value);
            diagnosticContext.Set("CompanyId", http.User.FindFirst("company_id")?.Value ?? http.Request.Headers[HttpCurrentUser.CompanyHeader].ToString());
        });
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<UserLogEnrichmentMiddleware>();

    app.MapControllers();

    // ---------------------------------------------------------------- Veritabanı hazırlığı ve seed
    if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Migration dosyası yoksa şema doğrudan oluşturulur (hızlı başlangıç). Üretimde migration kullanın.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
    }

    await DataSeeder.SeedAsync(app.Services, app.Configuration, app.Logger, app.Lifetime.ApplicationStopping);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Uygulama başlatılamadı");
}
finally
{
    Log.CloseAndFlush();
}
