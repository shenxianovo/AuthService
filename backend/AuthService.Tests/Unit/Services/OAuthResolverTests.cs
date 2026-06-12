using AuthService.Common;
using AuthService.Services;

namespace AuthService.Tests.Unit.Services
{
    /// <summary>
    /// Full decision matrix for the OAuth resolution tree (ADR-003 / ADR-010).
    /// Pure function — every branch combination is exercised without a database.
    /// </summary>
    public class OAuthResolverTests
    {
        private static readonly Guid LinkedId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid CurrentId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000000003");

        // ── Case 1: provider already linked ────────────────────────────────────

        [Fact]
        public void Linked_PureLogin_LogsInAsLinked()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts { LinkedUserId = LinkedId });

            Assert.Equal(new OAuthDecision.LoginAsLinked(LinkedId), decision);
        }

        [Fact]
        public void Linked_BindingToSameUser_LogsInAsLinked()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts
            {
                LinkedUserId = LinkedId,
                CurrentUserId = LinkedId,
                CurrentUserExists = true,
            });

            Assert.Equal(new OAuthDecision.LoginAsLinked(LinkedId), decision);
        }

        [Fact]
        public void Linked_BindingToOtherUser_MergesLinkedIntoCurrent()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts
            {
                LinkedUserId = LinkedId,
                CurrentUserId = CurrentId,
                CurrentUserExists = true,
            });

            Assert.Equal(new OAuthDecision.MergeLinkedIntoCurrent(LinkedId, CurrentId), decision);
        }

        [Fact]
        public void Linked_BindingButCurrentUserMissing_Rejects()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts
            {
                LinkedUserId = LinkedId,
                CurrentUserId = CurrentId,
                CurrentUserExists = false,
            });

            Assert.Equal(new OAuthDecision.Reject(AuthError.UserNotFoundForMerge), decision);
        }

        // ── Case 2: not linked, binding flow ────────────────────────────────────

        [Fact]
        public void Binding_NoEmailOwner_LinksToCurrent()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts
            {
                CurrentUserId = CurrentId,
                CurrentUserExists = true,
            });

            Assert.Equal(new OAuthDecision.LinkToCurrent(CurrentId, null), decision);
        }

        [Fact]
        public void Binding_EmailOwnedByCurrentUser_LinksWithoutMerge()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts
            {
                CurrentUserId = CurrentId,
                CurrentUserExists = true,
                EmailOwnerUserId = CurrentId,
            });

            Assert.Equal(new OAuthDecision.LinkToCurrent(CurrentId, null), decision);
        }

        [Fact]
        public void Binding_EmailOwnedByOtherLiveUser_LinksAndMergesOwner()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts
            {
                CurrentUserId = CurrentId,
                CurrentUserExists = true,
                EmailOwnerUserId = OwnerId,
            });

            Assert.Equal(new OAuthDecision.LinkToCurrent(CurrentId, OwnerId), decision);
        }

        [Fact]
        public void Binding_CurrentUserMissing_Rejects()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts
            {
                CurrentUserId = CurrentId,
                CurrentUserExists = false,
            });

            Assert.Equal(new OAuthDecision.Reject(AuthError.UserNotFoundForMerge), decision);
        }

        // ── Case 3: pure login, email matches an existing user ─────────────────

        [Fact]
        public void PureLogin_EmailOwnerLive_LinksToOwner()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts { EmailOwnerUserId = OwnerId });

            Assert.Equal(new OAuthDecision.LinkToEmailOwner(OwnerId), decision);
        }

        // ── Case 4: nothing matches ─────────────────────────────────────────────

        [Fact]
        public void NothingMatches_CreatesNewUser()
        {
            var decision = OAuthResolver.Decide(new OAuthFacts());

            Assert.Equal(new OAuthDecision.CreateNewUser(), decision);
        }
    }
}
