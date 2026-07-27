# Contributing to RYDO

## Branches

Use lowercase, hyphenated names in the form `<type>/<scope>-<description>`.

Supported types include `feature`, `fix`, `ci`, `chore`, `docs`, `refactor`,
`release`, and `hotfix`.

Examples:

- `feature/passenger-ride-booking`
- `feature/driver-trip-controls`
- `feature/admin-driver-verification`
- `feature/api-trip-matching`
- `ci/mobile-quality-gates`
- `fix/auth-session-expiration`

Branch names must describe the product or engineering outcome. Tool- or
developer-specific prefixes are not used.

## Commits

Use Conventional Commit messages with an accurate scope, for example:

- `feat(passenger): add pickup selection`
- `feat(api): create trip matching service`
- `test(driver): cover ride request decisions`
- `ci(mobile): add type and lint checks`

## Pull requests

Keep each pull request focused on one coherent outcome. Include tests with the
behavior they protect, update documentation when contracts change, and merge
only after required checks pass.

Never commit credentials, signing keys, production configuration, or user data.
