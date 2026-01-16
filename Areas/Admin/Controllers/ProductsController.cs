using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // =======================
        // LISTADO (CON BÚSQUEDA, FILTRO Y PAGINACIÓN)
        // =======================
        public async Task<IActionResult> Index(string? search, int? categoryId, int page = 1)
        {
            int pageSize = 10;

            // Para filtros en la vista
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

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

        // =======================
        // CREATE GET
        // =======================
        public IActionResult Create()
        {
            LoadCategories();
            return View("~/Areas/Admin/Views/Admin/Create.cshtml");
        }

        // =======================
        // CREATE POST
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (!await _context.Categories.AnyAsync(c => c.Id == product.CategoryId))
            {
                ModelState.AddModelError("CategoryId", "La categoría seleccionada no existe.");
            }

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

        // =======================
        // EDIT GET
        // =======================
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound();

            LoadCategories(product.CategoryId);
            return View("~/Areas/Admin/Views/Admin/Edit.cshtml", product);
        }

        // =======================
        // EDIT POST
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            if (!await _context.Categories.AnyAsync(c => c.Id == product.CategoryId))
            {
                ModelState.AddModelError("CategoryId", "La categoría seleccionada no existe.");
            }

            if (!ModelState.IsValid)
            {
                LoadCategories(product.CategoryId);
                return View("~/Areas/Admin/Views/Admin/Edit.cshtml", product);
            }

            var existingProduct = await _context.Products.FindAsync(product.Id);
            if (existingProduct == null)
                return NotFound();

            existingProduct.Name = product.Name;
            existingProduct.Price = product.Price;
            existingProduct.Description = product.Description;
            existingProduct.Stock = product.Stock;
            existingProduct.CategoryId = product.CategoryId;

            if (imageFile != null && imageFile.Length > 0)
            {
                DeleteImage(existingProduct.ImageUrl);
                existingProduct.ImageUrl = await SaveImage(imageFile);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =======================
        // DELETE GET
        // =======================
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            return View("~/Areas/Admin/Views/Admin/Delete.cshtml", product);
        }

        // =======================
        // DELETE POST
        // =======================
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

        // =======================
        // MARCAR / DESMARCAR BANNER HOME (UNO ACTIVO)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBanner(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound();

            if (!product.IsHomeBanner)
            {
                var activeBanners = await _context.Products
                    .Where(p => p.IsHomeBanner)
                    .ToListAsync();

                foreach (var banner in activeBanners)
                {
                    banner.IsHomeBanner = false;
                }

                product.IsHomeBanner = true;
            }
            else
            {
                product.IsHomeBanner = false;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =======================
        // MÉTODOS AUXILIARES
        // =======================
        private void LoadCategories(int? selectedId = null)
        {
            var categories = _context.Categories.ToList();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedId);
        }

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            string folder = Path.Combine(_environment.WebRootPath, "images/products");
            Directory.CreateDirectory(folder);

            string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
            string path = Path.Combine(folder, fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return "/images/products/" + fileName;
        }

        private void DeleteImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return;

            string path = Path.Combine(
                _environment.WebRootPath,
                imageUrl.TrimStart('/')
            );

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
    }
}
