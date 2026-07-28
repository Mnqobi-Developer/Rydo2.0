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
5. Metro bundling with a non-secret CI API environment.
6. Production dependency auditing at the high severity threshold.

The shared Mobile API client job independently performs:

1. Deterministic dependency installation with `npm ci`.
2. Strict TypeScript compilation without output.
3. Transport, authentication rotation, retry, cancellation, and error tests.
4. Production dependency auditing at the high severity threshold.

The Admin dashboard job independently performs:

1. Deterministic dependency installation with `npm ci`.
2. Oxlint static analysis.
3. TypeScript compilation without output.
4. Vitest component tests in a browser-like environment.
5. A production Vite build.
6. Production dependency auditing at the high severity threshold.

The Backend API job uses committed NuGet lockfiles and performs:

1. A locked dependency restore with transitive vulnerability auditing.
2. Deterministic formatting verification.
3. A Release configuration build with warnings treated as errors.
4. A production API container image build.
5. Idempotent EF Core migration script generation.
6. ASP.NET Core API integration and security tests.
7. A transitive vulnerable dependency report.

The workflow has read-only repository permissions, cancels obsolete runs for the
same branch, and limits the job to 15 minutes.

Each application job retains a stable name so it can become a required
branch-protection check.

The deployable API image is validated but not published. Staging and production
promotion jobs will be added only after hosting environments are approved.
