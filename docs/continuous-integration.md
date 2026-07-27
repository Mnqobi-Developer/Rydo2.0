# Continuous integration

The `Quality gates` GitHub Actions workflow runs for every pull request into
`main`, every push to `main`, and manual dispatches.

## Current checks

The Passenger and Driver mobile jobs use their independent committed lockfiles
and perform:

1. Deterministic dependency installation with `npm ci`.
2. Expo ESLint validation.
3. TypeScript compilation without output.
4. Expo project compatibility checks.
5. Production dependency auditing at the high severity threshold.

The Admin dashboard job independently performs:

1. Deterministic dependency installation with `npm ci`.
2. Oxlint static analysis.
3. TypeScript compilation without output.
4. Vitest component tests in a browser-like environment.
5. A production Vite build.
6. Production dependency auditing at the high severity threshold.

The workflow has read-only repository permissions, cancels obsolete runs for the
same branch, and limits the job to 15 minutes.

## Expansion

The Backend API job will be introduced with its foundation branch. Each job
retains a stable name so it can become a required branch-protection check.

Deployment is intentionally excluded during local-first development. Staging
and production promotion jobs will be added only after hosting environments are
approved.
