# ParkWise Jenkins CI migration plan — Playwright E2E gap

Date: 2026-10-12

## Required PR checks

The centrally managed `ParkWise-PR-CI` script is `pr-pipeline.groovy`. It runs only on
the unprivileged `parkwise-pr` agent: Git Policy, backend tool restore/build, unit tests,
architecture tests, web foundation tests, frontend ESLint, TypeScript, Prettier, Vitest,
production build, and Python AI compilation/unittest. A failure in any executed check
fails the Pipeline. Playwright E2E is temporarily outside this required PR check.

`Dockerfile.pr-agent` provides .NET 10.0.302, Node 24.16.0, and Python 3.12.15.
It does not contain Chromium or Playwright browser dependencies. The agent remains
non-root and has no Docker socket.

## Unresolved E2E testing gap

The most recent local Windows Chromium run used `npm run test:e2e -- --workers=1`:
25 passed and 9 failed out of 34 tests. This is not a Jenkins/Linux result.
The following failures remain unresolved; no Playwright tests were changed:

1. Forgot Password validates email and does not put it in the URL
2. Login validates locally and never sends credentials in the URL
3. Gateway conflict displays a safe registration error
4. Reset Password requires a link token and offers a new link
5. Reset Password validates fields and does not leak credentials to the URL
6. Sign Up validates fields and posts the documented registration contract
7. Email verification screen shows its pending-session state
8. OTP without a registration email cannot claim verification
9. OTP verify and resend use Identity responses without exposing the code in the URL

Treat E2E coverage as incomplete until those failures are investigated and a real
Linux Jenkins run is observed. Do not report a green PR Pipeline as proof of E2E success.

## Optional E2E job

`e2e-pipeline.groovy` is a separate centrally managed Pipeline for manual `Build Now`
or a future nightly trigger. Configure it as an SCM-bound or Multibranch job with a
centrally managed definition so `checkout scm` is available; discover `develop` only.
It accepts only a `develop` SCM checkout, uses `npm ci`
and runs the existing Chromium suite with one worker. It needs a dedicated
`parkwise-e2e` agent built from `Dockerfile.e2e-agent`. That image includes Node
24.16.0 and Playwright/Chromium 1.63.0. The agent should run as `jenkins`, with one
executor, without Docker socket or privileged mode. No new job, node, trigger, secret,
or container has been configured or started.

Before enabling a nightly trigger, validate the image and Pipeline manually, inspect
the nine failing tests, and decide whether the E2E job should publish an advisory or
required status. Keep the main PR checks mandatory throughout.
