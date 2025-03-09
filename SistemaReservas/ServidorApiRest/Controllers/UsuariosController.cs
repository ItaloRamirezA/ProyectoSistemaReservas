using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaReservasAPI.Models;
using SistemaReservasAPI.Services;
using System.Collections.Generic;
using System.Linq;

namespace SistemaReservasAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        // Servicio que maneja la lectura y escritura de usuarios en el archivo JSON
        private readonly JsonDatabaseService _dbService;

        // Constructor que inyecta el servicio de base de datos JSON en el controlador
        public UsuariosController(JsonDatabaseService dbService)
        {
            _dbService = dbService;
        }

        // *************************************************************
        // ************************** USUARIOS *************************
        // *************************************************************

        // GET: api/usuarios
        // Método que devuelve la lista completa de usuarios almacenados en la base de datos JSON
        [HttpGet]
        public ActionResult<IEnumerable<Usuario>> GetUsuarios()
        {
            // Obtiene la lista de usuarios del servicio
            var usuarios = _dbService.ObtenerUsuarios();
            // Devuelve la lista de usuarios con un código HTTP 200
            return Ok(usuarios);
        }

        // GET: api/usuarios/{id}
        // Método que devuelve un usuario específico basado en su ID
        [HttpGet("{id}")]
        public ActionResult<Usuario> GetUsuario(string id)
        {
            // Obtiene la lista de usuarios
            var usuarios = _dbService.ObtenerUsuarios();
            // Busca el usuario con el ID especificado
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}"); // Retorna 404 si no se encuentra el usuario
            return Ok(usuario); // Devuelve el usuario encontrado con un código HTTP 200
        }

        // POST: api/usuarios
        // Método para crear un nuevo usuario
        // Recibe un objeto Usuario en el cuerpo de la petición y lo agrega a la base de datos JSON
        [HttpPost]
        public ActionResult<Usuario> CrearUsuario([FromBody] Usuario usuario)
        {
            // Obtiene la lista actual de usuarios
            var usuarios = _dbService.ObtenerUsuarios();
            // Agrega el nuevo usuario a la lista
            usuarios.Add(usuario);
            // Guarda la lista actualizada en el archivo JSON
            _dbService.GuardarUsuarios(usuarios);
            // Retorna 201 Created, incluyendo la ubicación del nuevo recurso y el objeto creado
            return CreatedAtAction(nameof(GetUsuario), new { id = usuario.Id }, usuario);
        }

        // PUT: api/usuarios/{id}
        // Método para actualizar la información de un usuario existente
        // Recibe un usuario actualizado en el cuerpo de la petición y lo utiliza para modificar el registro existente
        [HttpPut("{id}")]
        public IActionResult ActualizarUsuario(string id, [FromBody] Usuario usuarioActualizado)
        {
            var usuarios = _dbService.ObtenerUsuarios(); // Obtiene la lista de usuarios
            var usuario = usuarios.FirstOrDefault(u => u.Id == id); // Busca el usuario a actualizar por ID
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}"); // Retorna 404 si el usuario no existe

            // Actualiza los campos del usuario con los valores del objeto actualizado
            usuario.Nombre = usuarioActualizado.Nombre;
            usuario.Email = usuarioActualizado.Email;
            usuario.Telefono = usuarioActualizado.Telefono;
            _dbService.GuardarUsuarios(usuarios); // Guarda los cambios en el archivo JSON
            return NoContent(); // Retorna 204 No Content para indicar que la actualización fue exitosa
        }

        // DELETE: api/usuarios/{id}
        // Método para eliminar un usuario basado en su ID
        [HttpDelete("{id}")]
        public IActionResult EliminarUsuario(string id)
        {
            // Obtiene la lista de usuarios
            var usuarios = _dbService.ObtenerUsuarios();
            // Busca el usuario por ID
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}"); // Retorna 404 si el usuario no existe
            usuarios.Remove(usuario); // Elimina el usuario de la lista
            // Guarda la lista actualizada en el archivo JSON
            _dbService.GuardarUsuarios(usuarios);
            // Retorna 204 No Content para indicar que la eliminación fue exitosa
            return NoContent();
        }

        // *************************************************************
        // *************************** CITAS ***************************
        // *************************************************************

        // GET: api/usuarios/{id}/citas
        [HttpGet("{id}/citas")]
        public IActionResult ObtenerCitas(string id)
        {
            // Obtiene la lista de usuarios
            var usuarios = _dbService.ObtenerUsuarios();
            // Busca el usuario por ID
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}"); // Retorna 404 si no se encuentra el usuario
            // Devuelve la lista de citas del usuario
            return Ok(usuario.Citas);
        }

        // GET: api/usuarios/{id}/citas/{citaId}
        // Método que devuelve una cita específica de un usuario basado en el ID de la cita
        [HttpGet("{id}/citas/{citaId}")]
        public IActionResult GetCita(string id, string citaId)
        {
            // Obtiene la lista de usuarios
            var usuarios = _dbService.ObtenerUsuarios();
            // Busca el usuario por ID
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}"); // Retorna 404 si el usuario no existe

            // Busca la cita específica por su ID
            var cita = usuario.Citas.FirstOrDefault(c => c.Id == citaId);
            if (cita == null)
                return NotFound($"Cita no encontrada con id: {citaId} para el usuario {id}");
            // Devuelve la cita encontrada con un código HTTP 200
            return Ok(cita);
        }

        // POST: api/usuarios/{id}/citas
        // Método para agregar una nueva cita a un usuario específico
        [HttpPost("{id}/citas")]
        public IActionResult AgregarCita(string id, [FromBody] Cita cita)
        {
            var usuarios = _dbService.ObtenerUsuarios();
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}");
            usuario.Citas.Add(cita);
            _dbService.GuardarUsuarios(usuarios);
            return CreatedAtAction(nameof(ObtenerCitas), new { id = id }, cita);
        }

        // PUT: api/usuarios/{id}/citas/{citaId}
        [HttpPut("{id}/citas/{citaId}")]
        public IActionResult ActualizarCita(string id, string citaId, [FromBody] Cita citaActualizada)
        {
            // Obtiene la lista de usuarios
            var usuarios = _dbService.ObtenerUsuarios();
            // Busca el usuario por ID
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}"); // Retorna 404 si el usuario no existe

            // Busca la cita por su ID
            var cita = usuario.Citas.FirstOrDefault(c => c.Id == citaId);
            if (cita == null)
                return NotFound($"Cita no encontrada con id: {citaId} para el usuario {id}"); // Retorna 404 si la cita no existe

            // Actualiza los campos de la cita con los datos recibidos
            cita.Fecha = citaActualizada.Fecha;
            cita.Servicio = citaActualizada.Servicio;
            cita.Lugar = citaActualizada.Lugar;
            cita.MetodoPago = citaActualizada.MetodoPago;
            // Guarda los cambios en el archivo JSON
            _dbService.GuardarUsuarios(usuarios);
            // Retorna 204 No Content para indicar que la actualización fue exitosa
            return NoContent();
        }

        // DELETE: api/usuarios/{id}/citas/{citaId}
        [HttpDelete("{id}/citas/{citaId}")]
        public IActionResult EliminarCita(string id, string citaId)
        {
            // Obtiene la lista de usuarios
            var usuarios = _dbService.ObtenerUsuarios();
            // Busca el usuario por ID
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null)
                return NotFound($"Usuario no encontrado con id: {id}"); // Retorna 404 si el usuario no existe

            // Busca la cita por su ID
            var cita = usuario.Citas.FirstOrDefault(c => c.Id == citaId); 
            if (cita == null)
                return NotFound($"Cita no encontrada con id: {citaId} para el usuario {id}"); // Retorna 404 si la cita no existe
            // Elimina la cita de la lista del usuario
            usuario.Citas.Remove(cita);
            // Guarda los cambios en el archivo JSON
            _dbService.GuardarUsuarios(usuarios);
            // Retorna 204 No Content para indicar que la eliminación fue exitosa
            return NoContent();
        }
    }
}
