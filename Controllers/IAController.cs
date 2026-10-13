using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Web.Services;

namespace MiniMarket.Web.Controllers
{
    [Authorize]
    public class IAController : Controller
    {
        private readonly IAAsistenteService _iaService;

        public IAController(IAAsistenteService iaService)
        {
            _iaService = iaService;
        }

        [HttpPost]
        public async Task<IActionResult> Consultar([FromBody] PreguntaDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Pregunta))
            {
                return BadRequest(new { respuesta = "Por favor ingresa una pregunta válida." });
            }

            var respuestaIa = await _iaService.PreguntarAlAsistenteAsync(dto.Pregunta);
            return Json(new { respuesta = respuestaIa });
        }
    }

    public class PreguntaDto
    {
        public string Pregunta { get; set; }
    }
}