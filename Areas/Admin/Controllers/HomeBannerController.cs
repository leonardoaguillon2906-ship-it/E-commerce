using Microsoft.AspNetCore.Mvc;
using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // 🔒 Solo administradores
    public class HomeBannersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public HomeBannersController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // =======================
        // LISTADO
        // =======================
        public IActionResult Index()
        {
            var banners = _context.HomeBanners
                .OrderBy(b => b.Order)
                .ToList();

            return View(banners);
        }

        // =======================
        // CREATE GET
        // =======================
        public IActionResult Create()
        {
            return View();
        }

        // =======================
        // CREATE POST
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormFile file, bool isActive)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Debe seleccionar una imagen o video.");
                return View();
            }

            var ext = Path.GetExtension(file.FileName).ToLower();
            bool isVideo = ext == ".mp4";

            if (!isVideo && ext != ".jpg" && ext != ".png" && ext != ".jpeg")
            {
                ModelState.AddModelError("", "Formato no permitido.");
                return View();
            }

            // 🔴 SOLO UN BANNER ACTIVO
            if (isActive)
            {
                var actives = _context.HomeBanners.Where(b => b.IsActive).ToList();
                foreach (var b in actives)
                {
                    b.IsActive = false;
                }
            }

            Directory.CreateDirectory(Path.Combine(_env.WebRootPath, "banners"));

            var fileName = Guid.NewGuid() + ext;
            var path = Path.Combine(_env.WebRootPath, "banners", fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            var banner = new HomeBanner
            {
                MediaUrl = "/banners/" + fileName,
                IsVideo = isVideo,
                IsActive = isActive,
                CreatedAt = DateTime.Now
            };

            _context.HomeBanners.Add(banner);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
