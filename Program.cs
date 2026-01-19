using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using EcommerceApp.Services.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// 1. VARIABLES DE ENTORNO
// =====================================================
builder.Configuration.AddEnvironmentVariables();

// =====================================================
// 2. SERVICIOS BASE Y CONTROLADORES
// =====================================================
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => 
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddControllers();

// Configuración para carga de imágenes pesadas
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder("Identity", "/");
});

// =====================================================
// 3. PROTECCIÓN DE DATOS (CRÍTICO PARA RENDER)
// =====================================================
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")));

// =====================================================
// 4. INYECCIÓN DE DEPENDENCIAS (SERVICIOS)
// =====================================================
builder.Services.AddScoped<PasswordService>();

// Registro de Email: Clase concreta e Interfaces
builder.Services.AddScoped<EmailService>(); 
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ✅ CORRECCIÓN: Registro de la Interfaz del Template Service
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<EmailTemplateService>(); // Clase concreta para Webhook

builder.Services.AddScoped<MercadoPagoService>();

// =====================================================
// 5. CONFIGURACIÓN DE BASE DE DATOS (POSTGRESQL)
// =====================================================
var connectionString = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetConnectionString("DefaultConnection") 
    : $"Host={Environment.GetEnvironmentVariable("DATABASE_HOST")};" +
      $"Port={Environment.GetEnvironmentVariable("DATABASE_PORT")};" +
      $"Database={Environment.GetEnvironmentVariable("DATABASE_NAME")};" +
      $"Username={Environment.GetEnvironmentVariable("DATABASE_USER")};" +
      $"Password={Environment.GetEnvironmentVariable("DATABASE_PASSWORD")};" +
      "SSL Mode=Require;Trust Server Certificate=true";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// =====================================================
// 6. IDENTITY, SESSION & COOKIES
// =====================================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Recomendado para HTTPS (Render)
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
    
    options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
    options.ClaimsIdentity.UserNameClaimType = ClaimTypes.Name;
    options.ClaimsIdentity.RoleClaimType = ClaimTypes.Role;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "EcommerceApp.Identity";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

// =====================================================
// 7. CONSTRUCCIÓN Y MIDDLEWARES
// =====================================================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Tipos de archivo para imágenes modernas
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".avif"] = "image/avif";
provider.Mappings[".webp"] = "image/webp";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseRouting();

// Orden crítico de seguridad
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Redirecciones de rutas Identity
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();
    if (path == "/identity/account/login") { context.Response.Redirect("/Account/Login"); return; }
    if (path == "/identity/account/register") { context.Response.Redirect("/Account/Register"); return; }
    await next();
});

// =====================================================
// 8. MANTENIMIENTO AUTOMÁTICO DE BASE DE DATOS
// =====================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Verificando actualizaciones de base de datos...");
        await context.Database.MigrateAsync();

        await RoleSeeder.SeedRolesAsync(services);
        await SeedAdminUser.CreateAsync(services);
        
        logger.LogInformation("Sistema de base de datos listo.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error durante la inicialización automática.");
    }
}

// =====================================================
// 9. RUTAS DE CONTROLADORES Y LANZAMIENTO
// =====================================================
app.MapControllers();

app.MapControllerRoute(
    name: "areas", 
    pattern: "{area:exists}/{controller=Products}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default", 
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();