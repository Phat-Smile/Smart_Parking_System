# Smart Parking AI Owner Portal

This directory contains the Owner Portal for the **Smart Parking AI System**.

## 🚀 Setup & Scripts

To run the owner portal locally, you need Node.js >= 24 (or 18+ depending on the Next.js setup).

```bash
# Install dependencies
npm install

# Run development server (with mocks enabled by default via .env.local)
npm run dev

# Build for production
npm run build

# Typecheck and Lint
npm run typecheck
npm run lint
```

## 🏗 Architecture

The portal is built with **Next.js 14+ (App Router)** and **React 19**.
It follows a clean feature-driven architecture:

- **`src/app/(owner)/owner/*`**: Route definitions for the portal.
- **`src/components/viz/*`**: 2D and 3D visualization components built with `react-three-fiber` and `three`.
- **`src/domain/*`**: Shared TypeScript types and Zod schemas that represent the core domain model (ParkingLot, Slot, Reservation, etc.).
- **`src/lib/api/*`**: Data fetching layer powered by `TanStack Query` (`@tanstack/react-query`).
- **`src/store/*`**: Client state management powered by `Zustand` (acting as the single source of truth for the 3D and 2D views).
- **`src/mocks/*`**: API mocking using `msw` (Mock Service Worker).

## 🔌 API Contract & Mocking

The frontend expects a REST API with the following basic contract (see `src/domain/types.ts` for full schema):

- `GET /api/owner/lots` - Returns `ParkingLot[]`
- `GET /api/owner/lots/:id` - Returns a `ParkingLot`
- `GET /api/owner/lots/:id/slots` - Returns `Slot[]`
- `POST /api/owner/lots` - Creates a new `ParkingLot` (from the Wizard form)

### Swapping Mocks for the Real Backend

To disable the mocks and connect to a real backend, modify your `.env.local` file:

```env
NEXT_PUBLIC_USE_MOCKS=false
```

When `NEXT_PUBLIC_USE_MOCKS` is false, `MSWProvider` will not intercept requests, and the `fetchApi` function in `src/lib/api/owner.ts` will directly hit your real backend endpoints (make sure to set the correct `BASE_URL` or proxy).

## 🚗 3D Visualization (Procedural vs GLB)

The 3D scene (`src/components/viz/ParkingScene.tsx`) currently uses procedural primitives to render the structure and the cars (`CarFactory.tsx`). This ensures great performance and avoids managing large binary files during early development.

### How to swap procedural 3D models for GLB assets:

1. Obtain `.glb` or `.gltf` files for the cars and the parking structure. Place them in the `public/models` directory.
2. In `src/components/viz/CarFactory.tsx`, import `useGLTF` from `@react-three/drei`:
   ```tsx
   import { useGLTF } from "@react-three/drei";

   export function CarFactory({ type, position, rotation, color }) {
     // Swap out the switch statement for model loading
     const { scene } = useGLTF(`/models/car-${type}.glb`);
     const clonedScene = useMemo(() => scene.clone(), [scene]);

     // Optionally traverse the scene to apply the dynamic `color` to the car body mesh

     return (
       <primitive
         object={clonedScene}
         position={position}
         rotation={rotation}
       />
     );
   }
   ```
3. Do the same for the parking structure inside `src/components/viz/Structure.tsx`. Ensure the bounding boxes and scale of the GLB models match the expected meters (e.g., 4.7m for a sedan).

## 🎨 Design System

The app utilizes a custom **Light Premium SaaS** design system enforced via Tailwind CSS v4 variables in `src/app/globals.css` and `src/styles/tokens.css`.
The primary color is a vibrant lime (`#B8F34A`) paired with a dark forest green (`#19301D`) for strong, accessible contrast.
