using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PoolMate.Api.Data;
using PoolMate.Api.Hubs;
using PoolMate.Api.Integrations.Cloudinary36;
using PoolMate.Api.Integrations.Email;
using PoolMate.Api.Integrations.FargoRate;
using PoolMate.Api.Models;
using PoolMate.Api.Services;
using System.Text;
using System.Text.Json.Serialization;
using PoolMate.Api.Dtos.Response;
using System.Text.Json;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000", // React dev server
                "http://localhost:3001", // Alternative React port
                "https://localhost:3000", // HTTPS React dev
                "https://localhost:3001" // HTTPS alternative
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    // Policy for production (thêm domain thật khi deploy)
    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy.WithOrigins("https://your-production-domain.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Controllers + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)
        );
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PoolMate.Api", Version = "v1" });

    c.CustomSchemaIds(type => type.ToString());

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter token follow:{your JWT}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// DbContext (SQL Server)
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("ConnStr")));

// Identity 
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.User.RequireUniqueEmail = true;
        opt.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// JWT Bearer
var jwt = builder.Configuration.GetSection("JWT");
builder.Services.AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["ValidIssuer"],
            ValidateAudience = true,
            ValidAudience = jwt["ValidAudience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userManager = context.HttpContext.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await userManager.FindByIdAsync(userId);
                    if (user == null ||
                        (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow))
                    {
                        context.Fail("This account has been deactivated/locked.");
                    }
                }
            },
            //  401 Unauthorized 
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var response =
                    ApiResponse<object>.Fail(401, "Unauthorized: You are not logged in or token is invalid.");
                var json = JsonSerializer.Serialize(response);
                return context.Response.WriteAsync(json);
            },

            // 403 Forbidden
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var response = ApiResponse<object>.Fail(403,
                    "Forbidden: You do not have permission to access this resource.");
                var json = JsonSerializer.Serialize(response);
                return context.Response.WriteAsync(json);
            }
        };
    });

//Email
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();

//FargoRate
builder.Services.Configure<FargoRateOptions>(
    builder.Configuration.GetSection(FargoRateOptions.SectionName));

builder.Services.AddHttpClient<IFargoRateService, FargoRateService>(client =>
{
    client.BaseAddress = new Uri("https://dashboard.fargorate.com/api/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "YourTournamentApp/1.0");
});

// Add Memory Cache
builder.Services.AddMemoryCache();

builder.Services.Configure<TableTokenOptions>(builder.Configuration.GetSection(TableTokenOptions.SectionName));
builder.Services.AddSingleton<IMatchLockService, MatchLockService>();
builder.Services.AddScoped<ITableTokenService, TableTokenService>();

// App services
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<IPostService, PostService>();

builder.Services.AddScoped<ITournamentService, TournamentService>();

builder.Services.AddScoped<IVenueService, VenueService>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IBracketService, BracketService>();

builder.Services.AddScoped<IAdminUserService, AdminUserService>();

builder.Services.AddScoped<IAdminPlayerService, AdminPlayerService>();

builder.Services.AddScoped<IPlayerProfileService, PlayerProfileService>();

builder.Services.AddScoped<IAdminPayoutService, AdminPayoutService>();

// Cloudinary
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var acc = new Account(
        cfg["Cloudinary:CloudName"],
        cfg["Cloudinary:ApiKey"],
        cfg["Cloudinary:ApiSecret"]);
    return new Cloudinary(acc) { Api = { Secure = true } };
});

builder.Services.Configure<CloudinaryOptions>(
    builder.Configuration.GetSection("Cloudinary"));

builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

// Dashboard Data Seeder (for testing)
builder.Services.AddScoped<DashboardDataSeeder>();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors("ReactPolicy");
}
else
{
    app.UseCors("ProductionPolicy");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TournamentHub>("/hubs/tournament");
app.Run();