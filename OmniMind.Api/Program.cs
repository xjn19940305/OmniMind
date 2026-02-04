using OmniMind.Infrastructure;
using OmniMind.Application;
using OmniMind.Api.Swaggers;
using HealthChecks.UI.Client;
using IGeekFan.AspNetCore.Knife4jUI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using OmniMind.Abstractions.Tenant;
using OmniMind.Entities;
using OmniMind.Infrastructure.Scheduling;
using OmniMind.Persistence.PostgreSql;
using OmniMind.Realtime.SignalR;
using OmniMind.Works;
using Quartz;
using System.Reflection;
using System.Text;
using OmniMind.Storage.Minio;
using OmniMind.Vector.Qdrant;
using OmniMind.Messaging.RabbitMQ;
using OmniMind.Ingestion;
using RabbitMQ.Client;
var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .AddCommandLine(args)
    .AddEnvironmentVariables();

// Load CONFIG environment variable if exists
if (!string.IsNullOrWhiteSpace(builder.Configuration["CONFIG"]))
{
    builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(builder.Configuration["CONFIG"]!)));
}

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var postgreSqlConnectionString = builder.Configuration["DB_CONNECTION"];
var redisConnectionString = builder.Configuration["StackExchangeRedis:Connection"];

// Identity
builder.Services.AddIdentity<User, Role>(options =>
{
    options.ClaimsIdentity.UserIdClaimType = "sub";
    options.ClaimsIdentity.RoleClaimType = "role";
    options.ClaimsIdentity.SecurityStampClaimType = "security_stamp";
    options.ClaimsIdentity.UserNameClaimType = "username";
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
})
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<MyUserClaimsPrincipalFactory>()
.AddEntityFrameworkStores<OmniMindDbContext>();

// Cookie Policy
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.HttpOnly = HttpOnlyPolicy.None;
    options.Secure = CookieSecurePolicy.Always;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
});

// Authentication - JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        builder.Configuration.Bind("JwtBearerOptions", options);

        // Set signing key
        var secretKey = builder.Configuration["JwtBearerOptions:SecretKey"];
        if (!string.IsNullOrEmpty(secretKey))
        {
            options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));
        }

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Authorization
builder.Services.AddAuthorization(config =>
{
    config.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    config.AddPolicy("Demo", policy =>
    {
        policy.RequireClaim("permission", "demo.demo1");
        policy.RequireClaim("permission", "demo.demo2");
    });
    config.AddPolicy("Demo1", policy => policy.RequireClaim("permission", "demo.demo1"));
    config.AddPolicy("Demo2", policy => policy.RequireClaim("permission", "demo.demo2"));
    config.AddPolicy("RoleQuery", policy => policy.RequireClaim("permission", "role.query"));
    config.AddPolicy("RoleCreate", policy => policy.RequireClaim("permission", "role.create"));
});

// Controllers
builder.Services
    .AddControllers(o => o.Filters.Add(typeof(ExceptionFilter)))
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat;
        options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
    });

// Quartz
builder.Services.AddQuartzHostedService(c =>
{
    c.WaitForJobsToComplete = true;
});

builder.Services.AddQuartz(options =>
{
    options.UseDefaultThreadPool(c =>
    {
        c.MaxConcurrency = Environment.ProcessorCount * 2;
    });

    // TestJob - 示例任务
    options.AddJob<TestJob>(config =>
    {
        config.WithIdentity(nameof(TestJob))
        .StoreDurably();
    })
    .AddTrigger(opt =>
    {
        opt.WithIdentity(nameof(TestJob))
        .ForJob(nameof(TestJob))
        .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever())
        .StartNow();
    });

    // DocumentProcessingJob - 文档处理任务
    // 模式1: 定时批量处理模式（推荐用于生产环境）
    // 每1分钟执行一次，每次处理10个待上传的文档
    //options.AddJob<DocumentProcessingJob>(config =>
    //{
    //    config.WithIdentity("DocumentProcessingJob")
    //    .StoreDurably()
    //    .UsingJobData("batchSize", 50)           // 每批处理文档数量
    //    .UsingJobData("timeoutSeconds", 60);     // 超时时间（秒）
    //})
    //.AddTrigger(opt =>
    //{
    //    opt.WithIdentity("DocumentProcessingJobTrigger")
    //    .ForJob("DocumentProcessingJob")
    //    // 使用Cron表达式：每分钟执行一次
    //    .WithCronSchedule("0 * * * * ?")
    //    .StartNow();
    //});

    options.UsePersistentStore(po =>
    {
        po.UseClustering();
        po.UseNewtonsoftJsonSerializer();
        po.SetProperty("quartz.jobStore.driverDelegateType", "Quartz.Impl.AdoJobStore.PostgreSQLDelegate, Quartz");
        po.SetProperty("quartz.jobStore.dataSource", "myDS");
        po.SetProperty("quartz.dataSource.myDS.connectionString", postgreSqlConnectionString!);
        po.SetProperty("quartz.dataSource.myDS.provider", "Npgsql");
    });
});

#region Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("management", new OpenApiInfo
    {
        Version = "v1",
        Title = "OmniMind Management API",
        Description = "Management API",
        TermsOfService = new Uri("https://example.com/terms")
    });

    c.SwaggerDoc("user", new OpenApiInfo
    {
        Version = "v1",
        Title = "OmniMind User API",
        Description = "User API",
        TermsOfService = new Uri("https://example.com/terms")
    });

    c.SchemaFilter<EnumSchemaFilter>();

    // Add dynamic API groups
    typeof(ApiGroupNames).GetFields().Skip(1).ToList().ForEach(f =>
    {
        var info = f.GetCustomAttributes(typeof(GroupInfoAttribute), false).FirstOrDefault() as GroupInfoAttribute;
        if (info != null)
        {
            c.SwaggerDoc(f.Name, new OpenApiInfo
            {
                Title = info.Title,
                Version = "v1",
                Description = info.Title
            });
        }
    });

    // Filter APIs by group
    c.DocInclusionPredicate((docName, apiDescription) =>
    {
        var actionList = apiDescription.ActionDescriptor.EndpointMetadata.Where(x => x is ApiGroupAttribute);
        if (actionList.Any())
        {
            var actionFilter = actionList.FirstOrDefault() as ApiGroupAttribute;
            if (actionFilter != null && !actionFilter.Igrone)
            {
                return actionFilter.GroupName.Any(x => x.ToString().Equals(docName, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }
        return false;
    });

    // XML Comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, true);

    var contractsXmlFile = $"{Assembly.Load("OmniMind.Contracts").GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, contractsXmlFile), true);

    var entitiesXmlFile = $"{Assembly.Load("OmniMind.Entities").GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, entitiesXmlFile), true);
});
#endregion

#region HSTS, CORS, ForwardedHeaders, DataProtection
builder.Services.AddHsts(o =>
{
    o.IncludeSubDomains = true;
});

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
    {
        p.SetIsOriginAllowed(_ => true);
        p.AllowAnyMethod();
        p.AllowAnyHeader();
        p.AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(
        StackExchange.Redis.ConnectionMultiplexer.Connect(builder.Configuration["DataProtection:Redis:Connection"]!),
        builder.Configuration["DataProtection:Redis:Key"]);
#endregion

#region  Redis (single connection multiplexer)

builder.Services.AddSingleton(sp =>
{
    return StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString!);
});
#endregion
#region  Distributed Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = builder.Configuration["StackExchangeRedis:Prefix"];
});
#endregion
#region postgres
if (string.IsNullOrEmpty(postgreSqlConnectionString))
{
    throw new Exception("DB_CONNECTION 环境变量或配置未设置。请在 appsettings.json 或环境变量中配置 PostgreSQL 连接字符串。格式: Host=localhost;Port=5432;Database=omnimind;Username=postgres;Password=your_password");
}

builder.Services.AddDbContext<OmniMindDbContext>(setup =>
{
    setup.UseNpgsql(postgreSqlConnectionString, options =>
    {
        options.MigrationsAssembly(Assembly.Load("OmniMind.Persistence.PostgreSql").FullName);
    });
});
#endregion
// 注册minio对象存储服务
builder.Services.AddMinioService(builder.Configuration);
// 注册qdrant向量数据库服务
builder.Services.AddQdrantService(builder.Configuration);
// 注册RabbitMQ消息服务
builder.Services.AddRabbitMQ(builder.Configuration);
// 注册Ingestion服务（文件解析和文本切片）
builder.Services.AddIngestion();
// 注册阿里云向量化服务
builder.Services.AddAlibabaCloudEmbedding(builder.Configuration);
// 注册阿里云百练 ChatClient 服务
builder.Services.AddAlibabaCloudChatClient(builder.Configuration);
// 注册 SignalR 实时通信服务（支持 Redis 背板）
builder.Services.AddSignalRServices(builder.Configuration);
// 注册后台服务（文档处理消费者）
builder.Services.AddHostedService<DocumentProcessingWorker>();


// HttpClient & HealthChecks
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Init data if needed
if (builder.Configuration.GetValue<bool>("Init"))
{
    using var scope = app.Services.CreateScope();
    await new OmniMind.Application.InitJob().Init(scope);
}

// Middleware pipeline
if (!app.Environment.IsProduction())
{
    app.UseSwagger();

    app.UseKnife4UI(c =>
    {
        c.RoutePrefix = string.Empty;
        c.SwaggerEndpoint("/swagger/user/swagger.json", "User API");
        c.SwaggerEndpoint("/swagger/management/swagger.json", "Management API");
    });
}

app.UseCookiePolicy();
app.UseStaticFiles();
app.UseForwardedHeaders();
app.UseHsts();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/hc", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/liveness", new HealthCheckOptions
{
    Predicate = r => r.Name.Contains("self")
});

// SignalR Hub 端点映射
app.MapHub<IngestionHub>("/hubs/ingestion");

app.MapControllers();

if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();
