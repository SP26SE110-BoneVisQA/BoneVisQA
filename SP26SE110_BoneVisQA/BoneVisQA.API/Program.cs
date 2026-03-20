using System.Security.Cryptography;
using System.Text;
using BoneVisQA.Repositories.DBContext;
using BoneVisQA.Repositories.Interfaces;
using BoneVisQA.Repositories.Services;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Admin;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Services;
using BoneVisQA.Services.Services.Admin;
using BoneVisQA.Services.Services.Expert;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Large PDF uploads (100 MB) — avoids multipart / Kestrel default limits during Swagger tests.
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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
});

builder.Services.AddDbContext<BoneVisQADbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SupabaseDb"),
        npgsqlOptions =>
        {
            npgsqlOptions.SetPostgresVersion(15, 0);
            npgsqlOptions.UseVector();
        }));

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
    });

builder.Services.AddHttpClient(PdfProcessingService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient(EmbeddingService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient<IImageProcessingService, ImageProcessingService>();
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IPdfProcessingService, PdfProcessingService>();
builder.Services.AddScoped<IVisualQaAiService, VisualQaAiService>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILecturerService, LecturerService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>();

builder.Services.AddScoped<IMedicalCaseService, MedicalCaseService>();
builder.Services.AddScoped<BoneVisQA.Services.Interfaces.IQuizService, BoneVisQA.Services.Services.QuizService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<ITagCaseService, TagCaseService>();
builder.Services.AddScoped<IDocumentQualityService, DocumentQualityService>();
builder.Services.AddScoped<IDocumentManagementService, DocumentManagementService>();
builder.Services.AddScoped<ISystemMonitoringService, SystemMonitoringService>();

var app = builder.Build();

// Global exception logging (catches unhandled exceptions in the pipeline before silent process exit).
app.Use(async (context, next) =>
{
    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[FATAL CRASH] {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        throw; // Re-throw so default host behavior still applies
    }
});

app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
