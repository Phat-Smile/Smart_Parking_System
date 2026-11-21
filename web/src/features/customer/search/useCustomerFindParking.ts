"use client";

import {
    useEffect,
    useMemo,
    useRef,
    useState,
    type FormEvent,
    type KeyboardEvent,
} from "react";
import { initialArrivalHour, useArrivalForecast } from "../arrivalForecast";
import { useCustomerLanguage } from "../i18n/customerLanguage";
import { getPublicMapboxToken } from "@/shared/config/env";
import {
    getApprovedParkingLots,
    searchNearbyParkingLots,
    searchCustomerParkingLots,
    type CustomerLotSearchItem,
    type NearbyParkingLot,
    type ParkingLotSummary,
    type RecommendedParkingLot,
} from "../parkingLotApi";
import {
    geocodeDestination,
    retrieveDestination,
    suggestDestinations,
    type DestinationSuggestion,
    type MapCoordinates,
} from "../geocodeDestination";

export type SearchResult = CustomerLotSearchItem & {
    metricsState?: "loading" | "ready" | "error";
};

export type FilterState = {
    distance: string;
    evChargingOnly: boolean;
    sort: string;
};

export type InitialSearchOptions = {
    destination?: string;
    date?: string;
    time?: string;
    vehicle?: string;
};

const EMPTY_RESULTS: SearchResult[] = [];

export const initialFilters: FilterState = {
    distance: "any",
    evChargingOnly: false,
    sort: "recommended",
};

export function toSearchResults(lots: ParkingLotSummary[]): SearchResult[];
export function toSearchResults(lots: NearbyParkingLot[]): SearchResult[];
export function toSearchResults(
    lots: ParkingLotSummary[] | NearbyParkingLot[],
): SearchResult[] {
    return lots.map((lot) => ({
        ...lot,
        distanceKm: "distanceKm" in lot ? lot.distanceKm : null,
        drivingTimeMinutes: null,
        startingPrice: null,
        billingMode: null,
        availableSpaces: null,
        prebookable: false,
        compatibility: [],
        amenities: [],
        metricsState: "loading" as const,
    }));
}

export function withSearchMetrics(
    lots: SearchResult[],
    metrics: CustomerLotSearchItem[],
): SearchResult[] {
    const byId = new Map(metrics.map((lot) => [lot.id, lot]));
    return lots.map((lot) => {
        const detail = byId.get(lot.id);
        return detail
            ? {
                  ...lot,
                  startingPrice: detail.startingPrice,
                  availableSpaces: detail.availableSpaces,
                  billingMode: detail.billingMode,
                  prebookable: detail.prebookable,
                  compatibility: detail.compatibility,
                  amenities: detail.amenities,
                  drivingTimeMinutes: detail.drivingTimeMinutes,
                  metricsState: "ready",
              }
            : { ...lot, metricsState: "error" };
    });
}

export const filterOptions = {
    sort: [
        {
            value: "recommended",
            label: "Best match",
            icon: "sparkles" as const,
        },
        { value: "name", label: "Name A–Z", icon: "sort-az" as const },
        { value: "price", label: "Lowest price", icon: "price" as const },
        { value: "distance", label: "Nearest", icon: "distance" as const },
    ],
    vehicle: [
        { value: "any", label: "Any vehicle", icon: "filter" as const },
        { value: "car", label: "Car", icon: "car" as const },
        { value: "motorbike", label: "Motorbike", icon: "motorbike" as const },
    ],
};

export function useCustomerFindParking(
    initialSearch: InitialSearchOptions = {
        destination: "",
        date: "",
        time: "",
        vehicle: "any",
    },
) {
    const { copy, language } = useCustomerLanguage();
    const [destination, setDestination] = useState(
        initialSearch.destination ?? "",
    );
    const [debouncedSearchQuery, setDebouncedSearchQuery] = useState(
        initialSearch.destination ?? "",
    );

    useEffect(() => {
        const timer = setTimeout(() => {
            setDebouncedSearchQuery(destination);
        }, 300);
        return () => clearTimeout(timer);
    }, [destination]);

    const [parkingDate, setParkingDate] = useState(initialSearch.date ?? "");
    const [parkingTime, setParkingTime] = useState(initialSearch.time ?? "");

    // Hour on the arrival scrubber; the anchor fixes the scrubber window while the user moves inside it.
    const [arrivalHour, setArrivalHour] = useState(() =>
        initialArrivalHour(initialSearch.time ?? ""),
    );
    const [anchorHour] = useState(arrivalHour);
    const [vehicleType, setVehicleType] = useState(
        initialSearch.vehicle ?? "any",
    );
    const [selectedId, setSelectedId] = useState("parkside");
    // US-PRIV-001: an incoming search label is not a grant to access GPS.
    // Only the explicit "Use my current location" button may request location.
    const [searchMessage, setSearchMessage] = useState(
        initialSearch.destination === "Current location"
            ? "Choose Use my current location to share your location, or enter an address instead."
            : "",
    );
    const [locationBusy, setLocationBusy] = useState(false);
    const [searchBusy, setSearchBusy] = useState(false);

    const searchAbortRef = useRef<AbortController | null>(null);
    const suggestionAbortRef = useRef<AbortController | null>(null);
    const suggestionSessionRef = useRef<string | null>(null);
    const destinationInputRef = useRef<HTMLInputElement>(null);

    const [suggestions, setSuggestions] = useState<DestinationSuggestion[]>([]);
    const [suggestionsOpen, setSuggestionsOpen] = useState(false);
    const [suggestionsBusy, setSuggestionsBusy] = useState(false);
    const [suggestionsError, setSuggestionsError] = useState(false);
    const [activeSuggestionIndex, setActiveSuggestionIndex] = useState(-1);
    const [selectedSuggestion, setSelectedSuggestion] =
        useState<DestinationSuggestion | null>(null);
    const [destinationLocation, setDestinationLocation] =
        useState<MapCoordinates | null>(null);
    const [currentLocation, setCurrentLocation] = useState<{
        longitude: number;
        latitude: number;
    } | null>(null);

    const [approvedLots, setApprovedLots] = useState<CustomerLotSearchItem[]>(
        [],
    );
    const [catalogState, setCatalogState] = useState<
        "loading" | "ready" | "error"
    >("loading");

    useEffect(() => {
        const frame = requestAnimationFrame(() => {
            const now = new Date();
            const localDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
            const localTime = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;
            setParkingDate((current) => current || localDate);
            setParkingTime((current) => current || localTime);
        });
        return () => cancelAnimationFrame(frame);
    }, []);

    useEffect(
        () => () => {
            searchAbortRef.current?.abort();
            suggestionAbortRef.current?.abort();
        },
        [],
    );

    useEffect(() => {
        const query = destination.trim();
        const token = getPublicMapboxToken();
        if (
            !token ||
            query.length < 3 ||
            query === "Current location" ||
            selectedSuggestion?.name === query
        ) {
            return;
        }

        const controller = new AbortController();
        suggestionAbortRef.current = controller;
        const timer = window.setTimeout(async () => {
            const sessionToken =
                suggestionSessionRef.current ?? crypto.randomUUID();
            suggestionSessionRef.current = sessionToken;
            try {
                const matches = await suggestDestinations(
                    query,
                    token,
                    sessionToken,
                    controller.signal,
                );
                if (controller.signal.aborted) return;
                setSuggestions(matches);
                setActiveSuggestionIndex(-1);
                setSuggestionsOpen(
                    document.activeElement === destinationInputRef.current,
                );
            } catch {
                if (controller.signal.aborted) return;
                setSuggestions([]);
                setSuggestionsError(true);
            } finally {
                if (!controller.signal.aborted) setSuggestionsBusy(false);
            }
        }, 300);

        return () => {
            window.clearTimeout(timer);
            controller.abort();
        };
    }, [destination, selectedSuggestion]);

    const [filters, setFilters] = useState<FilterState>(initialFilters);
    const [distanceChoice, setDistanceChoice] = useState("any");
    const [distanceNeedsOrigin, setDistanceNeedsOrigin] = useState(false);
    const [customDistanceInput, setCustomDistanceInput] = useState("");
    const [customDistanceError, setCustomDistanceError] = useState("");

    const filterKey = JSON.stringify(filters);
    const searchOrigin = destinationLocation ?? currentLocation;
    const radiusKm = filters.distance === "any" ? 5 : Number(filters.distance);

    const [nearby, setNearby] = useState<{
        key: string;
        state: "loading" | "ready" | "error";
        lots: CustomerLotSearchItem[];
    } | null>(null);

    const originKey = searchOrigin
        ? `${searchOrigin.latitude},${searchOrigin.longitude},${radiusKm}`
        : "";
    const requestKey = `${originKey}:${vehicleType}:${filterKey}`;

    useEffect(() => {
        if (originKey || !parkingDate || !parkingTime) return;
        const controller = new AbortController();
        const selected = JSON.parse(filterKey) as typeof initialFilters;
        const needsSearchEndpoint = selected.evChargingOnly;
        queueMicrotask(() => setCatalogState("loading"));
        const vehicle =
            vehicleType === "any"
                ? undefined
                : vehicleType === "car"
                  ? "Car"
                  : "Motorbike";
        const arrivalAt =
            parkingDate && parkingTime
                ? new Date(`${parkingDate}T${parkingTime}`).toISOString()
                : undefined;
        const request = needsSearchEndpoint
            ? searchCustomerParkingLots(
                  {
                      vehicleType: vehicle,
                      amenities: selected.evChargingOnly ? ["ev-charging"] : [],
                      sort:
                          selected.sort === "distance"
                              ? "recommended"
                              : (selected.sort as
                                    "recommended" | "name" | "price"),
                  },
                  controller.signal,
              )
            : getApprovedParkingLots(
                  { vehicleType: vehicle, arrivalAt },
                  controller.signal,
              ).then(toSearchResults);
        void request
            .then((lots) => {
                if (controller.signal.aborted) return;
                setApprovedLots(lots);
                setCatalogState("ready");
                if (!needsSearchEndpoint && lots.length) {
                    void searchCustomerParkingLots(
                        { vehicleType: vehicle },
                        controller.signal,
                    )
                        .then((metrics) => {
                            if (!controller.signal.aborted)
                                setApprovedLots(
                                    withSearchMetrics(lots, metrics),
                                );
                        })
                        .catch(() => {
                            if (!controller.signal.aborted)
                                setApprovedLots(
                                    lots.map((lot) => ({
                                        ...lot,
                                        metricsState: "error",
                                    })),
                                );
                        });
                }
            })
            .catch(() => {
                if (!controller.signal.aborted) setCatalogState("error");
            });
        return () => controller.abort();
    }, [originKey, filterKey, vehicleType, parkingDate, parkingTime]);

    useEffect(() => {
        if (!originKey) return;
        const [latitude, longitude, radius] = originKey.split(",").map(Number);
        const selected = JSON.parse(filterKey) as typeof initialFilters;
        const controller = new AbortController();
        const needsSearchEndpoint =
            selected.evChargingOnly || vehicleType !== "any";
        void (async () => {
            setNearby({ key: requestKey, state: "loading", lots: [] });
            try {
                const lots = needsSearchEndpoint
                    ? await searchCustomerParkingLots(
                          {
                              latitude,
                              longitude,
                              radiusKm: radius,
                              vehicleType:
                                  vehicleType === "car"
                                      ? "Car"
                                      : vehicleType === "motorbike"
                                        ? "Motorbike"
                                        : undefined,
                              amenities: selected.evChargingOnly
                                  ? ["ev-charging"]
                                  : [],
                              sort: selected.sort as
                                  "recommended" | "name" | "price" | "distance",
                          },
                          controller.signal,
                      )
                    : toSearchResults(
                          await searchNearbyParkingLots(
                              latitude,
                              longitude,
                              radius,
                              controller.signal,
                          ),
                      );
                if (controller.signal.aborted) return;
                setNearby({ key: requestKey, state: "ready", lots });
                if (!needsSearchEndpoint && lots.length) {
                    void searchCustomerParkingLots(
                        { latitude, longitude, radiusKm: radius },
                        controller.signal,
                    )
                        .then((metrics) => {
                            if (!controller.signal.aborted)
                                setNearby({
                                    key: requestKey,
                                    state: "ready",
                                    lots: withSearchMetrics(lots, metrics),
                                });
                        })
                        .catch(() => {
                            if (!controller.signal.aborted)
                                setNearby({
                                    key: requestKey,
                                    state: "ready",
                                    lots: lots.map((lot) => ({
                                        ...lot,
                                        metricsState: "error",
                                    })),
                                });
                        });
                }
            } catch {
                if (!controller.signal.aborted)
                    setNearby({ key: requestKey, state: "error", lots: [] });
            }
        })();
        return () => controller.abort();
    }, [originKey, requestKey, vehicleType, filterKey]);

    const forecast = useArrivalForecast(
        searchOrigin
            ? {
                  latitude: searchOrigin.latitude,
                  longitude: searchOrigin.longitude,
                  radiusKm,
                  vehicleType:
                      vehicleType === "motorbike" ? "Motorbike" : "Car",
                  date: parkingDate,
                  anchorHour,
              }
            : null,
    );

    const slotAtArrival = forecast.slots.find(
        (slot) => slot.hour === arrivalHour,
    );
    const nearbyCurrent = nearby?.key === requestKey ? nearby : null;
    const resultsState: "loading" | "ready" | "error" = originKey
        ? (nearbyCurrent?.state ?? "loading")
        : catalogState;
    const results: SearchResult[] = originKey
        ? (nearbyCurrent?.lots ?? EMPTY_RESULTS)
        : approvedLots;

    const sortedParkings = useMemo(() => {
        let list = [...results];

        const term = debouncedSearchQuery.trim().toLowerCase();
        if (term && term !== "current location" && !originKey) {
            const matched = list.filter(
                (lot) =>
                    lot.name.toLowerCase().includes(term) ||
                    lot.address.toLowerCase().includes(term),
            );
            if (matched.length > 0) {
                list = matched;
            }
        }

        if (filters.sort === "name") {
            list.sort((a, b) => a.name.localeCompare(b.name, "vi"));
        } else if (filters.sort === "price") {
            list.sort((a, b) => {
                const priceA = a.startingPrice ?? Number.MAX_SAFE_INTEGER;
                const priceB = b.startingPrice ?? Number.MAX_SAFE_INTEGER;
                return priceA - priceB;
            });
        } else if (filters.sort === "distance" && originKey) {
            list.sort((a, b) => {
                const distA = a.distanceKm ?? Number.MAX_SAFE_INTEGER;
                const distB = b.distanceKm ?? Number.MAX_SAFE_INTEGER;
                return distA - distB;
            });
        }
        return list;
    }, [results, debouncedSearchQuery, originKey, filters.sort]);

    const visibleParkings = sortedParkings;

    // Only recommendations for lots that survive the current filters, ranked at the chosen arrival hour.
    const allRecommendations = useMemo(() => {
        if (!slotAtArrival?.lots) return null;
        return slotAtArrival.lots.filter((rec) =>
            visibleParkings.some((lot) => lot.id === rec.lotId),
        );
    }, [slotAtArrival, visibleParkings]);

    const currentRecommendations: RecommendedParkingLot[] | null =
        useMemo(() => {
            return allRecommendations?.slice(0, 3) ?? null;
        }, [allRecommendations]);

    const activeSelectedId = useMemo(() => {
        return visibleParkings.some((parking) => parking.id === selectedId)
            ? selectedId
            : (visibleParkings[0]?.id ?? "");
    }, [visibleParkings, selectedId]);

    function useCurrentLocation() {
        searchAbortRef.current?.abort();
        suggestionAbortRef.current?.abort();
        suggestionSessionRef.current = null;
        setSuggestionsOpen(false);
        setSuggestionsBusy(false);
        setSuggestions([]);
        setSelectedSuggestion(null);
        setSearchBusy(false);
        setDestinationLocation(null);
        if (!navigator.geolocation) {
            setSearchMessage(
                "Location is not supported by this browser. Enter an address instead.",
            );
            return;
        }
        setLocationBusy(true);
        setSearchMessage("Requesting your current location…");
        navigator.geolocation.getCurrentPosition(
            ({ coords }) => {
                setCurrentLocation({
                    longitude: coords.longitude,
                    latitude: coords.latitude,
                });
                setDestination("Current location");

                setSearchMessage(
                    getPublicMapboxToken()
                        ? "Current location selected and shown on the map."
                        : "Current location selected. The map will center here when Mapbox is configured.",
                );
                setLocationBusy(false);
            },
            (error) => {
                setSearchMessage(
                    error.code === 1
                        ? "Location access was declined. Enter an address instead."
                        : "Your location could not be found. Enter an address instead.",
                );
                setLocationBusy(false);
            },
            { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 },
        );
    }

    async function selectSuggestion(suggestion: DestinationSuggestion) {
        const token = getPublicMapboxToken();
        const sessionToken = suggestionSessionRef.current;
        if (!token || !sessionToken) return;

        suggestionAbortRef.current?.abort();
        searchAbortRef.current?.abort();
        setDestination(suggestion.name);
        setSelectedSuggestion(suggestion);
        setSuggestionsOpen(false);
        setSuggestionsBusy(false);
        setSuggestions([]);
        setActiveSuggestionIndex(-1);
        setSearchBusy(true);
        setSearchMessage("Locating destination on Mapbox…");
        destinationInputRef.current?.focus();

        const controller = new AbortController();
        searchAbortRef.current = controller;
        try {
            const coordinates = await retrieveDestination(
                suggestion.id,
                token,
                sessionToken,
                controller.signal,
            );
            if (controller.signal.aborted) return;
            if (!coordinates) {
                setSearchMessage("Destination not found. Try another place.");
                return;
            }
            setCurrentLocation(null);
            setDestinationLocation(coordinates);

            setSearchMessage(
                `Showing approved parking lots near “${suggestion.name}”.`,
            );
        } catch {
            if (!controller.signal.aborted) {
                setSearchMessage(
                    "Mapbox destination search is unavailable. Try again later.",
                );
            }
        } finally {
            if (searchAbortRef.current === controller) {
                searchAbortRef.current = null;
                setSearchBusy(false);
            }
            if (suggestionSessionRef.current === sessionToken) {
                suggestionSessionRef.current = null;
            }
        }
    }

    function handleDestinationKeyDown(event: KeyboardEvent<HTMLInputElement>) {
        if (event.key === "Escape") {
            setSuggestionsOpen(false);
            return;
        }
        if (!suggestionsOpen || suggestions.length === 0) return;

        if (event.key === "ArrowDown") {
            event.preventDefault();
            setActiveSuggestionIndex(
                (index) => (index + 1) % suggestions.length,
            );
        } else if (event.key === "ArrowUp") {
            event.preventDefault();
            setActiveSuggestionIndex(
                (index) => (index <= 0 ? suggestions.length : index) - 1,
            );
        } else if (event.key === "Enter" && activeSuggestionIndex >= 0) {
            event.preventDefault();
            void selectSuggestion(suggestions[activeSuggestionIndex]);
        }
    }

    function handleDestinationInputChange(nextDestination: string) {
        const canSuggest =
            !!getPublicMapboxToken() &&
            nextDestination.trim().length >= 3 &&
            nextDestination.trim() !== "Current location";
        searchAbortRef.current?.abort();
        suggestionAbortRef.current?.abort();
        setSearchBusy(false);
        setDestination(nextDestination);
        setCurrentLocation(null);
        setDestinationLocation(null);
        setSelectedSuggestion(null);
        setSuggestions([]);
        setSuggestionsOpen(canSuggest);
        setSuggestionsBusy(canSuggest);
        setSuggestionsError(false);
        setActiveSuggestionIndex(-1);
        if (!canSuggest) {
            suggestionSessionRef.current = null;
        }
        setSearchMessage("");
    }

    async function handleSearch(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        searchAbortRef.current?.abort();
        suggestionAbortRef.current?.abort();
        suggestionSessionRef.current = null;
        setSuggestionsOpen(false);
        setSuggestionsBusy(false);
        const query = destination.trim();
        const token = getPublicMapboxToken();

        if (selectedSuggestion?.name === query && destinationLocation) {
            setSearchBusy(false);
            setSearchMessage(`Showing approved parking lots near “${query}”.`);
            return;
        }

        if (!query || query === "Current location" || !token) {
            setSearchBusy(false);
            setDestinationLocation(null);

            setSearchMessage(
                query
                    ? "Destination search needs the Mapbox token. Showing all approved parking lots."
                    : "Showing all approved parking lots.",
            );
            return;
        }

        const controller = new AbortController();
        searchAbortRef.current = controller;
        setSearchBusy(true);
        setDestinationLocation(null);

        setSearchMessage("Finding destination on Mapbox…");
        try {
            const coordinates = await geocodeDestination(
                query,
                token,
                controller.signal,
            );
            if (controller.signal.aborted) return;
            if (!coordinates) {
                setSearchMessage("Destination not found. Try another address.");
                return;
            }
            setCurrentLocation(null);
            setDestinationLocation(coordinates);

            setSearchMessage(`Showing approved parking lots near “${query}”.`);
        } catch {
            if (!controller.signal.aborted) {
                setSearchMessage(
                    "Mapbox destination search is unavailable. Try again later.",
                );
            }
        } finally {
            if (searchAbortRef.current === controller) {
                searchAbortRef.current = null;
                setSearchBusy(false);
            }
        }
    }

    const distanceLabels: Record<string, string> = {
        "1": copy("Within 1 km", "Trong 1 km"),
        "2": copy("Within 2 km", "Trong 2 km"),
        "5": copy("Within 5 km", "Trong 5 km"),
        "10": copy("Within 10 km", "Trong 10 km"),
        "20": copy("Within 20 km", "Trong 20 km"),
    };

    function handleDistanceChoiceChange(value: string) {
        setSearchMessage("");
        setDistanceNeedsOrigin(false);
        setCustomDistanceError("");
        setDistanceChoice(value);
        if (value === "any") {
            clearDistanceFilter();
        } else if (value === "custom") {
            if (
                filters.distance !== "any" &&
                !distanceLabels[filters.distance]
            ) {
                setCustomDistanceInput(filters.distance);
            }
        } else if (!searchOrigin) {
            setDistanceChoice("any");
            setDistanceNeedsOrigin(true);
        } else {
            setFilters((current) => ({
                ...current,
                distance: value,
            }));
        }
    }

    function clearDistanceFilter() {
        setFilters((current) => ({ ...current, distance: "any" }));
        setDistanceChoice("any");
        setCustomDistanceInput("");
        setCustomDistanceError("");
    }

    function applyCustomDistance() {
        const distanceKm = Number(customDistanceInput);
        if (
            !Number.isFinite(distanceKm) ||
            distanceKm <= 0 ||
            distanceKm > 50
        ) {
            setCustomDistanceError(
                copy(
                    "Enter a distance greater than 0 and no more than 50 km.",
                    "Nhập khoảng cách lớn hơn 0 và không quá 50 km.",
                ),
            );
            return;
        }
        if (!searchOrigin) {
            setCustomDistanceError(
                copy(
                    "Choose a destination or use your current location first.",
                    "Hãy chọn điểm đến hoặc dùng vị trí hiện tại trước.",
                ),
            );
            return;
        }
        setFilters((current) => ({
            ...current,
            distance: String(distanceKm),
        }));
        setDistanceChoice("custom");
        setCustomDistanceError("");
    }

    return {
        copy,
        language,
        destination,
        setDestination,
        debouncedSearchQuery,
        parkingDate,
        setParkingDate,
        parkingTime,
        setParkingTime,
        arrivalHour,
        setArrivalHour,
        anchorHour,
        vehicleType,
        setVehicleType,
        selectedId,
        setSelectedId,
        activeSelectedId,
        searchMessage,
        setSearchMessage,
        locationBusy,
        searchBusy,
        destinationInputRef,
        suggestions,
        suggestionsOpen,
        setSuggestionsOpen,
        suggestionsBusy,
        suggestionsError,
        activeSuggestionIndex,
        setActiveSuggestionIndex,
        selectedSuggestion,
        destinationLocation,
        currentLocation,
        approvedLots,
        catalogState,
        filters,
        setFilters,
        distanceChoice,
        setDistanceChoice,
        distanceNeedsOrigin,
        setDistanceNeedsOrigin,
        customDistanceInput,
        setCustomDistanceInput,
        customDistanceError,
        setCustomDistanceError,
        searchOrigin,
        radiusKm,
        originKey,
        requestKey,
        forecast,
        slotAtArrival,
        resultsState,
        results,
        sortedParkings,
        visibleParkings,
        allRecommendations,
        currentRecommendations,
        useCurrentLocation,
        selectSuggestion,
        handleDestinationKeyDown,
        handleDestinationInputChange,
        handleDistanceChoiceChange,
        handleSearch,
        clearDistanceFilter,
        applyCustomDistance,
        distanceLabels,
    };
}
