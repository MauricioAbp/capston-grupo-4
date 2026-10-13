using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration; // <-- Asegúrate de incluir este using

namespace MiniMarket.Web.Services
{
    public class FactilizaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _token;

        public FactilizaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            // Obtenemos el token de manera segura desde appsettings.json
            _token = configuration["FactilizaSettings:Token"] ?? string.Empty;

            _httpClient.BaseAddress = new Uri("https://api.factiliza.com/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<bool> EmitirBoletaAsync(string numeroDocumento, string clienteNombre, decimal total, decimal igv)
        {
            try
            {
                var payload = new
                {
                    tipo_comprobante = "03", // 03 = Boleta de Venta
                    serie = "B001",
                    cliente = new
                    {
                        tipo_documento = "1", // 1 = DNI
                        numero = numeroDocumento,
                        razon_social = clienteNombre
                    },
                    total = total,
                    igv = igv,
                    moneda = "PEN"
                };

                var response = await _httpClient.PostAsJsonAsync("comprobantes", payload);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Manejo de contingencia offline si la API no responde
                return false;
            }
        }

        public async Task<ConsultaDocumentoResult> ConsultarDocumentoAsync(string documento)
        {
            documento = documento?.Trim() ?? string.Empty;

            if (documento.Length != 8 && documento.Length != 11)
                return ConsultaDocumentoResult.Fallo("Ingrese un DNI de 8 dígitos o RUC de 11 dígitos.");

            if (string.IsNullOrWhiteSpace(_token))
                return ConsultaDocumentoResult.Fallo("No se configuró el token de Factiliza.");

            try
            {
                var tipo = documento.Length == 8 ? "dni" : "ruc";
                var response = await _httpClient.GetAsync($"{tipo}/info/{documento}");

                if (!response.IsSuccessStatusCode)
                    return ConsultaDocumentoResult.Fallo("No se encontró información para el documento ingresado.");

                using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                var root = json.RootElement;
                var data = TryGetProperty(root, "data", out var dataElement) ? dataElement : root;

                var nombre = documento.Length == 8
                    ? FirstString(data, "nombre_completo", "nombre", "nombres")
                    : FirstString(data, "nombre_o_razon_social", "razon_social", "nombre", "nombre_completo");

                if (string.IsNullOrWhiteSpace(nombre))
                    return ConsultaDocumentoResult.Fallo("La API respondió, pero no devolvió el nombre del cliente.");

                var direccion = FirstString(data, "direccion_completa", "direccion", "domicilio_fiscal");

                return new ConsultaDocumentoResult
                {
                    Exito = true,
                    Documento = documento,
                    Nombre = nombre,
                    Direccion = direccion
                };
            }
            catch
            {
                return ConsultaDocumentoResult.Fallo("No se pudo consultar la API de documentos.");
            }
        }

        private static string FirstString(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryGetProperty(element, name, out var property) && property.ValueKind == JsonValueKind.String)
                    return property.GetString();
            }

            return string.Empty;
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out property))
                return true;

            property = default;
            return false;
        }
    }

    public class ConsultaDocumentoResult
    {
        public bool Exito { get; set; }
        public string Documento { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string Mensaje { get; set; }

        public static ConsultaDocumentoResult Fallo(string mensaje)
        {
            return new ConsultaDocumentoResult { Exito = false, Mensaje = mensaje };
        }
    }
}
