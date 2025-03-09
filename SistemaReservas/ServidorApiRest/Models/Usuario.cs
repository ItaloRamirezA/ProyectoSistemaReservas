using System;
using System.Collections.Generic;

namespace SistemaReservasAPI.Models
{
    public class Usuario
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        // La colección de citas se almacena como una lista dentro del usuario.
        public List<Cita> Citas { get; set; } = new List<Cita>();
    }
}
