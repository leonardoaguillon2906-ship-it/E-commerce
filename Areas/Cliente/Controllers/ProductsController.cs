using EcommerceApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace EcommerceApp.Areas.Cliente.Controllers
{
    [Area("Cliente")]
    [Authorize(Roles = "Cliente")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // LISTA DE PRODUCTOS
        // URL: /Cliente/Products
        // =========================
        public async Task<IActionResult> Index(int? categoryId)
        {
            // Traer productos con categoría y stock, rastreados para reflejar cambios dinámicos
            var productsQuery = _context.Products
                                        .Include(p => p.Category)
                                        .AsQueryable(); // ❌ Quitamos AsNoTracking

            // Filtrar por categoría si existe
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            // Obtener lista de productos ordenada por nombre
            var products = await productsQuery
                                .OrderBy(p => p.Name)
                                .ToListAsync();

            // Eliminar posibles duplicados por ID
            products = products.GroupBy(p => p.Id)
                               .Select(g => g.First())
                               .ToList();

            // Pasar categorías y selección a la vista
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SelectedCategory = categoryId;

            return View(products);
        }

        // =========================
        // DETALLES DE PRODUCTO
        // URL: /Cliente/Products/Details/5
        // =========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            // Traer producto con seguimiento para reflejar cambios de stock
            var product = await _context.Products
                                        .Include(p => p.Category)
                                        .FirstOrDefaultAsync(p => p.Id == id); // ❌ Quitamos AsNoTracking

            if (product == null)
                return NotFound();

            // Validar stock mínimo (0)
            if (product.Stock < 0)
                product.Stock = 0;

            return View(product);
        }
    }
}
