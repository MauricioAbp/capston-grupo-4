using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniMarket.Web.Data;

namespace MiniMarket.Web.Services
{
    public class IAAsistenteService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public IAAsistenteService(ApplicationDbContext context, IConfiguration configuration, HttpClient httpClient)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<string> PreguntarAlAsistenteAsync(string preguntaUsuario)
        {
            var totalProductos = await _context.Productos.CountAsync();
            var stockCritico = await _context.Productos.Where(p => p.Stock <= p.StockMinimo).CountAsync();
            var ventasHoy = await _context.Ventas
                .Where(v => v.Fecha.Date == DateTime.Today)
                .SumAsync(v => (decimal?)v.Total) ?? 0;

            string contextoNegocio = $"[Contexto del Negocio AmayaSoft]\n" +
                                   $"- Total de productos registrados: {totalProductos}\n" +
                                   $"- Productos con stock crítico o bajo: {stockCritico}\n" +
                                   $"- Ventas totales del día de hoy: S/. {ventasHoy:N2}\n" +
                                   $"Eres el asistente financiero y operativo experto de AmayaSoft, un software ERP para distribuidoras y minimarkets. Responde de forma clara, directa y ejecutiva al bodeguero basándote en los datos proporcionados.\n" +
                                   $"Pregunta del usuario: {preguntaUsuario}";

            var apiKey = _configuration["GoogleAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                return "El asistente de IA no está configurado con una API Key activa.";
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={apiKey}";

            var cuerpoRequest = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = contextoNegocio }
                        }
                    }
                }
            };

            var contenidoJson = new StringContent(JsonSerializer.Serialize(cuerpoRequest), Encoding.UTF8, "application/json");
            var respuesta = await _httpClient.PostAsync(url, contenidoJson);

            if (!respuesta.IsSuccessStatusCode)
            {
                var errorDetalle = await respuesta.Content.ReadAsStringAsync();
                Console.WriteLine($"--- ERROR GEMINI: {errorDetalle} ---");
                return "Lo siento, hubo un error al comunicarse con el servicio de IA.";
            }

            var jsonRespuesta = await respuesta.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonRespuesta);
            
            var textoRespuesta = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return textoRespuesta?.Trim() ?? "No se obtuvo respuesta de la IA.";
        }
    }
}