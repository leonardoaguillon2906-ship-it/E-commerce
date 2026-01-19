using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services.Payments;
using EcommerceApp.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcommerceApp.Areas.Cliente.Controllers
{
    [Area("Cliente")]
    [Authorize(Roles = "Cliente")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly MercadoPagoService _mercadoPagoService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(ApplicationDbContext context, MercadoPagoService mercadoPagoService, ILogger<CheckoutController> logger)
        {
            _context = context;
            _mercadoPagoService = mercadoPagoService;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm()
        {
            // ✅ MEJORA 1: Transacción de DB para evitar órdenes huérfanas
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var cart = HttpContext.Session.GetObject<List<CartItem>>("CART");
                if (cart == null || !cart.Any())
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // ✅ MEJORA 2: Validación y descuento preventivo de Stock
                foreach (var item in cart)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.Stock < item.Quantity)
                    {
                        TempData["Error"] = $"No hay stock suficiente para {item.Name}";
                        return RedirectToAction("Index", "Cart", new { area = "Cliente" });
                    }
                    product.Stock -= item.Quantity; 
                }

                var total = cart.Sum(i => i.Price * i.Quantity);
                var order = new Order
                {
                    UserId = userId,
                    Total = total,
                    Status = "Pendiente",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cart)
                {
                    _context.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); 

                // ✅ MEJORA 3: El servicio ahora usará BinaryMode=true para forzar respuesta inmediata
                var initPoint = await _mercadoPagoService.CrearPago(order.Total, order.Id);
                return Redirect(initPoint);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error en Confirm");
                TempData["Error"] = "Ocurrió un error procesando su orden.";
                return RedirectToAction("Index", "Cart", new { area = "Cliente" });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Success(string payment_id, string status, string external_reference)
        {
            if (int.TryParse(external_reference, out int orderId))
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null && status?.ToLower() == "approved")
                {
                    order.Status = "Procesando";
                    await _context.SaveChangesAsync();
                    HttpContext.Session.Remove("CART");
                    ViewBag.Message = "¡Pago aprobado!";
                    return View(order);
                }
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Failure(string external_reference)
        {
            if (int.TryParse(external_reference, out int orderId))
            {
                var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId);
                if (order != null)
                {
                    order.Status = "Rechazado";
                    // ✅ MEJORA 4: Devolver stock si el pago es rechazado (evita pérdida de inventario)
                    foreach (var item in order.OrderItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null) product.Stock += item.Quantity;
                    }
                    await _context.SaveChangesAsync();
                }
            }
            ViewBag.Message = "Tu pago fue rechazado.";
            return View();
        }
    }
}