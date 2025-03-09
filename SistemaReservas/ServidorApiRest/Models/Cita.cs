using System;

namespace SistemaReservasAPI.Models
{
    public class Cita
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Fecha { get; set; }
        public string Servicio { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
