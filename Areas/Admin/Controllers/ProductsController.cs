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
        private readonly Cloudinary? _cloudinary; 

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

            // Diagnóstico simple para Logs de Render
            if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                Console.WriteLine("⚠️ Alerta: Cloudinary no se inicializó. Revisa las Variables de Entorno en Render.");
            }
            else
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account);
                Console.WriteLine("✅ Cloudinary conectado exitosamente.");
            }
        }

        public async Task<IActionResult> Index(string? search, int? categoryId, int page = 1)
        {
            int pageSize = 10;
            ViewBag.Categories = await _context.Categories.ToListAsync();
            
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
            var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            if (existingProduct == null) return NotFound();

            if (imageFile != null && imageFile.Length > 0)
            {
                // Solo intentamos borrar de Cloudinary si la URL es válida
                if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                {
                    DeleteImage(existingProduct.ImageUrl);
                }
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
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    DeleteImage(product.ImageUrl);
                }
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
            ViewBag.Categories = new SelectList(_context.Categories.ToList(), "Id", "Name", selectedId);
        }

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            if (_cloudinary == null) return "";

            try 
            {
                using var stream = imageFile.OpenReadStream();
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    Folder = "ecommerce_productos", 
                    Transformation = new Transformation().Width(800).Height(800).Crop("limit").Quality("auto").FetchFormat("auto")
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                return uploadResult.SecureUrl?.ToString() ?? ""; 
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al subir a Cloudinary: " + ex.Message);
                return "";
            }
        }

        private void DeleteImage(string? imageUrl)
        {
            if (_cloudinary == null || string.IsNullOrEmpty(imageUrl) || !imageUrl.Contains("res.cloudinary.com"))
                return;

            try
            {
                var uri = new Uri(imageUrl);
                var fileName = Path.GetFileNameWithoutExtension(uri.Segments.Last());
                var publicId = "ecommerce_productos/" + fileName;
                _cloudinary.Destroy(new DeletionParams(publicId));
            }
            catch (Exception ex) 
            { 
                Console.WriteLine("Error al borrar de Cloudinary: " + ex.Message); 
            }
        }
    }
}