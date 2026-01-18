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

// ✅ VARIABLES DE ENTORNO
builder.Configuration.AddEnvironmentVariables();

// ✅ AJUSTE CRÍTICO PARA RENDER
if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT")}");
}

// =======================
// SERVICIOS BASE
// =======================
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddControllers();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder("Identity", "/");
});

// =======================
// SERVICIOS PERSONALIZADOS
// =======================
builder.Services.AddScoped<PasswordService>();

builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();

builder.Services.AddScoped<MercadoPagoService>();

// =======================
// POSTGRESQL
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
// IDENTITY & SESSION
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
// APP
// =======================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

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

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();
    if (path == "/identity/account/login")
    {
        context.Response.Redirect("/Account/Login");
        return;
    }
    if (path == "/identity/account/register")
    {
        context.Response.Redirect("/Account/Register");
        return;
    }
    await next();
});

// =======================
// MIGRATIONS SOLO DEV
// =======================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

// =======================
// RUTAS
// =======================
app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Products}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
