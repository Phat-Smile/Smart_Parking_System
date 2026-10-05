# Frontend architecture

The repository has one Next.js App Router application in `web/`. Its stack is React, strict TypeScript, Tailwind CSS, CSS variable tokens, TanStack Query, React Hook Form, Zod and the resolver package. Vitest with Testing Library covers technical units; Playwright checks the shell and 404 route. Node 24.16.0 and npm 11.13.0 are pinned in `web/.nvmrc` and `web/package.json`.

## Browser and backend boundary

Browser traffic goes to the YARP Gateway at `NEXT_PUBLIC_API_BASE_URL` (local default `http://localhost:5000`). The shared native fetch wrapper lives in `src/shared/api/`. Browser code must not call individual service ports, the internal AI host, or databases. The Gateway and service APIs currently expose technical foundation endpoints; business endpoints are not implemented.

The wrapper accepts root-relative paths, preserves the response `X-Correlation-ID`, parses ProblemDetails fields including `traceId` and `correlationId`, and uses a safe title for the user-facing error message. These IDs are for support, not business identity. Public API contracts should be generated or mapped from provider-owned OpenAPI when business routes exist; do not share .NET Domain classes with browser code.

## Server and client components

App Router pages and layouts remain Server Components by default. `src/app/providers.tsx` is the narrow client boundary for TanStack Query. Use `"use client"` only for interactive UI, forms, browser state, Mapbox, WebGL, and realtime lifecycle. The technical Mapbox and React Three Fiber scenes are dynamically loaded with SSR disabled, and neither is mounted by the application shell. Future slices own business data, interaction and visualizations.

## Feature ownership

Future business code belongs under `src/features/<feature-name>/`: auth, profile, vehicles, parking, map, parking-3d, booking, parking-session, payment, and notifications. Do not create empty feature directories ahead of a slice. Shared technical primitives belong under `src/shared/`: Mapbox (`mapbox-gl`, `react-map-gl`), 3D (`three`, `@react-three/fiber`, `@react-three/drei`), and SignalR (`@microsoft/signalr`). The SignalR factory requires a future slice to supply a hub path and optional token factory, configures automatic reconnect, and never starts a connection on its own. No hub route or business event is assumed.

## Environment and security

`NEXT_PUBLIC_*` values are embedded in browser code. `web/.env.example` contains only the Gateway URL and an empty public Mapbox token placeholder. A Mapbox browser token must be restricted appropriately; never copy a server-side/private token from the backend configuration. Never place database, RabbitMQ, JWT signing, Google client secret, SMTP, payment or other private credentials in frontend environment or source. JWT storage, refresh and route protection are owned by VS01. Compose allows only `http://localhost:3000` by default for local Gateway CORS; production origins remain configurable.
