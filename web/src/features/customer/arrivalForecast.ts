import { useQueries } from "@tanstack/react-query";
import {
    getParkingLotRecommendations,
    type RecommendedParkingLot,
} from "./parkingLotApi";

/** Hours shown by the arrival-time scrubber. */
export const FORECAST_HOURS = 8;

export type ArrivalHourSlot = {
    hour: number;
    /** Free-space probability per lot id at this hour, or null while it loads or fails. */
    percentByLot: Record<string, number> | null;
    /** Ranked recommendations at this hour, or null while it loads or fails. */
    lots: RecommendedParkingLot[] | null;
};

/** First scrubber hour so the selected hour sits near the start and the window stays inside the day. */
export function forecastWindowStart(selectedHour: number): number {
    const start = Math.max(0, selectedHour - 2);
    return Math.min(start, 24 - FORECAST_HOURS);
}

export function forecastHours(selectedHour: number): number[] {
    const start = forecastWindowStart(selectedHour);
    return Array.from({ length: FORECAST_HOURS }, (_, index) => start + index);
}

/** Local date ("yyyy-mm-dd", empty means today) plus an hour, as the ISO instant the AI service expects. */
export function arrivalAtIso(
    date: string,
    hour: number,
    now: Date = new Date(),
): string {
    const day = /^\d{4}-\d{2}-\d{2}$/.test(date)
        ? date
        : `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
    const local = new Date(`${day}T${String(hour).padStart(2, "0")}:00:00`);
    return local.toISOString();
}

/** Hour chosen by the "Parking time" input ("HH:MM"), or the next full hour when it is empty. */
export function initialArrivalHour(time: string, now: Date = new Date()) {
    const match = /^(\d{1,2}):\d{2}$/.exec(time);
    if (match) {
        const hour = Number(match[1]);
        if (hour >= 0 && hour <= 23) return hour;
    }
    return Math.min(23, now.getHours() + 1);
}

export function percentByLot(
    lots: RecommendedParkingLot[],
): Record<string, number> {
    return Object.fromEntries(
        lots
            .filter(
                (
                    lot,
                ): lot is RecommendedParkingLot & {
                    probabilityPercent: number;
                } =>
                    lot.probabilityPercent !== null &&
                    !isNaN(lot.probabilityPercent),
            )
            .map((lot) => [lot.lotId, Math.round(lot.probabilityPercent)]),
    );
}

type ForecastOptions = {
    latitude: number;
    longitude: number;
    radiusKm: number;
    vehicleType: "Car" | "Motorbike";
    date: string;
    /** Hour that positions the scrubber window; the window stays fixed while the user moves inside it. */
    anchorHour: number;
};

/** One recommendation call per scrubber hour (cached for a minute). The AI service answers one arrival time per call. */
export function useArrivalForecast(options: ForecastOptions | null) {
    const hours = forecastHours(options?.anchorHour ?? 0);
    const results = useQueries({
        queries: hours.map((hour) => ({
            queryKey: [
                "arrival-forecast",
                options?.latitude,
                options?.longitude,
                options?.radiusKm,
                options?.vehicleType,
                options?.date,
                hour,
            ],
            enabled: options !== null,
            staleTime: 60_000,
            retry: false,
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const result = await getParkingLotRecommendations(
                    options!.latitude,
                    options!.longitude,
                    {
                        vehicleType: options!.vehicleType,
                        radiusKm: options!.radiusKm,
                        arrivalAt: arrivalAtIso(options!.date, hour),
                        topK: 10,
                    },
                    signal,
                );
                return result.recommendedLots;
            },
        })),
    });

    const slots: ArrivalHourSlot[] = hours.map((hour, index) => {
        const lots = results[index]?.data ?? null;
        return {
            hour,
            lots,
            percentByLot: lots ? percentByLot(lots) : null,
        };
    });
    const loading = options !== null && results.some((r) => r.isPending);
    return { slots, loading };
}
