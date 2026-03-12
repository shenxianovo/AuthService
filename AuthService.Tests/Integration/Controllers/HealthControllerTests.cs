using AuthService.Tests.Fixtures;
using System.Net;

namespace AuthService.Tests.Integration.Controllers
{

    [Collection("Api Tests")]
    public class HealthControllerTests(ApiTestFixture fixture)
    {
        private readonly HttpClient _client = fixture.Client;

        [Fact]
        public async Task HealthEndpoint_Returns200()
        {
            var response = await _client.GetAsync("/api/v1/health", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
