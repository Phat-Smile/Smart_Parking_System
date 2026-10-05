# Smart Parking Web

One Next.js App Router application with React, strict TypeScript, Tailwind CSS and semantic CSS tokens. Technical dependencies are TanStack Query, React Hook Form, Zod, Three.js / React Three Fiber / Drei, Mapbox GL / react-map-gl, and SignalR. There are no Smart Parking business screens or live integrations yet.

## Requirements and commands

Node 24.16.0 (see `.nvmrc`) and npm 11.13.0.

```powershell
cd web
npm ci
npm run dev
```

Open `http://localhost:3000`. Run `npm run lint`, `npm run typecheck`, `npm run test:run`, `npm run build`, or `npm run format:check` as needed. `npm run test` watches unit tests. E2E smoke uses `npx playwright install chromium` once, then `npm run test:e2e`; it starts Next locally and needs no backend APIs.

## Environment and backend

Copy `.env.example` to a local ignored `.env.local` if overriding defaults. `NEXT_PUBLIC_API_BASE_URL=http://localhost:5000` is the browser-visible YARP Gateway URL. Never use Docker-only `gateway:8080` in the browser, or call service ports 5001–5005 / AI 8000 directly. Gateway CORS allows `http://localhost:3000` in local Compose by default.

`NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN` is optional for the foundation build. A future map feature can use a public browser token restricted by Mapbox; do not copy a private/backend credential into it. All `NEXT_PUBLIC_*` values are visible to users.

## Technical boundaries

`src/shared/api/` centralizes fetch and safe ProblemDetails handling. `src/app/providers.tsx` scopes the TanStack Query client. `src/shared/three/` and `src/shared/map/` hold optional client-only rendering primitives with no parking data. `src/shared/realtime/` creates disconnected SignalR connections with automatic reconnect configuration; a future feature supplies its hub path, handlers and lifecycle. Business UI belongs in `src/features/<feature-name>/`. Authentication token storage and refresh are not chosen yet.

## Docker

From the repository root: `docker compose build web`, `docker compose up -d web`, then browse `http://localhost:3000`. The multi-stage image uses `npm ci` and Next standalone output. No real Mapbox token or backend secret is needed to build it.
