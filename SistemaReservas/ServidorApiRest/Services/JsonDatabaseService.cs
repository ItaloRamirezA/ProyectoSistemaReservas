using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SistemaReservasAPI.Models;

namespace SistemaReservasAPI.Services
{
    // Servicio que gestiona la base de datos local utilizando un archivo JSON
    public class JsonDatabaseService
    {
        // Ruta del archivo JSON donde se almacenan los datos de usuarios
        private readonly string _filePath = "database.json";

        // Método para obtener la lista de usuarios desde el archivo JSON
        public List<Usuario> ObtenerUsuarios()
        {
            // Si el archivo JSON no existe, se retorna una lista vacía de usuarios
            if (!File.Exists(_filePath))
                return new List<Usuario>();
            // Lee todo el contenido del archivo JSON
            var json = File.ReadAllText(_filePath);

            // Deserializa el JSON a una lista de objetos de tipo Usuario.
            // Si la deserialización resulta nula, se retorna una nueva lista vacía.
            return JsonSerializer.Deserialize<List<Usuario>>(json) ?? new List<Usuario>();
        }

        // Método para guardar la lista de usuarios en el archivo JSON
        public void GuardarUsuarios(List<Usuario> usuarios)
        {
            // Serializa la lista de usuarios a formato JSON con indentación (formato legible)
            var json = JsonSerializer.Serialize(usuarios, new JsonSerializerOptions { WriteIndented = true });
            // Escribe el JSON serializado en el archivo, sobrescribiendo su contenido
            File.WriteAllText(_filePath, json);
        }
    }
}
