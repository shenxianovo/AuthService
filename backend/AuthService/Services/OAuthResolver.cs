using AuthService.Common;
using AuthService.Entities;

namespace AuthService.Services
{
    /// <summary>
    /// The queried facts an OAuth callback resolution depends on — everything the
    /// decision needs, nothing it doesn't. Gathered up front by OAuthService so the
    /// decision itself (<see cref="OAuthResolver.Decide"/>) stays a pure function.
    /// Soft-deleted users never appear here: the cascade query filters (ADR-014)
    /// make them and their rows invisible to the gathering queries.
    /// </summary>
    public sealed record OAuthFacts
    {
        /// <summary>User the (provider, providerUserId) pair is already linked to, if any.</summary>
        public Guid? LinkedUserId { get; init; }

        /// <summary>Authenticated user carried in the signed OAuth state (binding flow), if any.</summary>
        public Guid? CurrentUserId { get; init; }
        public bool CurrentUserExists { get; init; }

        /// <summary>User who owns the provider-asserted email address, if any (globally unique, ADR-011).</summary>
        public Guid? EmailOwnerUserId { get; init; }
    }

    /// <summary>
    /// What an OAuth callback resolves to. OAuthService maps each decision onto
    /// account writes via IAccountService (ADR-010: AccountService is the single
    /// write authority over account composition).
    /// </summary>
    public abstract record OAuthDecision
    {
        private OAuthDecision() { }

        /// <summary>Provider already linked — log in as that user.</summary>
        public sealed record LoginAsLinked(Guid UserId) : OAuthDecision;

        /// <summary>Binding flow: the provider belongs to another user — merge them into the current user.</summary>
        public sealed record MergeLinkedIntoCurrent(Guid LinkedUserId, Guid CurrentUserId) : OAuthDecision;

        /// <summary>Binding flow: link the provider to the current user; if the email belongs to another live user, merge them in too.</summary>
        public sealed record LinkToCurrent(Guid CurrentUserId, Guid? MergeEmailOwnerUserId) : OAuthDecision;

        /// <summary>Pure login, email matches an existing user — link the provider to them.</summary>
        public sealed record LinkToEmailOwner(Guid EmailOwnerUserId) : OAuthDecision;

        /// <summary>No link, no binding, no email match — create a brand new user.</summary>
        public sealed record CreateNewUser : OAuthDecision;

        public sealed record Reject(AuthError Error) : OAuthDecision;
    }

    /// <summary>
    /// The OAuth resolution decision tree (ADR-003 / ADR-010), as a pure function:
    /// facts in, decision out. No I/O — exhaustively testable without a database.
    /// </summary>
    public static class OAuthResolver
    {
        public static OAuthDecision Decide(OAuthFacts f)
        {
            // Case 1: provider already linked to a user.
            if (f.LinkedUserId is Guid linked)
            {
                // Binding flow with a different owner — merge the linked user in.
                if (f.CurrentUserId is Guid current && linked != current)
                {
                    if (!f.CurrentUserExists)
                        return new OAuthDecision.Reject(AuthError.UserNotFoundForMerge);
                    return new OAuthDecision.MergeLinkedIntoCurrent(linked, current);
                }

                return new OAuthDecision.LoginAsLinked(linked);
            }

            // Case 2: provider not linked, binding flow — link it to the current user.
            if (f.CurrentUserId is Guid currentUser)
            {
                if (!f.CurrentUserExists)
                    return new OAuthDecision.Reject(AuthError.UserNotFoundForMerge);

                var mergeOwner = f.EmailOwnerUserId is Guid owner && owner != currentUser
                    ? f.EmailOwnerUserId
                    : null;
                return new OAuthDecision.LinkToCurrent(currentUser, mergeOwner);
            }

            // Case 3: pure login — email belongs to an existing user.
            if (f.EmailOwnerUserId is Guid emailOwner)
                return new OAuthDecision.LinkToEmailOwner(emailOwner);

            // Case 4: brand new user.
            return new OAuthDecision.CreateNewUser();
        }
    }
}
