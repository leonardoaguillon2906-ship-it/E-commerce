using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // ✅ PROTECCIÓN TOTAL DEL PANEL ADMIN
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Dashboard / Index
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products.ToListAsync();
            return View(products);
        }

        // Lista de Productos
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products.Include(p => p.Category).ToListAsync();
            return View(products);
        }

        // Crear Producto - GET
        public IActionResult Create()
        {
            ViewData["Categories"] = _context.Categories.ToList();
            return View();
        }

        // Crear Producto - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (ModelState.IsValid)
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Products));
            }
            ViewData["Categories"] = _context.Categories.ToList();
            return View(product);
        }

        // Editar Producto - GET
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewData["Categories"] = _context.Categories.ToList();
            return View(product);
        }

        // Editar Producto - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product)
        {
            if (ModelState.IsValid)
            {
                _context.Products.Update(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Products));
            }
            ViewData["Categories"] = _context.Categories.ToList();
            return View(product);
        }

        // Eliminar Producto
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // ============================================
                // ✅ ELIMINAR REGISTROS RELACIONADOS PRIMERO
                // (evita error FOREIGN KEY constraint failed)
                // ============================================

                // 🔹 OrderItems relacionados
                var relatedOrderItems = _context.OrderItems
                    .Where(o => o.ProductId == id);

                _context.OrderItems.RemoveRange(relatedOrderItems);

                // 🔹 CartItems relacionados (MUY IMPORTANTE)
                var relatedCartItems = _context.CartItems
                    .Where(c => c.ProductId == id);

                _context.CartItems.RemoveRange(relatedCartItems);

                // ============================================
                // ✅ AHORA SÍ SE PUEDE ELIMINAR EL PRODUCTO
                // ============================================

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Products));
        }
    }
}
