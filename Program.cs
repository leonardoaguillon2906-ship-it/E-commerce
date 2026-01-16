using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using EcommerceApp.Services.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// =======================
// SERVICIOS BASE
// =======================
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddControllers();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder("Identity", "/");
});

// =======================
// SERVICIOS PERSONALIZADOS
// =======================
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<EmailTemplateService>();
builder.Services.AddScoped<MercadoPagoService>();

// =======================
// CONFIGURACIÓN DE POSTGRESQL
// =======================
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

// =======================
// IDENTITY, SESSION & COOKIES
// =======================
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

// =======================
// CONSTRUCCIÓN DE LA APP
// =======================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Redirecciones Identity
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();
    if (path == "/identity/account/login") { context.Response.Redirect("/Account/Login"); return; }
    if (path == "/identity/account/register") { context.Response.Redirect("/Account/Register"); return; }
    await next();
});

// ============================================================
// BLOQUE DE MANTENIMIENTO AUTOMÁTICO (SEGURO PARA DATOS)
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // IMPORTANTE: La línea de DROP SCHEMA ha sido eliminada para proteger tus datos.
        // Las tablas ya existen, así que Migrate solo aplicará cambios nuevos en el futuro.
        
        logger.LogInformation("Verificando actualizaciones de base de datos...");
        await context.Database.MigrateAsync();

        // Los seeders están programados internamente para no duplicar datos si ya existen
        await RoleSeeder.SeedRolesAsync(services);
        await SeedAdminUser.CreateAsync(services);
        
        logger.LogInformation("Sistema de base de datos listo.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error durante la inicialización automática.");
    }
}

// =======================
// RUTAS Y LANZAMIENTO
// =======================
app.MapControllers();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Products}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

app.Run();