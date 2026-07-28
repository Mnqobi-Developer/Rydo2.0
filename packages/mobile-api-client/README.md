# RYDO mobile API client

This package is the shared HTTP and server-state foundation for the Passenger
and Driver Expo apps.

It provides:

- typed authentication and generic request/response contracts;
- JWT attachment and secure token-store abstraction;
- proactive and reactive single-flight refresh-token rotation;
- startup session restoration and observable session states;
- backend session revocation with guaranteed local logout;
- normalized ASP.NET Core problem-detail, network, timeout, and cancellation errors;
- bounded retries for safe requests only; and
- consistently shared TanStack Query defaults instantiated by each app.

Refresh requests are deliberately never retried. The backend rotates refresh
tokens and treats replay as a session-compromise signal, so retrying after an
ambiguous network failure could revoke the session family.

Access tokens are refreshed before they enter their final 60 seconds. If the
refresh token has expired, or rotation fails, the encrypted token pair is
removed and the session becomes `expired`. Apps subscribe through
`AuthSessionProvider` and can render `restoring`, `authenticated`, `anonymous`,
`expired`, or `unavailable` states without reading credentials directly.

Feature packages should pass TanStack Query's `signal` to the client so queries
are cancelled when they become obsolete:

```ts
useQuery({
  queryKey: ['profile'],
  queryFn: ({ signal }) => apiClient.get('/api/v1/passengers/me/profile', { signal }),
})
```

The transport owns bounded network retries. TanStack Query retrying is disabled
to prevent multiplicative retries; mutations are never retried unless a caller
explicitly supplies `retry: 'always'` for an idempotent operation.
