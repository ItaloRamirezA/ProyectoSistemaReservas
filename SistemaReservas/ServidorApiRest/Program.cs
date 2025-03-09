using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SistemaReservasAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor.
builder.Services.AddControllers();

// Registrar el servicio de base de datos JSON como Singleton.
builder.Services.AddSingleton<JsonDatabaseService>();

// Configurar autenticaci�n JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "http://localhost",
            ValidAudience = "http://localhost",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("mysupersecret_secret_key!1234567"))
        };
    });

var app = builder.Build();

app.UseHttpsRedirection();

// Agregar los middlewares de autenticaci�n y autorizaci�n.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
