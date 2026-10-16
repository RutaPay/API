using Api.Tests.Infrastructure;

namespace Api.Tests;

/// <summary>
/// Todas las pruebas comparten una sola instancia de la API y su base de datos
/// (se ejecutan en serie dentro de esta colección).
/// </summary>
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiTestFactory>
{
}
