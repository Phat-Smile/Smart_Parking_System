"use client";

import {
    useEffect,
    useRef,
    useState,
    type FormEvent,
    type KeyboardEvent,
} from "react";
import Image from "next/image";
import Link from "next/link";
import { CustomerMap } from "./CustomerMap";
import { CustomerFooter, CustomerHeader, CustomerIcon } from "./CustomerShell";
import { CustomerSearchDropdown } from "./CustomerSearchDropdown";
import { getPublicMapboxToken } from "@/shared/config/env";
import {
    getApprovedParkingLots,
    searchNearbyParkingLots,
    type NearbyParkingLot,
    type ParkingLotSummary,
} from "./parkingLotApi";

// A search result: an approved lot, with its distance when the search had a point.
type SearchResult = ParkingLotSummary & { distanceKm?: number };
import {
    geocodeDestination,
    retrieveDestination,
    suggestDestinations,
    type DestinationSuggestion,
    type MapCoordinates,
} from "./geocodeDestination";

const MAX_PRICE = 50000;

const initialFilters = {
    maxPrice: MAX_PRICE,
    distance: "any",
    availability: "any",
    compatibility: [] as string[],
    amenities: [] as string[],
    sort: "recommended",
};

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
    const [destination, setDestination] = useState(initialSearch.destination);
    const [parkingDate, setParkingDate] = useState(initialSearch.date);
    const [parkingTime, setParkingTime] = useState(initialSearch.time);
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
    const [approvedLots, setApprovedLots] = useState<ParkingLotSummary[]>([]);
    const [catalogState, setCatalogState] = useState<
        "loading" | "ready" | "error"
    >("loading");
    useEffect(() => {
        const controller = new AbortController();
        getApprovedParkingLots(controller.signal)
            .then((lots) => {
                setApprovedLots(lots);
                setCatalogState("ready");
            })
            .catch(() => {
                if (!controller.signal.aborted) setCatalogState("error");
            });
        return () => controller.abort();
    }, []);
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
    const activeFilterCount =
        Number(filters.maxPrice < MAX_PRICE) +
        Number(filters.distance !== "any") +
        Number(filters.availability !== "any") +
        Number(filters.compatibility.length > 0) +
        Number(filters.amenities.length > 0);
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
        lots: NearbyParkingLot[];
    } | null>(null);
    const originKey = searchOrigin
        ? `${searchOrigin.latitude},${searchOrigin.longitude},${radiusKm}`
        : "";
    useEffect(() => {
        if (!originKey) return;
        const [latitude, longitude, radius] = originKey.split(",").map(Number);
        const controller = new AbortController();
        void (async () => {
            setNearby({ key: originKey, state: "loading", lots: [] });
            try {
                const lots = await searchNearbyParkingLots(
                    latitude,
                    longitude,
                    radius,
                    controller.signal,
                );
                setNearby({ key: originKey, state: "ready", lots });
            } catch {
                if (!controller.signal.aborted)
                    setNearby({ key: originKey, state: "error", lots: [] });
            }
        })();
        return () => controller.abort();
    }, [originKey]);
    const nearbyCurrent = nearby?.key === originKey ? nearby : null;
    const resultsState: "loading" | "ready" | "error" = originKey
        ? (nearbyCurrent?.state ?? "loading")
        : catalogState;
    const results: SearchResult[] = originKey
        ? (nearbyCurrent?.lots ?? [])
        : approvedLots;
    // Price, availability, vehicle type and amenity filters need data Parking does not return in search yet.
    const visibleParkings = [...results].sort((a, b) => {
        if (filters.sort === "name") return a.name.localeCompare(b.name);
        return (
            (a.distanceKm ?? 0) - (b.distanceKm ?? 0) ||
            a.name.localeCompare(b.name)
        );
    });
    const activeSelectedId = visibleParkings.some(
        (parking) => parking.id === selectedId,
    )
        ? selectedId
        : (visibleParkings[0]?.id ?? "");

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
        setSubmittedDestination("");
        setDestination("");
        setVehicleType("any");
        setSearchMessage("");
        setCurrentLocation(null);
    }

    function toggleFilterValue(
        key: "compatibility" | "amenities",
        value: string,
    ) {
        setFilters((current) => ({
            ...current,
            [key]: current[key].includes(value)
                ? current[key].filter((item) => item !== value)
                : [...current[key], value],
        }));
    }

    const distanceLabels: Record<string, string> = {
        "1": "Within 1 km",
        "2": "Within 2 km",
        "5": "Within 5 km",
    };
    const compatibilityLabels: Record<string, string> = {
        car: "Car",
        motorbike: "Motorbike",
        ev: "Electric vehicle (EV)",
        large: "Large vehicle",
    };
    const amenityLabels: Record<string, string> = {
        "ev-charging": "EV Charging",
        covered: "Covered Parking",
        "always-open": "24/7 Access",
        cctv: "CCTV",
    };

    return (
        <div
            lang="en"
            className={`parkwise-page customer-shell-page customer-search-page ${fontClassName}`}
        >
            <CustomerHeader active="search" />
            <main className="customer-shell-container customer-search-main">
                <div className="customer-search-intro">
                    <p className="customer-search-kicker">FIND YOUR SPACE</p>
                    <h1>Find parking near your destination.</h1>
                    <p>
                        Compare locations and plan your trip with a clearer view
                        of the city.
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
                                Destination
                            </label>
                            <input
                                ref={destinationInputRef}
                                id="customer-search-destination-input"
                                aria-label="Destination"
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
                                placeholder="Enter an address or landmark"
                                autoComplete="off"
                            />
                        </span>
                        <button
                            type="button"
                            className="customer-search-locate"
                            onClick={useCurrentLocation}
                            disabled={locationBusy}
                            aria-label="Use my current location"
                            title="Use my current location"
                        >
                            <CustomerIcon name="location" size={19} />
                        </button>
                        {suggestionsOpen && (
                            <div
                                id="customer-destination-suggestions"
                                className="customer-search-suggestions"
                                role="listbox"
                                aria-label="Destination suggestions"
                            >
                                {suggestionsBusy ? (
                                    <p
                                        className="customer-search-suggestions-state"
                                        role="status"
                                    >
                                        Searching places…
                                    </p>
                                ) : suggestionsError ? (
                                    <p
                                        className="customer-search-suggestions-state"
                                        role="status"
                                    >
                                        Places are unavailable. Try again
                                        shortly.
                                    </p>
                                ) : suggestions.length === 0 ? (
                                    <p
                                        className="customer-search-suggestions-state"
                                        role="status"
                                    >
                                        No matching places found.
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
                                    Search by Mapbox
                                </span>
                            </div>
                        )}
                    </div>
                    <label className="customer-search-field">
                        <CustomerIcon name="calendar" size={21} />
                        <span>
                            <strong>Date</strong>
                            <input
                                aria-label="Parking date"
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
                            <strong>Time</strong>
                            <input
                                aria-label="Parking time"
                                type="time"
                                value={parkingTime}
                                onChange={(event) =>
                                    setParkingTime(event.target.value)
                                }
                            />
                        </span>
                    </label>
                    <div className="customer-search-field customer-search-vehicle-field">
                        <CustomerIcon name="car" size={22} />
                        <CustomerSearchDropdown
                            label="Vehicle type"
                            value={vehicleType}
                            options={filterOptions.vehicle}
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
                            {searchBusy ? "Searching map…" : "Find Parking"}
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
                                    All Filters
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
                                    aria-label="Remove price filter"
                                >
                                    Price: Under{" "}
                                    {filters.maxPrice.toLocaleString("vi-VN")}₫{" "}
                                    <span aria-hidden="true">×</span>
                                </button>
                            )}
                            {filters.distance !== "any" && (
                                <button
                                    type="button"
                                    className="customer-search-filter-chip"
                                    onClick={() =>
                                        setFilters((current) => ({
                                            ...current,
                                            distance: "any",
                                        }))
                                    }
                                    aria-label="Remove distance filter"
                                >
                                    Distance: {distanceLabels[filters.distance]}{" "}
                                    <span aria-hidden="true">×</span>
                                </button>
                            )}
                            {filters.availability !== "any" && (
                                <button
                                    type="button"
                                    className="customer-search-filter-chip"
                                    onClick={() =>
                                        setFilters((current) => ({
                                            ...current,
                                            availability: "any",
                                        }))
                                    }
                                    aria-label="Remove availability filter"
                                >
                                    {filters.availability === "available"
                                        ? "Available now"
                                        : "Pre-bookable"}{" "}
                                    <span aria-hidden="true">×</span>
                                </button>
                            )}
                            {filters.compatibility.length > 0 && (
                                <button
                                    type="button"
                                    className="customer-search-filter-chip"
                                    onClick={() =>
                                        setFilters((current) => ({
                                            ...current,
                                            compatibility: [],
                                        }))
                                    }
                                    aria-label="Remove vehicle compatibility filter"
                                >
                                    Vehicle:{" "}
                                    {filters.compatibility
                                        .map(
                                            (type) => compatibilityLabels[type],
                                        )
                                        .join(", ")}{" "}
                                    <span aria-hidden="true">×</span>
                                </button>
                            )}
                            {filters.amenities.length > 0 && (
                                <button
                                    type="button"
                                    className="customer-search-filter-chip"
                                    onClick={() =>
                                        setFilters((current) => ({
                                            ...current,
                                            amenities: [],
                                        }))
                                    }
                                    aria-label="Remove amenities filter"
                                >
                                    Amenities:{" "}
                                    {filters.amenities
                                        .map(
                                            (amenity) => amenityLabels[amenity],
                                        )
                                        .join(", ")}{" "}
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
                            Clear filters
                        </button>
                    </div>
                    <div
                        className="customer-search-filter-panel"
                        id="customer-search-filters"
                        hidden={!filtersOpen}
                    >
                        <fieldset className="customer-search-filter-group customer-search-price-group">
                            <legend>Price (₫/hour)</legend>
                            <input
                                type="range"
                                min="0"
                                max={MAX_PRICE}
                                step="5000"
                                value={filters.maxPrice}
                                aria-label="Maximum price per hour"
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
                                        Maximum price per hour in dong
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
                            <legend>Distance</legend>
                            {[
                                ["any", "Any"],
                                ["1", "Within 1 km"],
                                ["2", "Within 2 km"],
                                ["5", "Within 5 km"],
                            ].map(([value, label]) => (
                                <label
                                    className="customer-search-filter-choice"
                                    key={value}
                                >
                                    <input
                                        type="radio"
                                        name="parking-distance"
                                        value={value}
                                        checked={filters.distance === value}
                                        onChange={() =>
                                            setFilters((current) => ({
                                                ...current,
                                                distance: value,
                                            }))
                                        }
                                    />
                                    <span>{label}</span>
                                </label>
                            ))}
                        </fieldset>
                        <fieldset className="customer-search-filter-group">
                            <legend>Availability</legend>
                            {[
                                ["any", "Any"],
                                ["available", "Available now"],
                                ["prebookable", "Pre-bookable"],
                            ].map(([value, label]) => (
                                <label
                                    className="customer-search-filter-choice"
                                    key={value}
                                >
                                    <input
                                        type="radio"
                                        name="parking-availability"
                                        value={value}
                                        checked={filters.availability === value}
                                        onChange={() =>
                                            setFilters((current) => ({
                                                ...current,
                                                availability: value,
                                            }))
                                        }
                                    />
                                    <span>{label}</span>
                                </label>
                            ))}
                        </fieldset>
                        <fieldset className="customer-search-filter-group">
                            <legend>Vehicle compatibility</legend>
                            {Object.entries(compatibilityLabels).map(
                                ([value, label]) => (
                                    <label
                                        className="customer-search-filter-choice"
                                        key={value}
                                    >
                                        <input
                                            type="checkbox"
                                            checked={filters.compatibility.includes(
                                                value,
                                            )}
                                            onChange={() =>
                                                toggleFilterValue(
                                                    "compatibility",
                                                    value,
                                                )
                                            }
                                        />
                                        <span>{label}</span>
                                    </label>
                                ),
                            )}
                        </fieldset>
                        <fieldset className="customer-search-filter-group">
                            <legend>Amenities</legend>
                            {Object.entries(amenityLabels).map(
                                ([value, label]) => (
                                    <label
                                        className="customer-search-filter-choice"
                                        key={value}
                                    >
                                        <input
                                            type="checkbox"
                                            checked={filters.amenities.includes(
                                                value,
                                            )}
                                            onChange={() =>
                                                toggleFilterValue(
                                                    "amenities",
                                                    value,
                                                )
                                            }
                                        />
                                        <span>{label}</span>
                                    </label>
                                ),
                            )}
                        </fieldset>
                    </div>
                    <div className="customer-search-results-toolbar">
                        <span className="customer-search-preview-count">
                            {visibleParkings.length} parking{" "}
                            {visibleParkings.length === 1 ? "lot" : "lots"}{" "}
                            found
                        </span>
                        <div className="customer-search-sort-control">
                            <span>Sort by</span>
                            <CustomerSearchDropdown
                                label="Sort by"
                                value={filters.sort}
                                options={filterOptions.sort}
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
                                    ? "Loading approved parking lots…"
                                    : resultsState === "error"
                                      ? "Parking search is unavailable right now. Try again later."
                                      : searchOrigin
                                        ? `Approved parking lots within ${radiusKm} km. Price and free spaces are on each lot's page.`
                                        : "All approved parking lots. Search a destination to see the nearest ones."}
                            </span>
                        </div>
                        <div className="customer-search-list">
                            {visibleParkings.map((parking) => (
                                <article
                                    className={`customer-search-result ${activeSelectedId === parking.id ? "selected" : ""}`}
                                    key={parking.id}
                                >
                                    <div className="customer-search-result-top">
                                        <span>
                                            {parking.type.toUpperCase()}
                                        </span>
                                        {activeSelectedId === parking.id && (
                                            <strong>
                                                <CustomerIcon
                                                    name="check"
                                                    size={15}
                                                />{" "}
                                                Selected on map
                                            </strong>
                                        )}
                                    </div>
                                    <div className="customer-search-result-main">
                                        <div className="customer-search-result-photo">
                                            <Image
                                                src="/images/parkwise/dashboard-parking-structure.webp"
                                                alt="Illustration of a parking facility"
                                                fill
                                                sizes="(max-width: 700px) 100vw, 200px"
                                            />
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
                                                    <small>Distance</small>
                                                    <b>
                                                        {parking.distanceKm ===
                                                        undefined
                                                            ? "—"
                                                            : `${parking.distanceKm} km`}
                                                    </b>
                                                </span>
                                            </div>
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
                                                    View on map{" "}
                                                    <CustomerIcon
                                                        name="arrow"
                                                        size={15}
                                                    />
                                                </button>
                                                <Link
                                                    href={`/parking-lots/${parking.id}`}
                                                    className="customer-shell-primary"
                                                >
                                                    View details
                                                    <CustomerIcon
                                                        name="arrow"
                                                        size={15}
                                                    />
                                                </Link>
                                            </div>
                                        </div>
                                    </div>
                                </article>
                            ))}
                        </div>
                        {resultsState === "ready" &&
                            visibleParkings.length === 0 && (
                                <div className="customer-search-empty">
                                    <CustomerIcon name="search" size={28} />
                                    <strong>
                                        No approved parking lots found
                                    </strong>
                                    <p>
                                        Try another destination or a larger
                                        distance.
                                    </p>
                                    <button
                                        type="button"
                                        className="customer-shell-secondary"
                                        onClick={clearFilters}
                                    >
                                        Clear filters
                                    </button>
                                </div>
                            )}
                    </div>
                    <div className="customer-search-map-column">
                        <div className="customer-search-map-header">
                            <strong>Parking map</strong>
                            <span>Ho Chi Minh City</span>
                        </div>
                        <CustomerMap
                            selectedId={activeSelectedId}
                            onSelect={setSelectedId}
                            currentLocation={currentLocation}
                            destinationLocation={destinationLocation}
                            lots={visibleParkings}
                        />
                        <div className="customer-search-map-footer">
                            <span>
                                <i className="customer-search-map-dot" />{" "}
                                Approved parking lot
                            </span>
                            <Link href="/customer/vehicles">
                                Manage vehicles{" "}
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
