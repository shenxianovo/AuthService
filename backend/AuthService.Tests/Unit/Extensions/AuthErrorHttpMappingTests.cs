using AuthService.Common;
using AuthService.Extensions;
using Microsoft.AspNetCore.Http;

namespace AuthService.Tests.Unit.Extensions
{
    public class AuthErrorHttpMappingTests
    {
        /// <summary>
        /// The cure for mapping drift: every AuthError must declare an HTTP status.
        /// Adding a new code without a map entry fails here instead of silently
        /// returning 400 at runtime.
        /// </summary>
        [Fact]
        public void EveryAuthError_HasAnExplicitMapping()
        {
            var missing = Enum.GetValues<AuthError>()
                .Where(e => e != AuthError.None)
                .Where(e => !AuthErrorHttp.Entries.ContainsKey(e))
                .ToList();

            Assert.True(missing.Count == 0,
                $"AuthError values without an HTTP mapping: {string.Join(", ", missing)}");
        }

        [Fact]
        public void AllMappedStatuses_AreValidErrorCodes()
        {
            foreach (var (error, entry) in AuthErrorHttp.Entries)
            {
                Assert.True(entry.Status >= 400 && entry.Status < 500,
                    $"{error} maps to non-4xx status {entry.Status}");
                Assert.False(string.IsNullOrWhiteSpace(entry.Message),
                    $"{error} has an empty default message");
            }
        }

        [Theory]
        [InlineData(AuthError.EmailAlreadyExists, StatusCodes.Status409Conflict)]
        [InlineData(AuthError.InvalidCredentials, StatusCodes.Status401Unauthorized)]
        [InlineData(AuthError.ApiKeyNotFound, StatusCodes.Status404NotFound)]
        [InlineData(AuthError.VerificationRateLimited, StatusCodes.Status429TooManyRequests)]
        [InlineData(AuthError.UserNotFound, StatusCodes.Status400BadRequest)]
        public void Resolve_ReturnsExpectedStatus(AuthError error, int expectedStatus)
        {
            var (status, _) = AuthErrorHttp.Resolve(error);
            Assert.Equal(expectedStatus, status);
        }
    }
}
