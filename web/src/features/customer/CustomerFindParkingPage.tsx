"use client";

import { useEffect, useState, type FormEvent } from "react";
import Image from "next/image";
import Link from "next/link";
import { CustomerMap } from "./CustomerMap";
import { CustomerFooter, CustomerHeader, CustomerIcon } from "./CustomerShell";
import { CustomerSearchDropdown } from "./CustomerSearchDropdown";
import { getPublicMapboxToken } from "@/shared/config/env";

const initialFilters = {
    price: "any",
    distance: "any",
    availability: "any",
    compatibility: "any",
    sort: "recommended",
};

const filterOptions = {
    price: [
        { value: "any", label: "Any", icon: "price" as const },
        {
            value: "under-20000",
            label: "Under 20k / hour",
            icon: "price" as const,
        },
        {
            value: "under-30000",
            label: "Under 30k / hour",
            icon: "price" as const,
        },
    ],
    distance: [
        { value: "any", label: "Any", icon: "distance" as const },
        { value: "near", label: "Nearest", icon: "target" as const },
        { value: "two", label: "Within 2 km", icon: "distance" as const },
        { value: "five", label: "Within 5 km", icon: "distance" as const },
    ],
    availability: [
        { value: "any", label: "Any", icon: "filter" as const },
        { value: "available", label: "Available", icon: "check" as const },
    ],
    compatibility: [
        { value: "any", label: "Any", icon: "filter" as const },
        { value: "car", label: "Car", icon: "car" as const },
        { value: "motorbike", label: "Motorbike", icon: "motorbike" as const },
    ],
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

const parkingExamples = [
    {
        id: "parkside",
        name: "Parkside Parking",
        image: "/images/parkwise/dashboard-parking-entrance.webp",
        type: "City parking entrance",
        area: "District 1",
        hourlyPrice: 25000,
        distanceKm: 1.2,
        spaces: 12,
        compatible: ["car", "motorbike"],
    },
    {
        id: "sunrise",
        name: "City parking",
        image: "/images/parkwise/dashboard-parking-structure.webp",
        type: "Multi-storey parking",
        area: "Binh Thanh",
        hourlyPrice: 15000,
        distanceKm: 2.4,
        spaces: 4,
        compatible: ["motorbike"],
    },
    {
        id: "central",
        name: "Covered garage",
        image: "/images/parkwise/dashboard-parking-garage.webp",
        type: "Sheltered spaces",
        area: "District 3",
        hourlyPrice: 40000,
        distanceKm: 4.3,
        spaces: 0,
        compatible: ["car"],
    },
];

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
    const [currentLocation, setCurrentLocation] = useState<{
        longitude: number;
        latitude: number;
    } | null>(null);
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
    const [filters, setFilters] = useState(initialFilters);
    const hasActiveFilters = Object.entries(initialFilters).some(
        ([key, value]) => filters[key as keyof typeof filters] !== value,
    );
    const visibleParkings = parkingExamples
        .filter((parking) => {
            const query = submittedDestination.toLocaleLowerCase();
            return (
                (!query ||
                    query === "current location" ||
                    `${parking.name} ${parking.type} ${parking.area} ho chi minh city`
                        .toLocaleLowerCase()
                        .includes(query)) &&
                (filters.price === "any" ||
                    parking.hourlyPrice <
                        Number(filters.price.split("-")[1])) &&
                (filters.distance === "any" ||
                    filters.distance === "near" ||
                    parking.distanceKm <=
                        Number(filters.distance === "two" ? 2 : 5)) &&
                (filters.availability === "any" || parking.spaces > 0) &&
                (filters.compatibility === "any" ||
                    parking.compatible.includes(filters.compatibility)) &&
                (vehicleType === "any" ||
                    parking.compatible.includes(vehicleType))
            );
        })
        .sort((a, b) => {
            if (filters.sort === "name") return a.name.localeCompare(b.name);
            if (filters.sort === "price") return a.hourlyPrice - b.hourlyPrice;
            if (filters.sort === "distance" || filters.distance === "near")
                return a.distanceKm - b.distanceKm;
            return parkingExamples.indexOf(a) - parkingExamples.indexOf(b);
        });
    const activeSelectedId = visibleParkings.some(
        (parking) => parking.id === selectedId,
    )
        ? selectedId
        : (visibleParkings[0]?.id ?? "");

    function useCurrentLocation() {
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

    function handleSearch(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setSubmittedDestination(destination.trim());
        setSearchMessage(
            destination.trim()
                ? `Showing matching example locations for “${destination.trim()}”.`
                : "Showing all example parking locations.",
        );
    }

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
                    <div className="customer-search-field customer-search-destination">
                        <CustomerIcon name="location" size={21} />
                        <span>
                            <label htmlFor="customer-search-destination-input">
                                Destination
                            </label>
                            <input
                                id="customer-search-destination-input"
                                aria-label="Destination"
                                value={destination}
                                onChange={(event) => {
                                    setDestination(event.target.value);
                                    setCurrentLocation(null);
                                    setSearchMessage("");
                                }}
                                placeholder="Enter an address or landmark"
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
                    >
                        <CustomerIcon name="search" size={21} />
                        <span>Find Parking</span>
                        <CustomerIcon name="arrow" size={19} />
                    </button>
                </form>
                {searchMessage && (
                    <p className="customer-search-feedback" role="status">
                        {searchMessage}
                    </p>
                )}

                <div className="customer-search-toolbar">
                    <button
                        type="button"
                        className={`customer-search-filter-button ${filtersOpen ? "active" : ""}`}
                        onClick={() => setFiltersOpen((open) => !open)}
                        aria-expanded={filtersOpen}
                        aria-controls="customer-search-filters"
                    >
                        <CustomerIcon name="filter" size={17} /> All Filters
                    </button>
                    {filtersOpen && (
                        <div
                            className="customer-search-filter-controls"
                            id="customer-search-filters"
                        >
                            <CustomerSearchDropdown
                                label="Price"
                                value={filters.price}
                                options={filterOptions.price}
                                onChange={(price) =>
                                    setFilters((current) => ({
                                        ...current,
                                        price,
                                    }))
                                }
                            />
                            <CustomerSearchDropdown
                                label="Distance"
                                value={filters.distance}
                                options={filterOptions.distance}
                                onChange={(distance) =>
                                    setFilters((current) => ({
                                        ...current,
                                        distance,
                                    }))
                                }
                            />
                            <CustomerSearchDropdown
                                label="Availability"
                                value={filters.availability}
                                options={filterOptions.availability}
                                onChange={(availability) =>
                                    setFilters((current) => ({
                                        ...current,
                                        availability,
                                    }))
                                }
                            />
                            <CustomerSearchDropdown
                                label="Vehicle compatibility"
                                value={filters.compatibility}
                                options={filterOptions.compatibility}
                                onChange={(compatibility) =>
                                    setFilters((current) => ({
                                        ...current,
                                        compatibility,
                                    }))
                                }
                            />
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
                    )}
                    {(hasActiveFilters ||
                        submittedDestination ||
                        vehicleType !== "any") && (
                        <button
                            type="button"
                            className="customer-search-clear-filters"
                            onClick={() => {
                                setFilters(initialFilters);
                                setSubmittedDestination("");
                                setDestination("");
                                setVehicleType("any");
                                setSearchMessage("");
                            }}
                        >
                            Clear filters
                        </button>
                    )}
                    <span className="customer-search-preview-count">
                        {visibleParkings.length} example{" "}
                        {visibleParkings.length === 1
                            ? "location"
                            : "locations"}
                    </span>
                </div>
                <div className="customer-search-workspace">
                    <div className="customer-search-results">
                        <div className="customer-search-results-note">
                            <CustomerIcon name="location" size={17} />
                            <span>
                                Prices, distances and spaces below are example
                                values. Live availability and booking are not
                                connected yet.
                            </span>
                        </div>
                        <div className="customer-search-list">
                            {visibleParkings.map((parking, index) => (
                                <article
                                    className={`customer-search-result ${activeSelectedId === parking.id ? "selected" : ""}`}
                                    key={parking.id}
                                >
                                    <div className="customer-search-result-top">
                                        <span>
                                            {index === 0
                                                ? "FEATURED PREVIEW"
                                                : "DESIGN PREVIEW"}
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
                                                src={parking.image}
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
                                                {parking.type} · {parking.area},
                                                Ho Chi Minh City
                                            </p>
                                            <div className="customer-search-result-metrics">
                                                <span>
                                                    <CustomerIcon
                                                        name="location"
                                                        size={17}
                                                    />
                                                    <small>Distance</small>
                                                    <b>
                                                        {parking.distanceKm} km
                                                        demo
                                                    </b>
                                                </span>
                                                <span>
                                                    <CustomerIcon
                                                        name="price"
                                                        size={17}
                                                    />
                                                    <small>Price / hour</small>
                                                    <b>
                                                        {parking.hourlyPrice.toLocaleString(
                                                            "vi-VN",
                                                        )}
                                                        ₫ demo
                                                    </b>
                                                </span>
                                                <span>
                                                    <CustomerIcon
                                                        name="car"
                                                        size={17}
                                                    />
                                                    <small>Spaces</small>
                                                    <b>{parking.spaces} demo</b>
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
                                                <button
                                                    type="button"
                                                    className="customer-shell-primary"
                                                    disabled
                                                    title="Booking will be available when live parking data is connected"
                                                >
                                                    Book Now
                                                </button>
                                            </div>
                                        </div>
                                    </div>
                                </article>
                            ))}
                        </div>
                        {visibleParkings.length === 0 && (
                            <div className="customer-search-empty">
                                <CustomerIcon name="search" size={28} />
                                <strong>No matching example locations</strong>
                                <p>
                                    Try another destination or clear the
                                    filters. Live parking search is not
                                    connected yet.
                                </p>
                                <button
                                    type="button"
                                    className="customer-shell-secondary"
                                    onClick={() => {
                                        setFilters(initialFilters);
                                        setSubmittedDestination("");
                                        setDestination("");
                                        setVehicleType("any");
                                    }}
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
                            visibleIds={visibleParkings.map(
                                (parking) => parking.id,
                            )}
                        />
                        <div className="customer-search-map-footer">
                            <span>
                                <i className="customer-search-map-dot" />{" "}
                                Example location
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
