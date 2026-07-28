# RYDO mobile API client

This package is the shared HTTP and server-state foundation for the Passenger
and Driver Expo apps.

It provides:

- typed authentication and generic request/response contracts;
- JWT attachment and secure token-store abstraction;
- single-flight refresh-token rotation;
- normalized ASP.NET Core problem-detail, network, timeout, and cancellation errors;
- bounded retries for safe requests only; and
- a consistently configured TanStack Query client.

Refresh requests are deliberately never retried. The backend rotates refresh
tokens and treats replay as a session-compromise signal, so retrying after an
ambiguous network failure could revoke the session family.

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
