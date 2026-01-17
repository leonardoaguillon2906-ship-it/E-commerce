using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet; 
using CloudinaryDotNet.Actions; 

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly Cloudinary _cloudinary; 

        public ProductsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;

            // ✅ CONFIGURACIÓN SEGURA CON VARIABLES DE ENTORNO
            var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
            var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
            var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

            if (string.IsNullOrEmpty(cloudName))
            {
                // Solo para pruebas locales
                var account = new Account("TU_CLOUD_NAME", "TU_API_KEY", "TU_API_SECRET");
                _cloudinary = new Cloudinary(account);
            }
            else
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account);
            }
        }

        public async Task<IActionResult> Index(string? search, int? categoryId, int page = 1)
        {
            int pageSize = 10;
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            var query = _context.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId);

            int totalItems = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.CurrentPage = page;

            return View("~/Areas/Admin/Views/Admin/Index.cshtml", products);
        }

        public IActionResult Create()
        {
            LoadCategories();
            return View("~/Areas/Admin/Views/Admin/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (!await _context.Categories.AnyAsync(c => c.Id == product.CategoryId))
                ModelState.AddModelError("CategoryId", "La categoría seleccionada no existe.");

            if (!ModelState.IsValid)
            {
                LoadCategories(product.CategoryId);
                return View("~/Areas/Admin/Views/Admin/Create.cshtml", product);
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                product.ImageUrl = await SaveImage(imageFile);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            LoadCategories(product.CategoryId);
            return View("~/Areas/Admin/Views/Admin/Edit.cshtml", product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                LoadCategories(product.CategoryId);
                return View("~/Areas/Admin/Views/Admin/Edit.cshtml", product);
            }

            var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            if (existingProduct == null) return NotFound();

            if (imageFile != null && imageFile.Length > 0)
            {
                DeleteImage(existingProduct.ImageUrl);
                product.ImageUrl = await SaveImage(imageFile);
            }
            else
            {
                product.ImageUrl = existingProduct.ImageUrl;
            }

            _context.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                DeleteImage(product.ImageUrl);
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBanner(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            if (!product.IsHomeBanner)
            {
                var activeBanners = await _context.Products.Where(p => p.IsHomeBanner).ToListAsync();
                foreach (var banner in activeBanners) banner.IsHomeBanner = false;
                product.IsHomeBanner = true;
            }
            else product.IsHomeBanner = false;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private void LoadCategories(int? selectedId = null)
        {
            var categories = _context.Categories.ToList();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedId);
        }

        // =======================
        // MÉTODOS AUXILIARES CORREGIDOS (AVIF COMPATIBLE)
        // =======================

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            var uploadResult = new ImageUploadResult();
            using (var stream = imageFile.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    Folder = "ecommerce_productos", 
                    // FetchFormat = auto permite que Cloudinary optimice AVIF según el navegador
                    Transformation = new Transformation().Width(800).Height(800).Crop("limit").FetchFormat("auto").Quality("auto")
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }
            return uploadResult.SecureUrl.ToString(); 
        }

        private void DeleteImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || !imageUrl.Contains("res.cloudinary.com"))
                return;

            try
            {
                // Lógica mejorada para extraer PublicID ignorando extensiones complejas como .avif
                var uri = new Uri(imageUrl);
                var lastSegment = uri.Segments.Last();
                
                // Quitamos la extensión (pueden ser .jpg, .png, .avif, etc)
                var publicIdWithoutExtension = Path.GetFileNameWithoutExtension(lastSegment);
                
                // Construimos el PublicId exacto incluyendo la carpeta
                var publicId = "ecommerce_productos/" + publicIdWithoutExtension;
                
                _cloudinary.Destroy(new DeletionParams(publicId) { ResourceType = ResourceType.Image });
            }
            catch { /* Silencioso para no interrumpir el flujo del usuario */ }
        }
    }
}