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

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. SERVICIOS BASE Y CONFIGURACIÓN DE FORMULARIOS
// ============================================================
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddControllers();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB para imágenes
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder("Identity", "/");
});

// ============================================================
// 2. INYECCIÓN DE DEPENDENCIAS (SERVICIOS PERSONALIZADOS)
// ============================================================
builder.Services.AddScoped<PasswordService>();

// ✅ REGISTRO TRIPLE DE EMAIL: Soluciona el error 500 y errores de compilación
// Esto vincula la clase EmailService con las dos interfaces que utiliza tu app
builder.Services.AddScoped<IEmailSender, EmailService>();   // Requerido por ASP.NET Identity
builder.Services.AddScoped<IEmailService, EmailService>();  // Requerido por CheckoutController
builder.Services.AddScoped<EmailService>();                 // Acceso directo a la clase

builder.Services.AddScoped<EmailTemplateService>();
builder.Services.AddScoped<MercadoPagoService>();

// ============================================================
// 3. CONFIGURACIÓN DE BASE DE DATOS (POSTGRESQL)
// ============================================================
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

// ============================================================
// 4. IDENTITY, SESIONES Y COOKIES
// ============================================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ============================================================
// 5. CONSTRUCCIÓN Y MIDDLEWARES
// ============================================================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Soporte para formatos de imagen modernos
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".avif"] = "image/avif";
provider.Mappings[".webp"] = "image/webp";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Redirecciones manuales para rutas de Identity
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();
    if (path == "/identity/account/login") { context.Response.Redirect("/Account/Login"); return; }
    if (path == "/identity/account/register") { context.Response.Redirect("/Account/Register"); return; }
    await next();
});

// ============================================================
// 6. MANTENIMIENTO AUTOMÁTICO (MIGRACIONES Y SEEDERS)
// ============================================================
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

// ============================================================
// 7. CONFIGURACIÓN DE RUTAS Y PUERTO (RENDER)
// ============================================================
app.MapControllers();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Products}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Configuración de puerto dinámica para el despliegue en la nube
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();