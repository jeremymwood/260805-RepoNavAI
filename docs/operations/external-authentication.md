# External authentication operations

RepoNavAI supports Google, Apple, and Microsoft sign-in alongside its local email and password flow. Provider authentication uses OpenID Connect or OAuth 2.0 authorization code flows. After a provider authenticates a person, RepoNavAI creates or locates an ASP.NET Core Identity user, stores the provider and stable subject in `UserLogins`, and issues its normal RepoNavAI JWT in an `HttpOnly`, `SameSite=Strict` cookie. Provider access and refresh tokens are not retained.

The provider callback returns a random two-minute exchange code in the URL fragment. RepoNavAI stores only the code's SHA-256 hash, atomically consumes it once, and then establishes the cookie session. JavaScript never receives the RepoNavAI JWT. Deploy the web proxy and API under the same browser site; production must use HTTPS so the session cookie is marked `Secure`.

## Shared setup

Configure `Authentication__FrontendUrl` as the exact browser origin. Register the public API callback URI for each enabled provider:

| Provider | Callback path |
| --- | --- |
| Google | `/api/auth/external/signin-google` |
| Apple | `/api/auth/external/signin-apple` |
| Microsoft | `/api/auth/external/signin-microsoft` |

Set the matching `Authentication__{Provider}__ClientId` and `Authentication__{Provider}__ClientSecret` values through local user secrets or the environment's approved secret store. A provider is disabled when either value is absent. Never place credentials in source, `.env.example`, logs, issue text, screenshots, or deployment output.

Use only the identity scopes required by RepoNavAI: `openid`, `email`, and `profile`, plus Apple's `name` scope. Do not grant Google APIs, Microsoft Graph, or Apple service permissions unless a separately reviewed feature requires them.

## Provider registration

Google requires a Google Cloud OAuth web client, an approved consent-screen configuration, and exact authorized redirect URIs. Microsoft requires an Entra app registration; the current `common` authority accepts supported work, school, and personal Microsoft accounts according to the app registration's selected account audience. Apple requires a Services ID associated with the web domain and redirect URI.

Apple's client secret is a signed ES256 JWT created from the Apple team ID, key ID, Services ID, and Sign in with Apple private key. Generate it only in an approved secret-management or deployment process. Store the resulting client secret in the environment secret store, keep the private key out of RepoNavAI runtime storage, record its expiration, and rotate it before expiry. Apple permits a limited lifetime, so alert well before the configured secret expires.

## Account safety

RepoNavAI keys a linked identity by provider plus the provider's immutable subject, not by email. A first external sign-in can create a passwordless local user. If the provider email already belongs to another RepoNavAI user, sign-in is rejected instead of silently merging accounts. Intentional linking and unlinking require a separately authenticated account-management flow; never implement linking by email alone.

Apple private relay addresses are valid account addresses. Display names may be absent, and Apple supplies name data only during the first authorization. The application must continue to work with an email-derived fallback name.

## Rotation and incident response

Rotate one provider at a time in staging, complete a real sign-in, verify the `UserLogins` association and RepoNavAI JWT, then promote the secret version to production. Existing linked users do not need database changes when a client secret rotates.

If a credential is exposed, revoke it in the provider console, replace the secret-store version, deploy the configuration revision, and review authentication failures without logging authorization codes, cookies, JWTs, provider tokens, client secrets, or Apple keys. Disabling one provider must leave local authentication and other configured providers available.

## Acceptance checks

- Complete first-time and returning sign-in for every enabled provider.
- Cancel consent and confirm a safe error returns to the sign-in screen.
- Confirm an existing local email is not automatically merged.
- Confirm private relay and missing display-name cases remain usable.
- Confirm the intended return route survives authentication.
- Confirm source, logs, browser URLs after callback cleanup, and telemetry contain no credentials or JWTs.
- Confirm a callback code can be exchanged once and is rejected after use or expiry.
- Confirm the session cookie is `HttpOnly`, `SameSite=Strict`, and `Secure` over HTTPS.
