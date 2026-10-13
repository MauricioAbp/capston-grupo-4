using System.ComponentModel.DataAnnotations;

namespace MiniMarket.Web.Models
{
    public class Cliente
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "DNI / RUC")]
        public string Documento { get; set; }

        [Required]
        [Display(Name = "Nombre Completo")]
        public string Nombre { get; set; }

        public string Telefono { get; set; }  // Se quitó el ?
        public string Correo { get; set; }    // Se quitó el ?
        public string Direccion { get; set; } // Se quitó el ?

        public bool Estado { get; set; } = true;
    }
}