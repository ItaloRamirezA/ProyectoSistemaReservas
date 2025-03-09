using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ServidorTCP
{
    // Clase para mantener el estado de la conexión
    public class StateObject
    {
        public Socket workSocket = null!;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder sb = new StringBuilder();
    }

    public class HttpServer
    {
        private static int PORT = 8080;
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public static void Main(string[] args)
        {
            // Generamos el par de claves RSA al iniciar el servidor (se usarán en toda la sesión)
            RSAEncryption.Initialize();
            Console.WriteLine("Clave pública del servidor (envíala al cliente para encriptar):");
            Console.WriteLine(RSAEncryption.GetPublicKeyString());
            StartListening();
        }

        public static void StartListening()
        {
            IPAddress ipAddress = GetLocalIpAddress();
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PORT);
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
                Console.WriteLine("Servidor HTTP escuchando en {0}:{1}", ipAddress, PORT);

                while (true)
                {
                    allDone.Reset();
                    Console.WriteLine("Esperando conexión...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    allDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
            }
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();
            Socket listener = (Socket)ar.AsyncState!;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState!;
            Socket handler = state.workSocket;
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // Recopilar datos recibidos
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                string receivedData = state.sb.ToString();

                // Verificamos si la petición está completamente recibida
                if (receivedData.Contains("\r\n\r\n"))
                {
                    // Si la petición empieza con "ENCRYPTED:" se desencripta el mensaje
                    if (receivedData.StartsWith("ENCRYPTED:"))
                    {
                        string encryptedPart = receivedData.Substring("ENCRYPTED:".Length);
                        try
                        {
                            receivedData = RSAEncryption.Decrypt(encryptedPart);
                        }
                        catch (Exception ex)
                        {
                            SendResponse(handler, "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nError al desencriptar la petición: " + ex.Message);
                            return;
                        }
                    }

                    Console.WriteLine("Se recibió la petición HTTP:");
                    Console.WriteLine(receivedData);

                    string httpResponse = "";
                    // Se extrae la línea de la petición (ej: "GET /usuarios HTTP/1.1")
                    string[] requestLines = receivedData.Split("\r\n");
                    string requestLine = requestLines[0];

                    // Separar método y ruta
                    string[] requestParts = requestLine.Split(' ');
                    if (requestParts.Length < 2)
                    {
                        httpResponse = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nSolicitud malformada";
                    }
                    else
                    {
                        string method = requestParts[0];
                        string path = requestParts[1];

                        // Primero, comprobamos si es login o acceso protegido
                        if (path.Equals("/login", StringComparison.InvariantCultureIgnoreCase) && method == "POST")
                        {
                            int bodyIndex = receivedData.IndexOf("\r\n\r\n");
                            if (bodyIndex > 0 && bodyIndex + 4 < receivedData.Length)
                            {
                                string body = receivedData.Substring(bodyIndex + 4);
                                try
                                {
                                    var creds = JsonSerializer.Deserialize<Credentials>(body);
                                    if (creds != null && creds.Username == "Italo" && creds.Password == "apruebamePedro")
                                    {
                                        string token = JwtManager.GenerateToken(creds.Username);
                                        var responseObj = new { token = token };
                                        httpResponse = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n" +
                                                       JsonSerializer.Serialize(responseObj);
                                    }
                                    else
                                    {
                                        httpResponse = "HTTP/1.1 401 Unauthorized\r\nContent-Type: text/plain\r\n\r\nCredenciales inválidas";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    httpResponse = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nError al procesar el cuerpo: " + ex.Message;
                                }
                            }
                            else
                            {
                                httpResponse = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nNo se encontró cuerpo en la petición";
                            }
                        }
                        else if (path.Equals("/datos", StringComparison.InvariantCultureIgnoreCase) && method == "GET")
                        {
                            string jwt = GetJwtFromRequest(receivedData);
                            bool tokenValido = JwtManager.ValidateToken(jwt);
                            if (tokenValido)
                            {
                                httpResponse = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n" +
                                               "{\"mensaje\":\"Acceso concedido y datos procesados.\"}";
                            }
                            else
                            {
                                httpResponse = "HTTP/1.1 401 Unauthorized\r\nContent-Type: text/plain\r\n\r\nToken inválido o expirado";
                            }
                        }
                        else if (path.StartsWith("/usuarios", StringComparison.InvariantCultureIgnoreCase))
                        {
                            int bodyIndex = receivedData.IndexOf("\r\n\r\n");
                            string body = "";
                            if (bodyIndex > 0 && bodyIndex + 4 < receivedData.Length)
                                body = receivedData.Substring(bodyIndex + 4);

                            httpResponse = HandleCrudOperations(method, path, body, handler.RemoteEndPoint?.ToString() ?? "Desconocido");
                        }
                        else
                        {
                            httpResponse = "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nRecurso no encontrado";
                        }
                    }
                    SendResponse(handler, httpResponse);
                }
                else
                {
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private static void SendResponse(Socket handler, string httpResponse)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(httpResponse);
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState!;
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Se enviaron {0} bytes al cliente.", bytesSent);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en SendCallback: {0}", ex.ToString());
            }
        }

        // Extrae el token JWT del header Authorization de la petición HTTP
        private static string GetJwtFromRequest(string request)
        {
            string[] lines = request.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                if (line.StartsWith("Authorization:", StringComparison.InvariantCultureIgnoreCase))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && parts[1].Trim() == "Bearer")
                    {
                        return parts[2].Trim();
                    }
                }
            }
            return "";
        }

        // Obtiene una dirección IP local válida (si no encuentra otra, retorna Loopback)
        private static IPAddress GetLocalIpAddress()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.Equals(IPAddress.Loopback))
                {
                    return ip;
                }
            }
            return IPAddress.Loopback;
        }

        // *************************************************************
        // *************** CRUD PARA USUARIOS Y CITAS ******************
        // *************************************************************
        // Se ha agregado un parámetro adicional (clientInfo) para registrar la operación
        private static string HandleCrudOperations(string method, string path, string body, string clientInfo)
        {
            List<Usuario> usuarios = LoadUsuarios();
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string response = "";
            try
            {
                // Operaciones sobre "/usuarios"
                if (segments.Length == 1)
                {
                    if (method == "GET")
                    {
                        // Introduce un retraso de 7 segundos para simular una operación larga y probar los clientes asincrónicos
                        System.Threading.Thread.Sleep(7000);
                        response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n" +
                                   JsonSerializer.Serialize(usuarios);
                    }
                    else if (method == "POST")
                    {
                        Usuario? nuevoUsuario = JsonSerializer.Deserialize<Usuario>(body);
                        if (nuevoUsuario == null)
                        {
                            response = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nError al deserializar el usuario.";
                        }
                        else
                        {
                            usuarios.Add(nuevoUsuario);
                            SaveUsuarios(usuarios);
                            response = "HTTP/1.1 201 Created\r\nContent-Type: application/json\r\n\r\n" + JsonSerializer.Serialize(nuevoUsuario);
                            LogCrudOperation("CREAR USUARIO", nuevoUsuario.Id, clientInfo);
                        }
                    }
                    else
                    {
                        response = "HTTP/1.1 405 Method Not Allowed\r\nContent-Type: text/plain\r\n\r\nMétodo no permitido en /usuarios";
                    }
                }
                // Operaciones sobre "/usuarios/{id}"
                else if (segments.Length == 2)
                {
                    string userId = segments[1];
                    Usuario? user = usuarios.FirstOrDefault(u => u.Id == userId);
                    if (user == null)
                    {
                        response = "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nUsuario no encontrado";
                    }
                    else
                    {
                        if (method == "GET")
                        {
                            response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n" +
                                       JsonSerializer.Serialize(user);
                        }
                        else if (method == "PUT")
                        {
                            Usuario? updatedUser = JsonSerializer.Deserialize<Usuario>(body);
                            if (updatedUser == null)
                            {
                                response = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nError al deserializar el usuario.";
                            }
                            else
                            {
                                user.Nombre = updatedUser.Nombre;
                                user.Email = updatedUser.Email;
                                user.Telefono = updatedUser.Telefono;
                                SaveUsuarios(usuarios);
                                response = "HTTP/1.1 204 No Content\r\n\r\n";
                                LogCrudOperation("ACTUALIZAR USUARIO", user.Id, clientInfo);
                            }
                        }
                        else if (method == "DELETE")
                        {
                            usuarios.Remove(user);
                            SaveUsuarios(usuarios);
                            response = "HTTP/1.1 204 No Content\r\n\r\n";
                            LogCrudOperation("ELIMINAR USUARIO", user.Id, clientInfo);
                        }
                        else
                        {
                            response = "HTTP/1.1 405 Method Not Allowed\r\nContent-Type: text/plain\r\n\r\nMétodo no permitido en /usuarios/{id}";
                        }
                    }
                }
                // Operaciones sobre "/usuarios/{id}/citas" o "/usuarios/{id}/citas/{citaId}"
                else if (segments.Length >= 3 && segments[0].Equals("usuarios", StringComparison.InvariantCultureIgnoreCase) && segments[2].Equals("citas", StringComparison.InvariantCultureIgnoreCase))
                {
                    string userId = segments[1];
                    Usuario? user = usuarios.FirstOrDefault(u => u.Id == userId);
                    if (user == null)
                    {
                        response = "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nUsuario no encontrado";
                    }
                    else
                    {
                        // Rutas: /usuarios/{id}/citas
                        if (segments.Length == 3)
                        {
                            if (method == "GET")
                            {
                                response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n" +
                                           JsonSerializer.Serialize(user.Citas);
                            }
                            else if (method == "POST")
                            {
                                Cita? nuevaCita = JsonSerializer.Deserialize<Cita>(body);
                                if (nuevaCita == null)
                                {
                                    response = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nError al deserializar la cita.";
                                }
                                else
                                {
                                    user.Citas.Add(nuevaCita);
                                    SaveUsuarios(usuarios);
                                    response = "HTTP/1.1 201 Created\r\nContent-Type: application/json\r\n\r\n" +
                                               JsonSerializer.Serialize(nuevaCita);
                                    LogCrudOperation("CREAR CITA", nuevaCita.Id, clientInfo);
                                }
                            }
                            else
                            {
                                response = "HTTP/1.1 405 Method Not Allowed\r\nContent-Type: text/plain\r\n\r\nMétodo no permitido en /usuarios/{id}/citas";
                            }
                        }
                        // Rutas: /usuarios/{id}/citas/{citaId}
                        else if (segments.Length == 4)
                        {
                            string citaId = segments[3];
                            Cita? cita = user.Citas.FirstOrDefault(c => c.Id == citaId);
                            if (cita == null)
                            {
                                response = "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nCita no encontrada";
                            }
                            else
                            {
                                if (method == "GET")
                                {
                                    response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n" +
                                               JsonSerializer.Serialize(cita);
                                }
                                else if (method == "PUT")
                                {
                                    Cita? updatedCita = JsonSerializer.Deserialize<Cita>(body);
                                    if (updatedCita == null)
                                    {
                                        response = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\n\r\nError al deserializar la cita.";
                                    }
                                    else
                                    {
                                        cita.Fecha = updatedCita.Fecha;
                                        cita.Servicio = updatedCita.Servicio;
                                        cita.Lugar = updatedCita.Lugar;
                                        cita.MetodoPago = updatedCita.MetodoPago;
                                        SaveUsuarios(usuarios);
                                        response = "HTTP/1.1 204 No Content\r\n\r\n";
                                        LogCrudOperation("ACTUALIZAR CITA", cita.Id, clientInfo);
                                    }
                                }
                                else if (method == "DELETE")
                                {
                                    user.Citas.Remove(cita);
                                    SaveUsuarios(usuarios);
                                    response = "HTTP/1.1 204 No Content\r\n\r\n";
                                    LogCrudOperation("ELIMINAR CITA", cita.Id, clientInfo);
                                }
                                else
                                {
                                    response = "HTTP/1.1 405 Method Not Allowed\r\nContent-Type: text/plain\r\n\r\nMétodo no permitido en /usuarios/{id}/citas/{citaId}";
                                }
                            }
                        }
                        else
                        {
                            response = "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nRecurso no encontrado";
                        }
                    }
                }
                else
                {
                    response = "HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nRecurso no encontrado";
                }
            }
            catch (Exception ex)
            {
                response = "HTTP/1.1 500 Internal Server Error\r\nContent-Type: text/plain\r\n\r\n" + ex.Message;
            }
            return response;
        }

        // Métodos para cargar y guardar los usuarios en el archivo JSON
        private static List<Usuario> LoadUsuarios()
        {
            string filePath = "database.json";
            if (!File.Exists(filePath))
                return new List<Usuario>();
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<Usuario>>(json) ?? new List<Usuario>();
        }

        private static void SaveUsuarios(List<Usuario> usuarios)
        {
            string filePath = "database.json";
            string json = JsonSerializer.Serialize(usuarios, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        // Función para registrar las operaciones CRUD en un archivo de log
        private static void LogCrudOperation(string operation, string resourceId, string clientInfo)
        {
            string logEntry = $"{DateTime.Now}: Operación {operation} sobre el recurso {resourceId} realizada por {clientInfo}";
            File.AppendAllText("crud_log.txt", logEntry + Environment.NewLine);
        }
    }

    // Modelo para representar las credenciales de login
    public class Credentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // Modelo para Usuario
    public class Usuario
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public List<Cita> Citas { get; set; } = new List<Cita>();
    }

    // Modelo para Cita
    public class Cita
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Fecha { get; set; }
        public string Servicio { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }

    // Clase para gestionar los tokens JWT
    public static class JwtManager
    {
        private const string SecretKey = "mysupersecret_secret_key!1234567";
        private const int TokenValidityMinutes = 30;

        public static string GenerateToken(string username)
        {
            // En este ejemplo se usa un método simple de generación de token.
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + DateTime.UtcNow));
        }

        public static bool ValidateToken(string token)
        {
            return !string.IsNullOrEmpty(token);
        }
    }

    // Clase para implementar cifrado asimétrico con RSA
    public static class RSAEncryption
    {
        private static RSA? rsa;

        // Inicializa la clave RSA (se genera un par de claves)
        public static void Initialize()
        {
            rsa = RSA.Create(2048);
        }

        // Devuelve la clave pública en formato Base64 (para que el cliente la use para encriptar)
        public static string GetPublicKeyString()
        {
            if (rsa == null)
                throw new Exception("RSA no inicializado.");
            // Exporta la clave pública en formato PEM
            var publicKey = rsa.ExportSubjectPublicKeyInfo();
            return Convert.ToBase64String(publicKey);
        }

        // Desencripta el mensaje encriptado (se espera que el mensaje esté en Base64)
        public static string Decrypt(string base64Encrypted)
        {
            if (rsa == null)
                throw new Exception("RSA no inicializado.");
            byte[] encryptedBytes = Convert.FromBase64String(base64Encrypted);
            byte[] decryptedBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.Pkcs1);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        // Método opcional para encriptar (usado en el lado del cliente)
        public static string Encrypt(string message, string base64PublicKey)
        {
            byte[] publicKeyBytes = Convert.FromBase64String(base64PublicKey);
            using RSA rsaPublic = RSA.Create();
            rsaPublic.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] encryptedBytes = rsaPublic.Encrypt(messageBytes, RSAEncryptionPadding.Pkcs1);
            return Convert.ToBase64String(encryptedBytes);
        }
    }
}