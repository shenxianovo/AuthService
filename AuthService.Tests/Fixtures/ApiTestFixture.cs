using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthService.Tests.Fixtures
{
    public class ApiTestFixture : IDisposable
    {
        public HttpClient Client { get; }

        public ApiTestFixture()
        {
            var factory = new WebApplicationFactory<Program>();
            Client = factory.CreateClient();
        }

        public void Dispose() => Client.Dispose();
    }

    [CollectionDefinition("Api Tests")]
    public class ApiTestCollection : ICollectionFixture<ApiTestFixture> { }
}
