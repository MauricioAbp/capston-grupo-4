using System.Collections.Generic;

namespace MiniMarket.Web.Models
{
    // Clase simple para recibir datos del carrito de compras
    public class VentaRequest
    {
        public int? ClienteId { get; set; }
        public decimal Total { get; set; }
        public List<DetalleVentaRequest> Detalles { get; set; }
    }

    public class DetalleVentaRequest
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal Precio { get; set; }
        public decimal Total { get; set; }
    }
}