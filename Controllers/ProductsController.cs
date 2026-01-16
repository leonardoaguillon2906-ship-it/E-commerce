using EcommerceApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;              // ✅ NECESARIO
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // ✅ AGREGADO

namespace EcommerceApp.Controllers
{
    [AllowAnonymous] // ✅ TODO EL CONTROLADOR ES PÚBLICO
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // CATÁLOGO PÚBLICO
        // =========================
        public IActionResult Index(int? categoryId)
        {
            // Cargar categorías activas
            ViewBag.Categories = _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.SelectedCategory = categoryId;

            // Consulta base
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .AsQueryable();

            // Filtro por categoría
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery
                    .Where(p => p.CategoryId == categoryId);
            }

            var products = productsQuery.ToList();

            return View(products);
        }

        // =========================
        // DETALLE PÚBLICO DEL PRODUCTO
        // =========================
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (product == null)
                return NotFound();

            return View(product);
        }
    }
}
