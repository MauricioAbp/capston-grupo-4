using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMarket.Web.Models
{
    public class Venta
    {
        [Key]
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;

        public int? ClienteId { get; set; }
        [ForeignKey("ClienteId")]
        public virtual Cliente Cliente { get; set; }

        public string UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public virtual IdentityUser Usuario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Descuento { get; set; } = 0;

        /// <summary>Estado: "Activa" o "Anulada"</summary>
        public string Estado { get; set; } = "Activa";

        /// <summary>Método de pago: "Efectivo", "Tarjeta" o "QR"</summary>
        public string MetodoPago { get; set; } = "Efectivo";

        public virtual List<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    }
}