import { http, HttpResponse } from "msw";
import { MOCK_LOTS, MOCK_SLOTS } from "./seed";

export const handlers = [
    http.get("/api/owner/lots", () => {
        return HttpResponse.json(MOCK_LOTS);
    }),
    http.get("/api/owner/lots/:id", ({ params }) => {
        const lot = MOCK_LOTS.find((l) => String(l.id) === String(params.id));
        if (!lot) return new HttpResponse(null, { status: 404 });
        return HttpResponse.json(lot);
    }),
    http.get("/api/owner/lots/:id/slots", () => {
        // In a real scenario, this would filter by lotId.
        // For now, MOCK_SLOTS simulates one lot.
        return HttpResponse.json(MOCK_SLOTS);
    }),
    // Additional endpoints for reservations, sessions, etc. can be added here
];
