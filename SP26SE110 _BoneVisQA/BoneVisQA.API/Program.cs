using BoneVisQA.Repositories.DBContext; // DbContext của bạn
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký DbContext với Supabase (EF Core)
builder.Services.AddDbContext<BoneVisQADbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SupabaseConnection")));

// Lấy JwtSecret từ User Secrets (hoặc appsettings nếu deploy)
var jwtSecret = builder.Configuration["Supabase:JwtSecret"];
var projectRef = builder.Configuration["Supabase:ProjectRef"] ?? "nvdafbzgzfqsavvinvia"; // fallback nếu chưa config

// Thêm Authentication JwtBearer để verify token từ Supabase Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://{projectRef}.supabase.co/auth/v1",
            ValidateAudience = false, // Supabase không dùng audience mặc định
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero // Không cho phép lệch giờ
        };

        // Optional: Log hoặc thêm claims nếu cần
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("Authentication failed: " + context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Token hợp lệ → có thể truy cập claims như sub (user id)
                var userId = context.Principal?.FindFirst("sub")?.Value;
                Console.WriteLine("User authenticated: " + userId);
                return Task.CompletedTask;
            }
        };
    });

// Thêm Authorization để dùng [Authorize]
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Thêm Authentication & Authorization middleware (quan trọng: phải trước MapControllers)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();