# Backend architecture

The ASP.NET Core backend is RYDO's trusted decision layer. Mobile and web clients
request actions; the backend owns authorization, workflow validity, persistence,
payments, and real-time events.

The request flow is:

1. A controller receives and validates the request contract.
2. Authentication and authorization validate the JWT, session, role, and
   resource access.
3. A service applies business rules and valid state transitions.
4. Entity Framework Core reads or writes PostgreSQL/PostGIS data.
5. SignalR publishes relevant state changes.
6. The API returns a structured response.

Core domains include identity and sessions, passengers, driver onboarding,
vehicles, trips, matching, payments, ratings, disputes, administration, audit
logging, and real-time communication.

The API is also the database security boundary. RLS is enabled without client
policies on every RYDO table in Supabase's exposed `public` schema, so the Data
API denies Passenger, Driver, and Admin frontend access by default. The trusted
backend database role owns business access; application authorization remains in
the API. An event trigger enables RLS automatically for new public tables.

Trip state transitions are controlled by the backend: `Matching`,
`DriverAssigned`, `DriverArriving`, `InProgress`, `Completed`, `Cancelled`, and
`Expired`. Clients cannot bypass or invent these transitions.
