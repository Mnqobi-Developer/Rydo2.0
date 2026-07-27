# Continuous integration

The `Quality gates` GitHub Actions workflow runs for every pull request into
`main`, every push to `main`, and manual dispatches.

## Current checks

The Passenger mobile job uses the committed lockfile and performs:

1. Deterministic dependency installation with `npm ci`.
2. Expo ESLint validation.
3. TypeScript compilation without output.
4. Expo project compatibility checks.
5. Production dependency auditing at the high severity threshold.

The workflow has read-only repository permissions, cancels obsolete runs for the
same branch, and limits the job to 15 minutes.

## Expansion

Driver mobile, Admin dashboard, and Backend API jobs will be introduced with
their respective foundation branches. Each job will retain a stable name so it
can become a required branch-protection check.

Deployment is intentionally excluded during local-first development. Staging
and production promotion jobs will be added only after hosting environments are
approved.
