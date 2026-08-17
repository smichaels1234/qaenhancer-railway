using System.Text;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Explicitly set URLs
builder.WebHost.UseUrls("https://localhost:7058", "http://localhost:5252");

// Get the connection string from the configuration
var connectionString = builder.Configuration.GetConnectionString("QAConnection") ??
    throw new InvalidOperationException("Connection string 'QAConnection' not found.");

// Add your DbContext with the PostgreSQL provider with optimized settings
builder.Services.AddDbContext<QAEnhancerDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "qaenhancer");
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    });
    
    // Add connection pooling configuration
    options.EnableServiceProviderCaching();
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

// Add services to the container.

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings - Disable for development to speed up login
    options.Lockout.AllowedForNewUsers = false;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<QAEnhancerDbContext>()
.AddDefaultTokenProviders();

// Configure faster password hasher for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<PasswordHasherOptions>(options =>
    {
        options.IterationCount = 10_000; // Default is 100,000+ - reduce for dev
    });
}

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Set to true in production
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// Register Token Service
builder.Services.AddScoped<ITokenService, TokenService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4204", "https://localhost:4204")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();

// Add HttpClient for AI Analysis service
builder.Services.AddHttpClient();

// Register AI Bug Detection Service
builder.Services.AddScoped<AIBugDetectionService>();
builder.Services.AddScoped<PlanEntitlementService>();

// Add health checks for the database
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database");

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Use CORS
app.UseCors("AllowAngularApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Seed admin user on startup
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<QAEnhancerDbContext>();

    const string adminEmail = "admin@qaenhancer.local";
    const string adminPassword = "Admin123!";

    var existing = await userManager.FindByEmailAsync(adminEmail);
    if (existing == null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Admin",
            OrganizationId = "qaenhancer-admin",
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            dbContext.Subscriptions.Add(new Subscription
            {
                UserId = adminUser.Id,
                PlanType = "free",
                Status = "active",
                StartDate = DateTime.UtcNow,
                AutoRenew = false
            });
            await dbContext.SaveChangesAsync();
        }
    }
}

app.MapFallbackToFile("index.html");

app.Run();
