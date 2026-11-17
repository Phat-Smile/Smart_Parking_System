"use client";

import {
    useEffect,
    useMemo,
    useRef,
    useState,
    type FormEvent,
    type KeyboardEvent,
} from "react";
import Image from "next/image";
import Link from "next/link";
import { CustomerMap } from "./CustomerMap";
import { CustomerFooter, CustomerHeader, CustomerIcon } from "./CustomerShell";
import { AiRecommendationPanel } from "./components/AiRecommendationPanel";
import { ArrivalTimeScrubber } from "./components/ArrivalTimeScrubber";
import { initialArrivalHour, useArrivalForecast } from "./arrivalForecast";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { CustomerSearchDropdown } from "./CustomerSearchDropdown";
import { getPublicMapboxToken } from "@/shared/config/env";
import {
    getApprovedParkingLots,
    searchNearbyParkingLots,
    searchCustomerParkingLots,
    type CustomerLotSearchItem,
    type NearbyParkingLot,
    type ParkingLotSummary,
} from "./parkingLotApi";

// Catalog/nearby responses don't include price or live availability.
type SearchResult = CustomerLotSearchItem & {
    metricsState?: "loading" | "ready" | "error";
};
import {
    geocodeDestination,
    retrieveDestination,
    suggestDestinations,
    type DestinationSuggestion,
    type MapCoordinates,
} from "./geocodeDestination";

const EMPTY_RESULTS: SearchResult[] = [];

const MAX_PRICE = 50000;

const initialFilters = {
    maxPrice: MAX_PRICE,
    distance: "any",
    evChargingOnly: false,
    sort: "recommended",
};

function toSearchResults(lots: ParkingLotSummary[]): SearchResult[];
function toSearchResults(lots: NearbyParkingLot[]): SearchResult[];
function toSearchResults(
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

function withSearchMetrics(
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

const filterOptions = {
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

export function CustomerFindParkingPage({
    fontClassName,
    initialSearch = { destination: "", date: "", time: "", vehicle: "any" },
}: {
    fontClassName: string;
    initialSearch?: {
        destination: string;
        date: string;
        time: string;
        vehicle: string;
    };
}) {
    const { copy, language } = useCustomerLanguage();
    const [destination, setDestination] = useState(initialSearch.destination);
    const [debouncedSearchQuery, setDebouncedSearchQuery] = useState(
        initialSearch.destination,
    );

    useEffect(() => {
        const timer = setTimeout(() => {
            setDebouncedSearchQuery(destination);
        }, 300);
        return () => clearTimeout(timer);
    }, [destination]);
    const [parkingDate, setParkingDate] = useState(initialSearch.date);
    const [parkingTime, setParkingTime] = useState(initialSearch.time);
    // Hour on the arrival scrubber; the anchor fixes the scrubber window while the user moves inside it.
    const [arrivalHour, setArrivalHour] = useState(() =>
        initialArrivalHour(initialSearch.time),
    );
    const [anchorHour, setAnchorHour] = useState(arrivalHour);
    const [vehicleType, setVehicleType] = useState(initialSearch.vehicle);
    const [selectedId, setSelectedId] = useState("parkside");
    const [filtersOpen, setFiltersOpen] = useState(true);
    const [searchMessage, setSearchMessage] = useState("");
    const [submittedDestination, setSubmittedDestination] = useState("");
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
    useEffect(() => {
        if (
            initialSearch.destination !== "Current location" ||
            !navigator.geolocation
        )
            return;
        navigator.geolocation.getCurrentPosition(
            ({ coords }) => {
                setCurrentLocation({
                    longitude: coords.longitude,
                    latitude: coords.latitude,
                });
            },
            () => {
                setSearchMessage(
                    "Your location could not be restored. Choose Use my current location to try again.",
                );
            },
            { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 },
        );
    }, [initialSearch.destination]);
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
    const [filters, setFilters] = useState(initialFilters);
    const [distanceChoice, setDistanceChoice] = useState("any");
    const [customDistanceInput, setCustomDistanceInput] = useState("");
    const [customDistanceError, setCustomDistanceError] = useState("");
    const filterKey = JSON.stringify(filters);
    const activeFilterCount =
        Number(filters.maxPrice < MAX_PRICE) +
        Number(filters.distance !== "any") +
        Number(filters.evChargingOnly);
    const hasActiveFilters =
        activeFilterCount > 0 || filters.sort !== initialFilters.sort;
    const canClearFilters =
        hasActiveFilters ||
        Boolean(destination.trim() || submittedDestination) ||
        vehicleType !== "any";
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
        const needsSearchEndpoint =
            selected.maxPrice < MAX_PRICE || selected.evChargingOnly;
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
                      maxPrice:
                          selected.maxPrice < MAX_PRICE
                              ? selected.maxPrice
                              : undefined,
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
            selected.maxPrice < MAX_PRICE ||
            selected.evChargingOnly ||
            vehicleType !== "any";
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
                              maxPrice:
                                  selected.maxPrice < MAX_PRICE
                                      ? selected.maxPrice
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

    const currentRecommendations = useMemo(() => {
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
                setSubmittedDestination("Current location");
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
            setSubmittedDestination("");
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
            setSubmittedDestination(query);
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
        setSubmittedDestination("");
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
            setSubmittedDestination("");
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

    function clearFilters() {
        searchAbortRef.current?.abort();
        suggestionAbortRef.current?.abort();
        suggestionSessionRef.current = null;
        setSuggestions([]);
        setSuggestionsOpen(false);
        setSuggestionsBusy(false);
        setSuggestionsError(false);
        setActiveSuggestionIndex(-1);
        setSelectedSuggestion(null);
        setSearchBusy(false);
        setDestinationLocation(null);
        setFilters(initialFilters);
        setDistanceChoice("any");
        setCustomDistanceInput("");
        setCustomDistanceError("");
        setSubmittedDestination("");
        setDestination("");
        setVehicleType("any");
        setSearchMessage("");
        setCurrentLocation(null);
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

    const distanceLabels: Record<string, string> = {
        "1": copy("Within 1 km", "Trong 1 km"),
        "2": copy("Within 2 km", "Trong 2 km"),
        "5": copy("Within 5 km", "Trong 5 km"),
        "10": copy("Within 10 km", "Trong 10 km"),
        "20": copy("Within 20 km", "Trong 20 km"),
    };
    return (
        <div
            lang={language}
            className={`parkwise-page customer-shell-page customer-search-page ${fontClassName}`}
        >
            <CustomerHeader active="search" />
            <main className="customer-shell-container customer-search-main">
                <div className="customer-search-intro">
                    <p className="customer-search-kicker">
                        {copy("FIND YOUR SPACE", "TÌM CHỖ ĐỖ")}
                    </p>
                    <h1>
                        {copy(
                            "Find parking near your destination.",
                            "Tìm bãi đỗ gần điểm đến của bạn.",
                        )}
                    </h1>
                    <p>
                        {copy(
                            "Compare locations and plan your trip with a clearer view of the city.",
                            "So sánh các địa điểm và lên kế hoạch di chuyển thuận tiện hơn.",
                        )}
                    </p>
                </div>
                <form className="customer-search-form" onSubmit={handleSearch}>
                    <div
                        className="customer-search-field customer-search-destination"
                        onBlur={(event) => {
                            if (
                                !event.currentTarget.contains(
                                    event.relatedTarget as Node | null,
                                )
                            ) {
                                setSuggestionsOpen(false);
                            }
                        }}
                    >
                        <CustomerIcon name="location" size={21} />
                        <span>
                            <label htmlFor="customer-search-destination-input">
                                {copy("Destination", "Điểm đến")}
                            </label>
                            <input
                                ref={destinationInputRef}
                                id="customer-search-destination-input"
                                aria-label={copy("Destination", "Điểm đến")}
                                role="combobox"
                                aria-autocomplete="list"
                                aria-expanded={suggestionsOpen}
                                aria-controls={
                                    suggestionsOpen
                                        ? "customer-destination-suggestions"
                                        : undefined
                                }
                                aria-activedescendant={
                                    suggestionsOpen &&
                                    activeSuggestionIndex >= 0
                                        ? `customer-destination-suggestion-${activeSuggestionIndex}`
                                        : undefined
                                }
                                value={destination}
                                onChange={(event) => {
                                    const nextDestination = event.target.value;
                                    const canSuggest =
                                        !!getPublicMapboxToken() &&
                                        nextDestination.trim().length >= 3 &&
                                        nextDestination.trim() !==
                                            "Current location";
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
                                }}
                                onFocus={() => {
                                    if (
                                        suggestions.length > 0 ||
                                        suggestionsBusy ||
                                        suggestionsError
                                    ) {
                                        setSuggestionsOpen(true);
                                    }
                                }}
                                onKeyDown={handleDestinationKeyDown}
                                maxLength={256}
                                placeholder={copy(
                                    "Enter an address or landmark",
                                    "Nhập địa chỉ hoặc địa điểm",
                                )}
                                autoComplete="off"
                            />
                        </span>
                        <button
                            type="button"
                            className="customer-search-locate"
                            onClick={useCurrentLocation}
                            disabled={locationBusy}
                            aria-label={copy(
                                "Use my current location",
                                "Dùng vị trí hiện tại",
                            )}
                            title={copy(
                                "Use my current location",
                                "Dùng vị trí hiện tại",
                            )}
                        >
                            <CustomerIcon name="location" size={19} />
                        </button>
                        {suggestionsOpen && (
                            <div
                                id="customer-destination-suggestions"
                                className="customer-search-suggestions"
                                role="listbox"
                                aria-label={copy(
                                    "Destination suggestions",
                                    "Gợi ý điểm đến",
                                )}
                            >
                                {suggestionsBusy ? (
                                    <p
                                        className="customer-search-suggestions-state"
                                        role="status"
                                    >
                                        {copy(
                                            "Searching places…",
                                            "Đang tìm địa điểm…",
                                        )}
                                    </p>
                                ) : suggestionsError ? (
                                    <p
                                        className="customer-search-suggestions-state"
                                        role="status"
                                    >
                                        {copy(
                                            "Places are unavailable. Try again shortly.",
                                            "Không thể tải địa điểm. Vui lòng thử lại sau.",
                                        )}
                                    </p>
                                ) : suggestions.length === 0 ? (
                                    <p
                                        className="customer-search-suggestions-state"
                                        role="status"
                                    >
                                        {copy(
                                            "No matching places found.",
                                            "Không tìm thấy địa điểm phù hợp.",
                                        )}
                                    </p>
                                ) : (
                                    suggestions.map((suggestion, index) => (
                                        <button
                                            key={suggestion.id}
                                            id={`customer-destination-suggestion-${index}`}
                                            type="button"
                                            role="option"
                                            aria-selected={
                                                activeSuggestionIndex === index
                                            }
                                            className={`customer-search-suggestion ${activeSuggestionIndex === index ? "active" : ""}`}
                                            onMouseEnter={() =>
                                                setActiveSuggestionIndex(index)
                                            }
                                            onMouseDown={(event) =>
                                                event.preventDefault()
                                            }
                                            onClick={() =>
                                                void selectSuggestion(
                                                    suggestion,
                                                )
                                            }
                                        >
                                            <CustomerIcon
                                                name="location"
                                                size={17}
                                            />
                                            <span>
                                                <strong>
                                                    {suggestion.name}
                                                </strong>
                                                {suggestion.place && (
                                                    <small>
                                                        {suggestion.place}
                                                    </small>
                                                )}
                                            </span>
                                        </button>
                                    ))
                                )}
                                <span className="customer-search-suggestions-credit">
                                    {copy(
                                        "Search by Mapbox",
                                        "Tìm kiếm bằng Mapbox",
                                    )}
                                </span>
                            </div>
                        )}
                    </div>
                    <label className="customer-search-field">
                        <CustomerIcon name="calendar" size={21} />
                        <span>
                            <strong>{copy("Date", "Ngày")}</strong>
                            <input
                                aria-label={copy("Parking date", "Ngày đỗ xe")}
                                type="date"
                                value={parkingDate}
                                onChange={(event) =>
                                    setParkingDate(event.target.value)
                                }
                            />
                        </span>
                    </label>
                    <label className="customer-search-field">
                        <CustomerIcon name="clock" size={21} />
                        <span>
                            <strong>{copy("Time", "Giờ")}</strong>
                            <input
                                aria-label={copy("Parking time", "Giờ đỗ xe")}
                                type="time"
                                value={parkingTime}
                                onChange={(event) => {
                                    setParkingTime(event.target.value);
                                    const hour = initialArrivalHour(
                                        event.target.value,
                                    );
                                    setArrivalHour(hour);
                                    setAnchorHour(hour);
                                }}
                            />
                        </span>
                    </label>
                    <div className="customer-search-field customer-search-vehicle-field">
                        <CustomerIcon name="car" size={22} />
                        <CustomerSearchDropdown
                            label={copy("Vehicle type", "Loại xe")}
                            value={vehicleType}
                            options={filterOptions.vehicle.map((option) => ({
                                ...option,
                                label:
                                    option.value === "any"
                                        ? copy("Any vehicle", "Mọi loại xe")
                                        : option.value === "car"
                                          ? copy("Car", "Ô tô")
                                          : copy("Motorbike", "Xe máy"),
                            }))}
                            onChange={setVehicleType}
                            variant="field"
                            align="right"
                        />
                    </div>
                    <button
                        className="customer-shell-primary customer-search-submit"
                        type="submit"
                        disabled={searchBusy}
                    >
                        <CustomerIcon name="search" size={21} />
                        <span>
                            {searchBusy
                                ? copy(
                                      "Searching map…",
                                      "Đang tìm trên bản đồ…",
                                  )
                                : copy("Find Parking", "Tìm bãi đỗ")}
                        </span>
                        <CustomerIcon name="arrow" size={19} />
                    </button>
                </form>
                {searchMessage && (
                    <p className="customer-search-feedback" role="status">
                        {searchMessage}
                    </p>
                )}

                <div className="customer-search-toolbar">
                    <div className="customer-search-toolbar-header">
                        <div className="customer-search-filter-summary">
                            <button
                                type="button"
                                className={`customer-search-filter-button ${filtersOpen ? "active" : ""}`}
                                onClick={() => setFiltersOpen((open) => !open)}
                                aria-expanded={filtersOpen}
                                aria-controls="customer-search-filters"
                            >
                                <CustomerIcon name="filter" size={17} />
                                <span>
                                    {copy("All Filters", "Bộ lọc")}
                                    {activeFilterCount > 0
                                        ? ` (${activeFilterCount})`
                                        : ""}
                                </span>
                                <CustomerIcon name="chevron" size={15} />
                            </button>
                            {filters.maxPrice < MAX_PRICE && (
                                <button
                                    type="button"
                                    className="customer-search-filter-chip"
                                    onClick={() =>
                                        setFilters((current) => ({
                                            ...current,
                                            maxPrice: MAX_PRICE,
                                        }))
                                    }
                                    aria-label={copy(
                                        "Remove price filter",
                                        "Bỏ lọc giá",
                                    )}
                                >
                                    {copy("Price: Under", "Giá: Dưới")}{" "}
                                    {filters.maxPrice.toLocaleString("vi-VN")}₫{" "}
                                    <span aria-hidden="true">×</span>
                                </button>
                            )}
                            {filters.distance !== "any" && (
                                <button
                                    type="button"
                                    className="customer-search-filter-chip"
                                    onClick={() => clearDistanceFilter()}
                                    aria-label={copy(
                                        "Remove distance filter",
                                        "Bỏ lọc khoảng cách",
                                    )}
                                >
                                    {copy("Distance:", "Khoảng cách:")}{" "}
                                    {distanceLabels[filters.distance] ??
                                        `${filters.distance} km`}{" "}
                                    <span aria-hidden="true">×</span>
                                </button>
                            )}
                            {filters.evChargingOnly && (
                                <button
                                    type="button"
                                    className="customer-search-filter-chip"
                                    onClick={() =>
                                        setFilters((current) => ({
                                            ...current,
                                            evChargingOnly: false,
                                        }))
                                    }
                                    aria-label={copy(
                                        "Remove EV charging filter",
                                        "Bỏ lọc trạm sạc",
                                    )}
                                >
                                    {copy("EV charging", "Có trạm sạc")}{" "}
                                    <span aria-hidden="true">×</span>
                                </button>
                            )}
                        </div>
                        <button
                            type="button"
                            className="customer-search-clear-filters"
                            onClick={clearFilters}
                            disabled={!canClearFilters}
                        >
                            <CustomerIcon name="history" size={17} />
                            {copy("Clear filters", "Xóa bộ lọc")}
                        </button>
                    </div>
                    <div
                        className="customer-search-filter-panel"
                        id="customer-search-filters"
                        hidden={!filtersOpen}
                    >
                        <fieldset className="customer-search-filter-group customer-search-price-group">
                            <legend>
                                {copy("Starting price (₫)", "Giá từ (₫)")}
                            </legend>
                            <input
                                type="range"
                                min="0"
                                max={MAX_PRICE}
                                step="5000"
                                value={filters.maxPrice}
                                aria-label={copy(
                                    "Maximum starting price",
                                    "Giá từ tối đa",
                                )}
                                onChange={(event) =>
                                    setFilters((current) => ({
                                        ...current,
                                        maxPrice: Number(event.target.value),
                                    }))
                                }
                            />
                            <div className="customer-search-price-values">
                                <span>0₫</span>
                                <label>
                                    <span className="customer-search-sr-only">
                                        {copy(
                                            "Maximum starting price in dong",
                                            "Giá từ tối đa bằng đồng",
                                        )}
                                    </span>
                                    <input
                                        type="number"
                                        min="0"
                                        max={MAX_PRICE}
                                        step="1000"
                                        value={filters.maxPrice}
                                        onChange={(event) =>
                                            setFilters((current) => ({
                                                ...current,
                                                maxPrice: Math.min(
                                                    MAX_PRICE,
                                                    Math.max(
                                                        0,
                                                        Number(
                                                            event.target.value,
                                                        ),
                                                    ),
                                                ),
                                            }))
                                        }
                                    />
                                    ₫
                                </label>
                            </div>
                        </fieldset>
                        <fieldset className="customer-search-filter-group">
                            <legend>{copy("Distance", "Khoảng cách")}</legend>
                            <label
                                className="customer-search-distance-label"
                                htmlFor="parking-distance"
                            >
                                {copy("Maximum distance", "Khoảng cách tối đa")}
                            </label>
                            <select
                                id="parking-distance"
                                className="customer-search-filter-select"
                                value={distanceChoice}
                                onChange={(event) => {
                                    const value = event.target.value;
                                    setSearchMessage("");
                                    setCustomDistanceError("");
                                    setDistanceChoice(value);
                                    if (value === "any") {
                                        clearDistanceFilter();
                                    } else if (value === "custom") {
                                        if (
                                            filters.distance !== "any" &&
                                            !distanceLabels[filters.distance]
                                        ) {
                                            setCustomDistanceInput(
                                                filters.distance,
                                            );
                                        }
                                    } else if (!searchOrigin) {
                                        setDistanceChoice("any");
                                        setSearchMessage(
                                            copy(
                                                "Choose a destination or use your current location to filter by distance.",
                                                "Chọn điểm đến hoặc dùng vị trí hiện tại để lọc theo khoảng cách.",
                                            ),
                                        );
                                    } else {
                                        setFilters((current) => ({
                                            ...current,
                                            distance: value,
                                        }));
                                    }
                                }}
                                aria-label={copy(
                                    "Maximum distance from destination",
                                    "Khoảng cách tối đa từ điểm đến",
                                )}
                            >
                                <option value="any">
                                    {copy("Any distance", "Mọi khoảng cách")}
                                </option>
                                {["1", "2", "5", "10", "20"].map((value) => (
                                    <option value={value} key={value}>
                                        {distanceLabels[value]}
                                    </option>
                                ))}
                                <option value="custom">
                                    {copy(
                                        "Other / custom distance",
                                        "Khác / tùy chỉnh",
                                    )}
                                </option>
                            </select>
                            {distanceChoice === "custom" && (
                                <div className="customer-search-custom-distance">
                                    <label htmlFor="custom-distance-km">
                                        {copy(
                                            "Distance in km",
                                            "Khoảng cách (km)",
                                        )}
                                    </label>
                                    <div className="customer-search-custom-distance-row">
                                        <input
                                            id="custom-distance-km"
                                            type="number"
                                            min="0.1"
                                            max="50"
                                            step="0.1"
                                            inputMode="decimal"
                                            required
                                            value={customDistanceInput}
                                            onChange={(event) => {
                                                setCustomDistanceInput(
                                                    event.target.value,
                                                );
                                                setCustomDistanceError("");
                                            }}
                                            aria-invalid={Boolean(
                                                customDistanceError,
                                            )}
                                            aria-describedby={
                                                customDistanceError
                                                    ? "custom-distance-error"
                                                    : "custom-distance-hint"
                                            }
                                        />
                                        <button
                                            type="button"
                                            className="customer-search-apply-distance"
                                            onClick={applyCustomDistance}
                                        >
                                            {copy("Apply", "Áp dụng")}
                                        </button>
                                    </div>
                                    <small
                                        id={
                                            customDistanceError
                                                ? "custom-distance-error"
                                                : "custom-distance-hint"
                                        }
                                        role={
                                            customDistanceError
                                                ? "alert"
                                                : undefined
                                        }
                                    >
                                        {customDistanceError ||
                                            copy(
                                                "Enter a value from 0.1 to 50 km.",
                                                "Nhập giá trị từ 0,1 đến 50 km.",
                                            )}
                                    </small>
                                </div>
                            )}
                        </fieldset>
                        <fieldset className="customer-search-filter-group">
                            <legend>{copy("EV charging", "Trạm sạc")}</legend>
                            <label className="customer-search-filter-choice">
                                <input
                                    type="checkbox"
                                    checked={filters.evChargingOnly}
                                    onChange={(event) =>
                                        setFilters((current) => ({
                                            ...current,
                                            evChargingOnly:
                                                event.target.checked,
                                        }))
                                    }
                                />
                                <span>
                                    {copy(
                                        "Has EV charging station",
                                        "Có trạm sạc xe điện",
                                    )}
                                </span>
                            </label>
                        </fieldset>
                    </div>
                    <div className="customer-search-results-toolbar">
                        <span className="customer-search-preview-count">
                            {visibleParkings.length}{" "}
                            {copy(
                                visibleParkings.length === 1
                                    ? "parking lot found"
                                    : "parking lots found",
                                "bãi đỗ được tìm thấy",
                            )}
                        </span>
                        <div className="customer-search-sort-control">
                            <span>{copy("Sort by", "Sắp xếp")}</span>
                            <CustomerSearchDropdown
                                label={copy("Sort by", "Sắp xếp")}
                                value={filters.sort}
                                options={filterOptions.sort
                                    .filter(
                                        (option) =>
                                            searchOrigin ||
                                            option.value !== "distance",
                                    )
                                    .map((option) => ({
                                        ...option,
                                        label:
                                            option.value === "recommended"
                                                ? copy(
                                                      "Best match",
                                                      "Phù hợp nhất",
                                                  )
                                                : option.value === "name"
                                                  ? copy("Name A–Z", "Tên A–Z")
                                                  : option.value === "price"
                                                    ? copy(
                                                          "Lowest price",
                                                          "Giá thấp nhất",
                                                      )
                                                    : copy(
                                                          "Nearest",
                                                          "Gần nhất",
                                                      ),
                                    }))}
                                onChange={(sort) =>
                                    setFilters((current) => ({
                                        ...current,
                                        sort,
                                    }))
                                }
                                align="right"
                            />
                        </div>
                    </div>
                </div>
                <div className="customer-search-workspace">
                    <div className="customer-search-results">
                        <div className="customer-search-results-note">
                            <CustomerIcon name="location" size={17} />
                            <span>
                                {resultsState === "loading"
                                    ? copy(
                                          "Loading approved parking lots…",
                                          "Đang tải bãi đỗ đã duyệt…",
                                      )
                                    : resultsState === "error"
                                      ? copy(
                                            "Parking search is unavailable right now. Try again later.",
                                            "Không thể tìm bãi đỗ lúc này. Vui lòng thử lại sau.",
                                        )
                                      : searchOrigin
                                        ? `${copy("Approved parking lots within", "Bãi đỗ đã duyệt trong phạm vi")} ${radiusKm} km.`
                                        : copy(
                                              "All approved parking lots. Search a destination to see the nearest ones.",
                                              "Tất cả bãi đỗ đã duyệt. Tìm điểm đến để xem bãi gần nhất.",
                                          )}
                            </span>
                        </div>
                        {currentRecommendations &&
                            currentRecommendations.length > 0 && (
                                <AiRecommendationPanel
                                    recommendations={currentRecommendations}
                                    activeId={activeSelectedId}
                                    onSelect={setSelectedId}
                                    copy={copy}
                                />
                            )}
                        <div className="customer-search-list">
                            {visibleParkings.map((parking) => {
                                const lotRec = allRecommendations?.find(
                                    (r) => r.lotId === parking.id,
                                );
                                return (
                                    <article
                                        className={`customer-search-result ${activeSelectedId === parking.id ? "selected" : ""}`}
                                        key={parking.id}
                                    >
                                        <div className="customer-search-result-top">
                                            <span>
                                                {parking.type.toUpperCase()}
                                            </span>
                                            {lotRec && (
                                                <span
                                                    className="customer-ai-highlight-badge"
                                                    title={lotRec.reasons.join(
                                                        " · ",
                                                    )}
                                                >
                                                    {copy("Top", "Hạng")}{" "}
                                                    {lotRec.rank}
                                                    {lotRec.probabilityPercent !==
                                                    null ? (
                                                        <>
                                                            ,{" "}
                                                            {Math.round(
                                                                lotRec.probabilityPercent,
                                                            )}
                                                            %{" "}
                                                            {copy(
                                                                "free",
                                                                "còn chỗ",
                                                            )}
                                                        </>
                                                    ) : (
                                                        <>
                                                            ,{" "}
                                                            {copy(
                                                                "distance rank",
                                                                "sắp theo khoảng cách",
                                                            )}
                                                        </>
                                                    )}
                                                </span>
                                            )}
                                            {activeSelectedId ===
                                                parking.id && (
                                                <strong>
                                                    <CustomerIcon
                                                        name="check"
                                                        size={15}
                                                    />{" "}
                                                    {copy(
                                                        "Selected on map",
                                                        "Đã chọn trên bản đồ",
                                                    )}
                                                </strong>
                                            )}
                                        </div>
                                        <div className="customer-search-result-main">
                                            <div className="customer-search-result-photo">
                                                <Image
                                                    src="/images/parkwise/dashboard-parking-structure.webp"
                                                    alt={copy(
                                                        "Illustration of a parking facility",
                                                        "Minh họa bãi đỗ xe",
                                                    )}
                                                    fill
                                                    sizes="(max-width: 700px) 100vw, 200px"
                                                />
                                                <span className="customer-search-result-photo-label">
                                                    {copy(
                                                        "Illustration",
                                                        "Ảnh minh họa",
                                                    )}
                                                </span>
                                            </div>
                                            <div className="customer-search-result-content">
                                                <h2>{parking.name}</h2>
                                                <p className="customer-search-result-location">
                                                    <CustomerIcon
                                                        name="location"
                                                        size={15}
                                                    />{" "}
                                                    {parking.address}
                                                </p>
                                                <div className="customer-search-result-metrics">
                                                    <span>
                                                        <CustomerIcon
                                                            name="location"
                                                            size={17}
                                                        />
                                                        <small>
                                                            {copy(
                                                                "Distance",
                                                                "Khoảng cách",
                                                            )}
                                                        </small>
                                                        <b>
                                                            {parking.distanceKm ==
                                                            null
                                                                ? copy(
                                                                      "Search to see",
                                                                      "Tìm để xem",
                                                                  )
                                                                : `${parking.distanceKm} km`}
                                                        </b>
                                                    </span>
                                                    <span>
                                                        <small>
                                                            {copy(
                                                                "Starting price",
                                                                "Giá khởi điểm",
                                                            )}
                                                        </small>
                                                        <b>
                                                            {parking.metricsState ===
                                                            "loading"
                                                                ? copy(
                                                                      "Loading...",
                                                                      "Đang tải...",
                                                                  )
                                                                : parking.metricsState ===
                                                                    "error"
                                                                  ? copy(
                                                                        "Unavailable",
                                                                        "Chưa có dữ liệu",
                                                                    )
                                                                  : parking.startingPrice ==
                                                                      null
                                                                    ? copy(
                                                                          "Not set",
                                                                          "Chưa có giá",
                                                                      )
                                                                    : `${new Intl.NumberFormat("vi-VN").format(parking.startingPrice)} ₫`}
                                                        </b>
                                                    </span>
                                                    <span>
                                                        <small>
                                                            {copy(
                                                                "Spaces now",
                                                                "Chỗ trống hiện tại",
                                                            )}
                                                        </small>
                                                        <b>
                                                            {parking.metricsState ===
                                                            "loading"
                                                                ? copy(
                                                                      "Loading...",
                                                                      "Đang tải...",
                                                                  )
                                                                : (parking.availableSpaces ??
                                                                  copy(
                                                                      "Unavailable",
                                                                      "Chưa có dữ liệu",
                                                                  ))}
                                                        </b>
                                                    </span>
                                                </div>
                                                {originKey && (
                                                    <p className="customer-search-driving-time">
                                                        <CustomerIcon
                                                            name="clock"
                                                            size={15}
                                                        />
                                                        {parking.drivingTimeMinutes ==
                                                        null
                                                            ? copy(
                                                                  "Driving time unavailable",
                                                                  "Chưa có thời gian lái xe",
                                                              )
                                                            : `~${parking.drivingTimeMinutes.toLocaleString(copy("en-US", "vi-VN"), { maximumFractionDigits: 1 })} ${copy("min drive", "phút lái xe")}`}
                                                    </p>
                                                )}
                                                <div className="customer-search-result-actions">
                                                    <button
                                                        type="button"
                                                        className="customer-shell-secondary"
                                                        onClick={() =>
                                                            setSelectedId(
                                                                parking.id,
                                                            )
                                                        }
                                                    >
                                                        {copy(
                                                            "View on map",
                                                            "Xem trên bản đồ",
                                                        )}{" "}
                                                        <CustomerIcon
                                                            name="arrow"
                                                            size={15}
                                                        />
                                                    </button>
                                                    <Link
                                                        href={`/parking-lots/${parking.id}`}
                                                        className="customer-shell-primary"
                                                    >
                                                        {copy(
                                                            "View details",
                                                            "Xem chi tiết",
                                                        )}
                                                        <CustomerIcon
                                                            name="arrow"
                                                            size={15}
                                                        />
                                                    </Link>
                                                </div>
                                            </div>
                                        </div>
                                    </article>
                                );
                            })}
                        </div>
                        {resultsState === "ready" &&
                            visibleParkings.length === 0 && (
                                <div className="customer-search-empty">
                                    <CustomerIcon name="search" size={28} />
                                    <strong>
                                        {copy(
                                            "No approved parking lots found",
                                            "Không tìm thấy bãi đỗ đã duyệt",
                                        )}
                                    </strong>
                                    <p>
                                        {copy(
                                            "Try another destination or a larger distance.",
                                            "Thử điểm đến khác hoặc tăng khoảng cách tìm kiếm.",
                                        )}
                                    </p>
                                    <button
                                        type="button"
                                        className="customer-shell-secondary"
                                        onClick={clearFilters}
                                    >
                                        {copy("Clear filters", "Xóa bộ lọc")}
                                    </button>
                                </div>
                            )}
                    </div>
                    <div className="customer-search-map-column">
                        <div className="customer-search-map-header">
                            <strong>
                                {copy("Parking map", "Bản đồ bãi đỗ")}
                            </strong>
                            <span>
                                {copy(
                                    "Ho Chi Minh City",
                                    "Thành phố Hồ Chí Minh",
                                )}
                            </span>
                        </div>
                        <CustomerMap
                            loadEnabled={resultsState !== "loading"}
                            selectedId={activeSelectedId}
                            onSelect={setSelectedId}
                            currentLocation={currentLocation}
                            destinationLocation={destinationLocation}
                            lots={visibleParkings}
                            percentByLot={slotAtArrival?.percentByLot ?? null}
                        />
                        {searchOrigin && (
                            <ArrivalTimeScrubber
                                slots={forecast.slots.map((slot) => {
                                    const values = slot.percentByLot
                                        ? Object.values(slot.percentByLot)
                                        : [];
                                    const selectedValue =
                                        slot.percentByLot?.[activeSelectedId];
                                    const average = values.length
                                        ? Math.round(
                                              values.reduce(
                                                  (a, b) => a + b,
                                                  0,
                                              ) / values.length,
                                          )
                                        : null;
                                    return {
                                        hour: slot.hour,
                                        percent: selectedValue ?? average,
                                    };
                                })}
                                selectedHour={arrivalHour}
                                onChange={setArrivalHour}
                                loading={forecast.loading}
                                copy={copy}
                            />
                        )}
                        <div className="customer-search-map-footer">
                            <span>
                                <i className="customer-search-map-dot" />{" "}
                                {copy(
                                    "Approved parking lot",
                                    "Bãi đỗ đã duyệt",
                                )}
                            </span>
                            <Link href="/customer/vehicles">
                                {copy("Manage vehicles", "Quản lý xe")}{" "}
                                <CustomerIcon name="arrow" size={15} />
                            </Link>
                        </div>
                    </div>
                </div>
            </main>
            <CustomerFooter />
        </div>
    );
}
