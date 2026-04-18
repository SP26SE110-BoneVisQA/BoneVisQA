using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using BoneVisQA.API;
using BoneVisQA.API.ExceptionHandling;
using BoneVisQA.API.Hubs;
using BoneVisQA.API.Policies;
using BoneVisQA.API.Services;
using BoneVisQA.Domain.Settings;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Services;
using BoneVisQA.Services.Services.Admin;
using BoneVisQA.Services.Services.DocumentUpload;
using BoneVisQA.Services.Services.Rag;
using BoneVisQA.Services.Services.AiQuizServices;
using BoneVisQA.Services.Services.Auth;
using BoneVisQA.Services.Services.Email;
using BoneVisQA.Services.Services.Expert;
using BoneVisQA.Services.Services.Lecturer;
using BoneVisQA.Services.Services.Storage;
using BoneVisQA.Services.Services.Student;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

// Large multipart uploads: default Kestrel ~28MB drops the connection (ERR_CONNECTION_RESET).
const long maxUploadBodyBytes = 104857600; // 100 MB
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBodyBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBodyBytes;
    options.MemoryBufferThreshold = 1048576; // 1 MB — default-style buffering; larger parts use OS temp as needed.
});

// Add User Secrets cho development (Google OAuth credentials)
builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var originSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var o in configuredOrigins.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()))
            originSet.Add(o);

        if (builder.Environment.IsDevelopment())
        {
            foreach (var o in new[]
                     {
                         "http://localhost:3000",
                         "https://localhost:3000",
                         "http://localhost:5173",
                         "https://localhost:5173",
                         "http://localhost:5046"
                     })
                originSet.Add(o);
        }

        if (originSet.Count == 0)
        {
            foreach (var o in new[]
                     {
                         "http://localhost:3000",
                         "https://localhost:3000",
                         "http://localhost:5173",
                         "https://localhost:5173",
                         "http://localhost:5046"
                     })
                originSet.Add(o);
        }

        policy.WithOrigins(originSet.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BoneVisQA API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Nhập token sau 'Bearer '",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
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

    // Bỏ yêu cầu Bearer cho các endpoint Auth (register, login, forgot-password, reset-password)
    c.OperationFilter<SwaggerAuthFilter>();
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, JwtUserIdProvider>();

static string BuildSupabaseConnectionString(IConfiguration configuration)
{
    var raw = configuration.GetConnectionString("SupabaseDb");
    if (string.IsNullOrWhiteSpace(raw))
        throw new InvalidOperationException(
            "ConnectionStrings:SupabaseDb is missing. Set it in User Secrets, environment variables, or appsettings (never commit passwords).");

    var hasMaxPool = raw.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase)
                     || raw.Contains("Max Pool Size", StringComparison.OrdinalIgnoreCase)
                     || raw.Contains("MaxPoolSize=", StringComparison.OrdinalIgnoreCase);

    var csb = new NpgsqlConnectionStringBuilder(raw)
    {
        Pooling = true,
    };

    if (!hasMaxPool)
    {
        csb.MinPoolSize = configuration.GetValue("DatabasePooling:MinimumPoolSize", 0);
        csb.MaxPoolSize = configuration.GetValue("DatabasePooling:MaximumPoolSize", 15);
    }

    var idle = configuration.GetValue<int?>("DatabasePooling:ConnectionIdleLifetimeSeconds");
    if (idle is > 0)
        csb.ConnectionIdleLifetime = idle.Value;

    var timeout = configuration.GetValue<int?>("DatabasePooling:TimeoutSeconds");
    if (timeout is > 0)
        csb.Timeout = timeout.Value;

    var cmdTimeout = configuration.GetValue<int?>("DatabasePooling:CommandTimeoutSeconds");
    if (cmdTimeout is > 0)
        csb.CommandTimeout = cmdTimeout.Value;

    return csb.ConnectionString;
}

var supabaseConnectionString = BuildSupabaseConnectionString(builder.Configuration);

builder.Services.AddDbContext<BoneVisQADbContext>(options =>
    options.UseNpgsql(supabaseConnectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.SetPostgresVersion(15, 0);
            npgsqlOptions.UseVector();
        }))
    .AddScoped<BoneVisQADbContext>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "THIS_IS_DEMO_SECRET_KEY_CHANGE_ME";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
if (keyBytes.Length < 32)
    keyBytes = SHA256.HashData(keyBytes);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // SignalR WebSocket fallback: access_token in query string.
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? "";
        options.CallbackPath = "/signin-google";
    });

builder.Services.AddHttpClient(PdfProcessingService.HttpClientName, client =>
{
    // Background ingestion downloads PDF from storage (bucket max 50 MB); allow slow links.
    client.Timeout = TimeSpan.FromMinutes(60);
});
builder.Services.AddHttpClient(EmbeddingService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
}).AddPolicyHandler(AiHttpRetryPolicy.CreatePolicy());

builder.Services.AddHttpClient<IImageProcessingService, ImageProcessingService>();
builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection(GeminiSettings.SectionName));
builder.Services.AddHttpClient(GeminiService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
}).AddPolicyHandler(AiHttpRetryPolicy.CreatePolicy());
builder.Services.AddHttpClient(QuizGeminiService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
}).AddPolicyHandler(AiHttpRetryPolicy.CreatePolicy());
builder.Services.AddScoped<IGeminiService, GeminiService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IPdfProcessingService, PdfProcessingService>();
builder.Services.AddScoped<IVisualQaAiService, VisualQaAiService>();
builder.Services.AddScoped<IQuizGeminiService, QuizGeminiService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IRagExpertAnswerIndexingSignal, NoOpRagExpertAnswerIndexingSignal>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ILecturerService, LecturerService>();
builder.Services.AddScoped<ILecturerAssignmentService, LecturerAssignmentService>();
builder.Services.AddScoped<ILecturerDashboardService, LecturerDashboardService>();
builder.Services.AddScoped<ILecturerTriageService, LecturerTriageService>();
builder.Services.AddScoped<ILecturerProfileService, LecturerProfileService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IStudentProfileService, StudentProfileService>();
builder.Services.AddScoped<IStudentLearningService, StudentLearningService>();
builder.Services.AddScoped<IAIQuizService, AIQuizService>();
builder.Services.AddScoped<IClassManagementService, ClassManagementService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<IDocumentService>(sp => sp.GetRequiredService<DocumentService>());
builder.Services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>(client =>
{
    // Streaming uploads to Supabase may run for a long time on large PDFs / slow links.
    client.Timeout = TimeSpan.FromMinutes(60);
});

builder.Services.AddScoped<IMedicalCaseService, MedicalCaseService>();
builder.Services.AddScoped<IExpertReviewService, ExpertReviewService>();
builder.Services.AddScoped<IExpertDashboardService, ExpertDashboardService>();
builder.Services.AddScoped<IExpertProfileService, ExpertProfileService>();
builder.Services.AddScoped<IQuizsService, QuizsService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IAdminProfileService, AdminProfileService>();
builder.Services.AddScoped<ITagCaseService, TagCaseService>();
builder.Services.AddScoped<IDocumentQualityService, DocumentQualityService>();
builder.Services.AddScoped<IDocumentManagementService, DocumentManagementService>();
builder.Services.AddScoped<ISystemMonitoringService, SystemMonitoringService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("AllowAll");
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin-allow-popups");
    context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
    await next();
});
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseSwagger();
app.UseSwaggerUI();
//
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// Đảm bảo thư mục uploads tồn tại trước khi sử dụng PhysicalFileProvider
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    // FileProvider = new PhysicalFileProvider(
    //     Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.Run();
