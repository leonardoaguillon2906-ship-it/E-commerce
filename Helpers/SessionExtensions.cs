using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace EcommerceApp.Helpers
{
    /// <summary>
    /// Extensiones de sesión para almacenar y recuperar objetos complejos.
    /// Permite guardar y obtener listas u otros objetos serializados en JSON.
    /// </summary>
    public static class SessionExtensions
    {
        /// <summary>
        /// Serializa y guarda un objeto en la sesión con la clave especificada.
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a guardar.</typeparam>
        /// <param name="session">La sesión actual.</param>
        /// <param name="key">Clave para almacenar el objeto.</param>
        /// <param name="value">Objeto a almacenar.</param>
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            if (session == null) 
                throw new ArgumentNullException(nameof(session));
            
            if (string.IsNullOrEmpty(key)) 
                throw new ArgumentException("La clave de sesión no puede estar vacía.", nameof(key));

            // Serializa el objeto a JSON y lo guarda en la sesión
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        /// <summary>
        /// Recupera un objeto de la sesión usando la clave especificada.
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a recuperar.</typeparam>
        /// <param name="session">La sesión actual.</param>
        /// <param name="key">Clave para recuperar el objeto.</param>
        /// <returns>El objeto deserializado o default(T) si no existe o ocurre error.</returns>
        public static T? GetObject<T>(this ISession session, string key)
        {
            if (session == null) 
                throw new ArgumentNullException(nameof(session));
            
            if (string.IsNullOrEmpty(key)) 
                throw new ArgumentException("La clave de sesión no puede estar vacía.", nameof(key));

            var value = session.GetString(key);
            if (string.IsNullOrEmpty(value))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(value);
            }
            catch (JsonException)
            {
                // Si ocurre un error al deserializar, se devuelve default
                return default;
            }
        }
    }
}
