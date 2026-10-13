using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMarket.Web.Data;
using MiniMarket.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniMarket.Web.Controllers
{
    [Authorize]
    public class ComprasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ComprasController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var compras = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Usuario)
                .OrderByDescending(c => c.Fecha)
                .ToListAsync();
            return View(compras);
        }

        public async Task<IActionResult> Create(int? productoId, int? cantidad)
        {
            if (productoId.HasValue && productoId.Value > 0)
            {
                var prod = await _context.Productos.FindAsync(productoId.Value);
                if (prod != null)
                {
                    ViewBag.PreloadProductoId = prod.Id;
                    ViewBag.PreloadNombre = prod.Nombre;
                    ViewBag.PreloadCosto = prod.Costo;
                    ViewBag.PreloadCantidad = (cantidad.HasValue && cantidad.Value > 0) ? cantidad.Value : 1;
                }
            }

            return View();
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Usuario)
                .Include(c => c.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (compra == null) return NotFound();

            return View(compra);
        }

        [HttpPost]
        public async Task<IActionResult> Registrar([FromBody] CompraRequestDTO modelo)
        {
            if (modelo == null || modelo.Detalles == null || !modelo.Detalles.Any())
                return Json(new { exito = false, mensaje = "El detalle de compra está vacío." });

            using var tran = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId) && User.Identity.IsAuthenticated)
                {
                    var user = await _userManager.FindByNameAsync(User.Identity.Name);
                    userId = user?.Id;
                }

                var compra = new Compra
                {
                    ProveedorId = modelo.ProveedorId,
                    Fecha = DateTime.Now,
                    NumeroDocumento = modelo.NumeroDocumento,
                    Total = modelo.Total,
                    UsuarioId = userId
                };

                _context.Compras.Add(compra);
                await _context.SaveChangesAsync();

                foreach (var item in modelo.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(item.ProductoId);
                    if (producto == null) throw new Exception($"El producto ID {item.ProductoId} no existe.");

                    // A) AUMENTAR STOCK Y ACTUALIZAR COSTO
                    producto.Stock += item.Cantidad;
                    producto.Costo = item.Precio;

                    // B) Guardar Detalle
                    var detalle = new DetalleCompra
                    {
                        CompraId = compra.Id,
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = item.Precio,
                        Total = item.Total
                    };
                    _context.DetalleCompras.Add(detalle);

                    // C) REGISTRAR EN KARDEX
                    var kardex = new MovimientoInventario
                    {
                        Fecha = DateTime.Now,
                        ProductoId = item.ProductoId,
                        TipoMovimiento = "ENTRADA", 
                        Cantidad = item.Cantidad,
                        Referencia = $"Compra {compra.NumeroDocumento}",
                        Usuario = User.Identity.Name ?? "Sistema"
                    };
                    _context.MovimientosInventario.Add(kardex);
                }

                await _context.SaveChangesAsync();
                await tran.CommitAsync();

                return Json(new { exito = true, mensaje = "Compra registrada correctamente" });
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { exito = false, mensaje = "Error al guardar: " + errorMsg });
            }
        }

        // ==========================================================
        //  MÉTODO NUEVO: BUSCADOR PARA COMPRAS (Muestra Stock 0)
        // ==========================================================
        [HttpGet]
        public async Task<IActionResult> BuscarProductos(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) 
                return Json(new { results = new List<object>() });

            string busqueda = term.Trim().ToLower();

            // NOTA: Aquí quitamos la restricción de "p.Stock > 0"
            var productos = await _context.Productos
                .Where(p => p.Estado == true && // Solo activos
                           (p.Nombre.ToLower().Contains(busqueda) || p.Id.ToString().Contains(busqueda)))
                .Select(p => new {
                    id = p.Id,
                    // Mostramos el stock actual para referencia
                    text = $"{p.Nombre} | Stock Actual: {p.Stock}", 
                    precio = p.Precio, // Precio referencial
                    stock = p.Stock,
                    nombre = p.Nombre
                })
                .Take(20)
                .ToListAsync();

            return Json(new { results = productos });
        }
    }

    public class CompraRequestDTO
    {
        public int ProveedorId { get; set; }
        public string NumeroDocumento { get; set; }
        public decimal Total { get; set; }
        public List<DetalleCompraDTO> Detalles { get; set; }
    }

    public class DetalleCompraDTO
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal Precio { get; set; }
        public decimal Total { get; set; }
    }
}