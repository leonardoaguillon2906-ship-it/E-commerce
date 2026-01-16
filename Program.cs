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
// SERVICIOS
// =======================

// MVC + JSON (Mercado Pago Webhooks)
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// ✅ AGREGADO: Controllers API (para Webhooks)
builder.Services.AddControllers();

// Razor Pages (Identity) — protegidas para que NO use su propio Login/Register
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder("Identity", "/");
});

// =======================
// SERVICIOS DE PASSWORD
// =======================
builder.Services.AddScoped<PasswordService>();



// =======================
// SERVICIOS DE EMAIL
// =======================
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<EmailTemplateService>();



// =======================
// SERVICIOS DE APLICACIÓN
// =======================

// Mercado Pago
builder.Services.AddScoped<MercadoPagoService>();

// =======================
// BASE DE DATOS
// =======================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection"))
);

// =======================
// SESSION
// =======================
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =======================
// IDENTITY + ROLES
// =======================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// =======================
// COOKIES
// =======================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    // 🔁 SIEMPRE LOGIN PÚBLICO
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.Redirect("/Account/Login");
        return Task.CompletedTask;
    };

    // 🔁 ACCESS DENIED PÚBLICO
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.Redirect("/Account/AccessDenied");
        return Task.CompletedTask;
    };

    // ✅ REDIRECCIÓN SEGÚN ROL
    options.Events.OnSignedIn = context =>
    {
        var user = context.Principal;
        if (user != null)
        {
            if (user.IsInRole("Admin"))
            {
                context.Response.Redirect("/Admin/Products");
            }
            else if (user.IsInRole("Cliente"))
            {
                context.Response.Redirect("/Cliente/Products");
            }
        }
        return Task.CompletedTask;
    };
});

var app = builder.Build();

// =======================
// MIDDLEWARE
// =======================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// ✅ REDIRECCIÓN FORZADA DE IDENTITY UI → LOGIN / REGISTER PÚBLICOS
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

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// =======================
// SEEDERS
// =======================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await RoleSeeder.SeedRolesAsync(services);
        await SeedAdminUser.CreateAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error ejecutando seeders");
    }
}

// =======================
// RUTAS
// =======================

// ✅ AGREGADO: RUTAS API (Webhook Mercado Pago)
app.MapControllers();

// Áreas (Admin / Cliente)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Products}/{action=Index}/{id?}"
);

// MVC público
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// Razor Pages (Identity backend)
app.MapRazorPages();

app.Run();
