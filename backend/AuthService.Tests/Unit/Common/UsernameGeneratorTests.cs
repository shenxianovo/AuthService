using AuthService.Common;

namespace AuthService.Tests.Unit.Common
{
    public class UsernameGeneratorTests
    {
        private static Func<string, Task<bool>> NoneTaken => _ => Task.FromResult(false);
        private static Func<string, Task<bool>> Taken(params string[] taken)
        {
            var set = new HashSet<string>(taken);
            return name => Task.FromResult(set.Contains(name));
        }

        [Fact]
        public async Task UsesProviderLogin_WhenAvailable()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync("octocat", "octo@example.com", NoneTaken);
            Assert.Equal("octocat", result);
        }

        [Fact]
        public async Task FallsBackToEmailLocalPart_WhenNoProviderLogin()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync(null, "alice@example.com", NoneTaken);
            Assert.Equal("alice", result);
        }

        [Fact]
        public async Task SanitizesUppercaseAndDots()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync(null, "Alice.Smith@example.com", NoneTaken);
            Assert.Equal("alice-smith", result);
        }

        [Fact]
        public async Task CollapsesConsecutiveAndStripsLeadingTrailingHyphens()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync("--foo..bar--", null, NoneTaken);
            Assert.Equal("foo-bar", result);
        }

        [Fact]
        public async Task FallsBackToRandom_WhenAllInputsInvalid()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync(null, null, NoneTaken);
            Assert.StartsWith("user-", result);
            Assert.Equal(13, result.Length);
        }

        [Fact]
        public async Task FallsBackToRandom_WhenSanitizedTooShort()
        {
            // After sanitization, "ab" is only 2 chars (below min 3)
            var result = await UsernameGenerator.GenerateUniqueAsync("ab", null, NoneTaken);
            Assert.StartsWith("user-", result);
        }

        [Fact]
        public async Task FallsBackToRandom_WhenBaseIsReserved()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync("settings", null, NoneTaken);
            Assert.StartsWith("user-", result);
        }

        [Fact]
        public async Task AppendsSuffixOnCollision()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync("alice", null, Taken("alice"));
            Assert.Equal("alice2", result);
        }

        [Fact]
        public async Task IncrementsSuffixUntilUnique()
        {
            var result = await UsernameGenerator.GenerateUniqueAsync("alice", null, Taken("alice", "alice2", "alice3"));
            Assert.Equal("alice4", result);
        }

        [Fact]
        public async Task FallsBackToRandom_WhenSuffixOverflowsMaxLength()
        {
            var longBase = new string('a', 39);
            var result = await UsernameGenerator.GenerateUniqueAsync(longBase, null, Taken(longBase));
            Assert.StartsWith("user-", result);
        }

        [Fact]
        public async Task TruncatesInputExceedingMaxLength()
        {
            var tooLong = new string('a', 50);
            var result = await UsernameGenerator.GenerateUniqueAsync(tooLong, null, NoneTaken);
            Assert.Equal(39, result.Length);
        }
    }
}