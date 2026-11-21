"use client";

import { type FormEvent, type KeyboardEvent, type RefObject } from "react";
import { CustomerIcon } from "../CustomerShell";
import { CustomerSearchDropdown } from "../CustomerSearchDropdown";
import {
    type DestinationSuggestion,
    type MapCoordinates,
} from "../geocodeDestination";
import { filterOptions, type FilterState } from "./useCustomerFindParking";

export type CustomerSearchFormProps = {
    destination: string;
    destinationInputRef: RefObject<HTMLInputElement | null>;
    suggestions: DestinationSuggestion[];
    suggestionsOpen: boolean;
    setSuggestionsOpen: (open: boolean) => void;
    suggestionsBusy: boolean;
    suggestionsError: boolean;
    activeSuggestionIndex: number;
    setActiveSuggestionIndex: (
        index: number | ((prev: number) => number),
    ) => void;
    onDestinationInputChange: (value: string) => void;
    onDestinationKeyDown: (event: KeyboardEvent<HTMLInputElement>) => void;
    selectSuggestion: (suggestion: DestinationSuggestion) => Promise<void>;
    useCurrentLocation: () => void;
    locationBusy: boolean;
    distanceChoice: string;
    onDistanceChoiceChange: (value: string) => void;
    distanceNeedsOrigin: boolean;
    distanceLabels: Record<string, string>;
    searchOrigin:
        MapCoordinates | { longitude: number; latitude: number } | null;
    filters: FilterState;
    setFilters: React.Dispatch<React.SetStateAction<FilterState>>;
    vehicleType: string;
    setVehicleType: (value: string) => void;
    searchBusy: boolean;
    handleSearch: (event: FormEvent<HTMLFormElement>) => Promise<void>;
    copy: (en: string, vi: string) => string;
};

export function CustomerSearchForm({
    destination,
    destinationInputRef,
    suggestions,
    suggestionsOpen,
    setSuggestionsOpen,
    suggestionsBusy,
    suggestionsError,
    activeSuggestionIndex,
    setActiveSuggestionIndex,
    onDestinationInputChange,
    onDestinationKeyDown,
    selectSuggestion,
    useCurrentLocation,
    locationBusy,
    distanceChoice,
    onDistanceChoiceChange,
    distanceNeedsOrigin,
    distanceLabels,
    searchOrigin,
    filters,
    setFilters,
    vehicleType,
    setVehicleType,
    searchBusy,
    handleSearch,
    copy,
}: CustomerSearchFormProps) {
    return (
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
                            suggestionsOpen && activeSuggestionIndex >= 0
                                ? `customer-destination-suggestion-${activeSuggestionIndex}`
                                : undefined
                        }
                        value={destination}
                        onChange={(event) =>
                            onDestinationInputChange(event.target.value)
                        }
                        onFocus={() => {
                            if (
                                suggestions.length > 0 ||
                                suggestionsBusy ||
                                suggestionsError
                            ) {
                                setSuggestionsOpen(true);
                            }
                        }}
                        onKeyDown={onDestinationKeyDown}
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
                                        void selectSuggestion(suggestion)
                                    }
                                >
                                    <CustomerIcon name="location" size={17} />
                                    <span>
                                        <strong>{suggestion.name}</strong>
                                        {suggestion.place && (
                                            <small>{suggestion.place}</small>
                                        )}
                                    </span>
                                </button>
                            ))
                        )}
                        <span className="customer-search-suggestions-credit">
                            {copy("Search by Mapbox", "Tìm kiếm bằng Mapbox")}
                        </span>
                    </div>
                )}
            </div>

            <div className="customer-search-field customer-search-distance-field">
                <CustomerIcon name="distance" size={21} />
                <CustomerSearchDropdown
                    label={copy("Distance", "Khoảng cách")}
                    value={distanceChoice}
                    options={[
                        {
                            value: "any",
                            label: copy("Any distance", "Mọi khoảng cách"),
                        },
                        ...["1", "2", "5", "10", "20"].map((value) => ({
                            value,
                            label: distanceLabels[value],
                        })),
                        {
                            value: "custom",
                            label: copy("Custom distance", "Tùy chỉnh"),
                        },
                    ]}
                    onChange={onDistanceChoiceChange}
                    variant="field"
                />
                {distanceNeedsOrigin && !searchOrigin && (
                    <p className="customer-search-distance-hint" role="status">
                        {copy(
                            "Choose a destination or use your location first.",
                            "Chọn điểm đến hoặc dùng vị trí hiện tại trước.",
                        )}
                    </p>
                )}
            </div>

            <label className="customer-search-field customer-search-ev-field">
                <CustomerIcon name="zap" size={21} />
                <span>
                    <strong>{copy("EV charging", "Trạm sạc EV")}</strong>
                    <span>{copy("Charging station", "Có trạm sạc")}</span>
                </span>
                <input
                    type="checkbox"
                    role="switch"
                    aria-label={copy("EV charging", "Trạm sạc EV")}
                    checked={filters.evChargingOnly}
                    onChange={(event) =>
                        setFilters((current) => ({
                            ...current,
                            evChargingOnly: event.target.checked,
                        }))
                    }
                />
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
                        ? copy("Searching map…", "Đang tìm trên bản đồ…")
                        : copy("Find Parking", "Tìm bãi đỗ")}
                </span>
                <CustomerIcon name="arrow" size={19} />
            </button>
        </form>
    );
}
