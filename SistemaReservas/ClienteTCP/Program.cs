using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ClienteTCP
{
    // Clase para mantener el estado de la conexión
    // Se utiliza para almacenar el socket del cliente, el buffer de datos y un StringBuilder para acumular la información recibida.
    public class StateObject
    {
        // Socket utilizado para la comunicación
        public Socket WorkSocket { get; set; } = null!;
        // Tamaño fijo del buffer para la recepción de datos
        public const int BufferSize = 1024;
        // Buffer de bytes que se utiliza para almacenar los datos recibidos
        public byte[] Buffer { get; } = new byte[BufferSize];
        // Acumulador de datos en formato de cadena (útil para armar la solicitud completa)
        public StringBuilder Sb { get; } = new StringBuilder();
    }

    // Clase para deserializar la respuesta del login
    // Se utiliza para convertir el JSON recibido en un objeto que contenga el token o el error.
    public class TokenResponse
    {
        // Propiedad que almacenará el token JWT si el login fue exitoso
        public string token { get; set; } = string.Empty;
        // Propiedad que almacenará el mensaje de error en caso de fallo en el login
        public string Error { get; set; } = string.Empty;
    }

    public class HttpClientSocket
    {
        // Puerto en el que el ClienteTCP se conectará al ServidorTCP
        private const int Port = 8080;
        // Método que retorna la dirección IP del servidor; se debe ajustar según la red (ejemplo: "192.168.1.35")
        private static IPAddress GetServerIpAddress() => IPAddress.Parse("192.168.1.35");

        // Punto de entrada del ClienteTCP
        public static void Main(string[] args)
        {
            // Primero, obtener el token a través del endpoint de login
            string token = GetToken();
            if (string.IsNullOrEmpty(token))
            {
                // Si no se obtuvo token, se muestra un mensaje de error y se finaliza la aplicación
                Console.WriteLine("Error al obtener el token. Verifica las credenciales o la conexión.");
                return;
            }

            // Mostrar el token obtenido en consola
            Console.WriteLine($"Token recibido: {token}");

            // Usar el token para acceder a un recurso protegido (por ejemplo, obtener datos)
            AccessProtectedResource(token);
        }

        // Método para enviar una petición POST /login con credenciales y retornar el token JWT obtenido
        private static string GetToken()
        {
            try
            {
                // Obtener la dirección IP del servidor
                IPAddress ipAddress = GetServerIpAddress();
                // Crear un endpoint remoto con la dirección IP y el puerto definido
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Port);

                // Crear un socket TCP para conectarse al servidor
                using Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                // Conectar al endpoint remoto
                client.Connect(remoteEP);
                Console.WriteLine($"Conectado a {client.RemoteEndPoint} para autenticarse.");

                // Definir las credenciales de login (ajusta según sea necesario)
                var credentials = new { Username = "usuario", Password = "contraseña" };
                // Serializar las credenciales a formato JSON
                string jsonCredentials = JsonSerializer.Serialize(credentials);

                // Construir la petición HTTP para el endpoint /login
                // Se incluyen los encabezados necesarios: método, host, tipo de contenido, longitud y conexión
                string httpRequest = $"POST /login HTTP/1.1\r\n" +
                                     $"Host: {ipAddress}\r\n" +
                                     "Content-Type: application/json\r\n" +
                                     $"Content-Length: {Encoding.UTF8.GetByteCount(jsonCredentials)}\r\n" +
                                     "Connection: Close\r\n\r\n" +
                                     jsonCredentials;
                // Convertir la cadena de la solicitud HTTP a bytes
                byte[] requestBytes = Encoding.UTF8.GetBytes(httpRequest);
                // Enviar la solicitud al servidor
                client.Send(requestBytes);

                // Leer la respuesta del servidor
                StringBuilder responseSb = new StringBuilder();
                byte[] buffer = new byte[1024];
                int bytesRead = 0;
                // Mientras haya datos disponibles, leer del socket y agregarlos al StringBuilder
                while ((bytesRead = client.Receive(buffer)) > 0)
                {
                    responseSb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }
                // Convertir la respuesta acumulada a cadena
                string response = responseSb.ToString();
                Console.WriteLine("Respuesta del login:");
                Console.WriteLine(response);

                // Buscar el inicio del JSON en la respuesta
                int jsonStart = response.IndexOf("{");
                if (jsonStart >= 0)
                {
                    string jsonPart = response.Substring(jsonStart);
                    // Deserializar la respuesta JSON en un objeto TokenResponse
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(jsonPart);
                    // Si se obtuvo el token, retornarlo
                    if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.token))
                    {
                        return tokenResponse.token;
                    }
                }
            }
            catch (Exception ex)
            {
                // Mostrar en consola el error ocurrido durante la obtención del token
                Console.WriteLine($"Error en GetToken: {ex.Message}");
            }
            // Si ocurre algún error, retornar una cadena vacía
            return "";
        }

        // Método para enviar una petición GET /datos utilizando el token JWT en el header Authorization
        private static void AccessProtectedResource(string token)
        {
            try
            {
                // Obtener la dirección IP del servidor
                IPAddress ipAddress = GetServerIpAddress();
                // Crear el endpoint remoto con la dirección IP y puerto
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Port);

                // Crear un socket TCP y conectarse al endpoint
                using Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                client.Connect(remoteEP);
                Console.WriteLine($"Conectado a {client.RemoteEndPoint} para acceder a /datos.");

                // Construir la petición HTTP para el endpoint /datos
                // Se incluye el header Authorization con el token JWT
                string httpRequest = $"GET /datos HTTP/1.1\r\n" +
                                     $"Host: {ipAddress}\r\n" +
                                     $"Authorization: Bearer {token}\r\n" +
                                     "Connection: Close\r\n\r\n";
                // Convertir la solicitud a bytes y enviarla
                byte[] requestBytes = Encoding.UTF8.GetBytes(httpRequest);
                client.Send(requestBytes);

                // Leer la respuesta del servidor
                StringBuilder responseSb = new StringBuilder();
                byte[] buffer = new byte[1024];
                int bytesRead = 0;
                while ((bytesRead = client.Receive(buffer)) > 0)
                {
                    responseSb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }
                // Convertir la respuesta acumulada a cadena y mostrarla
                string response = responseSb.ToString();
                Console.WriteLine("Respuesta del recurso protegido:");
                Console.WriteLine(response);
            }
            catch (Exception ex)
            {
                // Mostrar en consola cualquier error que ocurra al acceder al recurso protegido
                Console.WriteLine($"Error en AccessProtectedResource: {ex.Message}");
            }
        }
    }
}
