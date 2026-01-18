using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
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
        private readonly IEmailService _emailService;
        private readonly EmailTemplateService _emailTemplateService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            ApplicationDbContext context,
            MercadoPagoService mercadoPagoService,
            IEmailService emailService,
            EmailTemplateService emailTemplateService,
            ILogger<CheckoutController> logger)
        {
            _context = context;
            _mercadoPagoService = mercadoPagoService;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                var cart = HttpContext.Session.GetObject<List<CartItem>>("CART");

                if (cart == null || !cart.Any())
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });

                return View(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el carrito para Checkout");
                return StatusCode(500, "Error al cargar el carrito.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm()
        {
            try
            {
                var cart = HttpContext.Session.GetObject<List<CartItem>>("CART");

                if (cart == null || !cart.Any())
                    return RedirectToAction("Index", "Cart", new { area = "Cliente" });

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return RedirectToAction("Login", "Account");

                foreach (var item in cart)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.Stock < item.Quantity)
                    {
                        TempData["Error"] = $"No hay stock suficiente para {item.Name}";
                        return RedirectToAction("Index", "Cart", new { area = "Cliente" });
                    }
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

                var initPoint = await _mercadoPagoService.CrearPago(order.Total, order.Id);
                return Redirect(initPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al confirmar el checkout");
                TempData["Error"] = "Ocurrió un error procesando su orden. Intente nuevamente.";
                return RedirectToAction("Index", "Cart", new { area = "Cliente" });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Success(string payment_id, string status, string external_reference)
        {
            try
            {
                if (string.IsNullOrEmpty(external_reference) || !int.TryParse(external_reference, out int orderId))
                    return RedirectToAction("Index", "Home", new { area = "" });

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                    return RedirectToAction("Index", "Home", new { area = "" });

                HttpContext.Session.Remove("CART");

                if (order.Status == "Pendiente")
                {
                    order.Status = "Procesando";
                    await _context.SaveChangesAsync();

                    var userEmail = await _context.Users
                        .Where(u => u.Id == order.UserId)
                        .Select(u => u.Email)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        try
                        {
                            var template = await _emailTemplateService.LoadAsync("OrderSuccess.html");
                            var body = template
                                .Replace("{{ORDER_ID}}", order.Id.ToString())
                                .Replace("{{TOTAL}}", order.Total.ToString("C"));

                            await _emailService.SendOrderConfirmationEmail(order);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error enviando email de confirmación para Order {order.Id}");
                        }
                    }
                }

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la página de Success del checkout");
                return StatusCode(500, "Ocurrió un error procesando la orden.");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Pending(string external_reference)
        {
            try
            {
                if (!string.IsNullOrEmpty(external_reference) &&
                    int.TryParse(external_reference, out int orderId))
                {
                    var order = await _context.Orders.FindAsync(orderId);
                    if (order != null)
                    {
                        order.Status = "Pendiente";
                        await _context.SaveChangesAsync();
                    }
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la página Pending del checkout");
                return StatusCode(500, "Ocurrió un error procesando la orden.");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Failure(string external_reference)
        {
            try
            {
                if (!string.IsNullOrEmpty(external_reference) &&
                    int.TryParse(external_reference, out int orderId))
                {
                    var order = await _context.Orders.FindAsync(orderId);
                    if (order != null)
                    {
                        order.Status = "Rechazado";
                        await _context.SaveChangesAsync();
                    }
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la página Failure del checkout");
                return StatusCode(500, "Ocurrió un error procesando la orden.");
            }
        }
    }
}
