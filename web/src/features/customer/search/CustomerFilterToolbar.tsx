"use client";

import { CustomerSearchDropdown } from "../CustomerSearchDropdown";
import { type MapCoordinates } from "../geocodeDestination";
import { filterOptions, type FilterState } from "./useCustomerFindParking";

export type CustomerFilterToolbarProps = {
    filters: FilterState;
    setFilters: React.Dispatch<React.SetStateAction<FilterState>>;
    clearDistanceFilter: () => void;
    visibleParkingsCount: number;
    searchOrigin:
        MapCoordinates | { longitude: number; latitude: number } | null;
    distanceLabels: Record<string, string>;
    copy: (en: string, vi: string) => string;
};

export function CustomerFilterToolbar({
    filters,
    setFilters,
    clearDistanceFilter,
    visibleParkingsCount,
    searchOrigin,
    distanceLabels,
    copy,
}: CustomerFilterToolbarProps) {
    return (
        <div className="customer-search-toolbar">
            {(filters.distance !== "any" || filters.evChargingOnly) && (
                <div className="customer-search-toolbar-header">
                    <div className="customer-search-filter-summary">
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
                </div>
            )}
            <div className="customer-search-results-toolbar">
                <span className="customer-search-preview-count">
                    {visibleParkingsCount}{" "}
                    {copy(
                        visibleParkingsCount === 1
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
                                    searchOrigin || option.value !== "distance",
                            )
                            .map((option) => ({
                                ...option,
                                label:
                                    option.value === "recommended"
                                        ? copy("Best match", "Phù hợp nhất")
                                        : option.value === "name"
                                          ? copy("Name A–Z", "Tên A–Z")
                                          : option.value === "price"
                                            ? copy(
                                                  "Lowest price",
                                                  "Giá thấp nhất",
                                              )
                                            : copy("Nearest", "Gần nhất"),
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
    );
}
