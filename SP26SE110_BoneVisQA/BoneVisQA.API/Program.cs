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

// Trang đặt lại mật khẩu (Backend phục vụ, không cần Frontend)
// Token lấy từ URL: /reset-password?token=XXX (KHÔNG phải JWT từ login!)
app.MapGet("/reset-password", (HttpContext ctx) =>
{
    var token = ctx.Request.Query["token"].ToString();
    if (string.IsNullOrEmpty(token))
    {
        return Results.Content(@"<!DOCTYPE html><html><body><h1>Error</h1><p class=""error"">Token missing. Please use the link from your email.</p><p><a href=""/swagger"">Go to homepage</a></p></body></html>", "text/html; charset=utf-8");
    }
    var tokenEscaped = token.Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    var html = @"<!DOCTYPE html>
<html><head><meta charset=""utf-8""><title>Đặt lại mật khẩu - BoneVisQA</title>
<style>body{font-family:Arial;max-width:420px;margin:50px auto;padding:20px}
input{width:100%;padding:10px;margin:8px 0;box-sizing:border-box}
.pw-wrap{position:relative;margin:8px 0}
.pw-wrap input{padding-right:50px}
.pw-wrap span{position:absolute;right:8px;top:50%;transform:translateY(-50%);cursor:pointer;font-size:13px;color:#888;user-select:none}
button{background:#3498db;color:white;padding:12px;border:none;width:100%;cursor:pointer;margin-top:10px}
.error{color:red}.success{color:green}
.token-hint{font-size:11px;color:#888;margin:5px 0}
</style></head><body>
<h1>Đặt lại mật khẩu</h1>
<p class=""token-hint"">Token: lấy từ link trong email (phần sau ?token=)</p>
<div id=""msg""></div>
<form id=""form"">
<input type=""hidden"" name=""token"" value=""" + tokenEscaped + @""">
<div class=""pw-wrap"">
<input type=""password"" id=""p1"" name=""newPassword"" placeholder=""Mật khẩu mới (ít nhất 6 ký tự)"" required minlength=""6"">
<span onclick=""tgl(1)"">Hiện</span>
</div>
<div class=""pw-wrap"">
<input type=""password"" id=""p2"" name=""confirmPassword"" placeholder=""Xác nhận mật khẩu"" required>
<span onclick=""tgl(2)"">Hiện</span>
</div>
<button type=""submit"">Đặt lại mật khẩu</button>
</form>
<script>
function tgl(n){var e=document.getElementById('p'+n),b=e.nextElementSibling;e.type=e.type==='password'?'text':'password';b.textContent=b.textContent==='Hiện'?'Ẩn':'Hiện';}
document.getElementById('form').onsubmit=async function(ev){ev.preventDefault();
var p1=ev.target.newPassword.value,p2=ev.target.confirmPassword.value;
if(p1!==p2){document.getElementById('msg').innerHTML='<p class=error>Mật khẩu không khớp</p>';return;}
document.getElementById('msg').innerHTML='<p>Đang xử lý...</p>';
var res=await fetch('/api/auths/reset-password',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:ev.target.token.value,newPassword:p1})});
var data=await res.json();
document.getElementById('msg').innerHTML=data.success?'<p class=success>'+data.message+'</p><p><a href=/swagger>Đăng nhập tại đây</a></p>':'<p class=error>'+data.message+'</p><p class=token-hint>Kiểm tra: token phải từ link email (?token=xxx), KHÔNG dùng JWT từ login.</p>';
};
</script></body></html>";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.Run();
