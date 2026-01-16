using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Models;

namespace EcommerceApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Página de inicio con productos + banner
        public async Task<IActionResult> Index()
        {
            // ============================
            // BANNER DESDE PRODUCTOS (NO SE TOCA)
            // ============================
            var banner = await _context.Products
                .Where(p => p.IsHomeBanner)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            // ============================
            // NUEVO: BANNER DESDE HomeBanner
            // ============================
            var homeBanner = await _context.HomeBanners
                .Where(b => b.IsActive)
                .OrderBy(b => b.Order)
                .ThenByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            // ============================
            // PRODUCTOS DESTACADOS (NO SE TOCA)
            // ============================
            var topProducts = await _context.Products
                .Where(p => !p.IsHomeBanner)
                .Take(4)
                .ToListAsync();

            // ============================
            // VIEWBAG (EXTENDIDO)
            // ============================

            // Banner antiguo (Product)
            ViewBag.Banner = banner;

            // Banner nuevo (HomeBanner)
            ViewBag.HomeBanner = homeBanner;

            return View(topProducts);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
