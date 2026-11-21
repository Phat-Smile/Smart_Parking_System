"use client";

import Link from "next/link";
import { CustomerIcon } from "../CustomerShell";
import { CustomerMap } from "../CustomerMap";
import { ArrivalTimeScrubber } from "../components/ArrivalTimeScrubber";
import { type MapCoordinates } from "../geocodeDestination";
import { type SearchResult } from "./useCustomerFindParking";

export type ForecastSlot = {
    hour: number;
    percentByLot: Record<string, number> | null;
};

export type CustomerSearchMapColumnProps = {
    loadEnabled: boolean;
    activeSelectedId: string;
    onSelectId: (id: string) => void;
    currentLocation: { longitude: number; latitude: number } | null;
    destinationLocation: MapCoordinates | null;
    visibleParkings: SearchResult[];
    slotPercentByLot: Record<string, number> | null;
    searchOrigin:
        MapCoordinates | { longitude: number; latitude: number } | null;
    forecastSlots: ForecastSlot[];
    forecastLoading: boolean;
    arrivalHour: number;
    onArrivalHourChange: (hour: number) => void;
    copy: (en: string, vi: string) => string;
};

export function CustomerSearchMapColumn({
    loadEnabled,
    activeSelectedId,
    onSelectId,
    currentLocation,
    destinationLocation,
    visibleParkings,
    slotPercentByLot,
    searchOrigin,
    forecastSlots,
    forecastLoading,
    arrivalHour,
    onArrivalHourChange,
    copy,
}: CustomerSearchMapColumnProps) {
    return (
        <div className="customer-search-map-column">
            <div className="customer-search-map-header">
                <strong>{copy("Parking map", "Bản đồ bãi đỗ")}</strong>
                <span>{copy("Ho Chi Minh City", "Thành phố Hồ Chí Minh")}</span>
            </div>
            <CustomerMap
                loadEnabled={loadEnabled}
                selectedId={activeSelectedId}
                onSelect={onSelectId}
                currentLocation={currentLocation}
                destinationLocation={destinationLocation}
                lots={visibleParkings}
                percentByLot={slotPercentByLot}
            />
            {searchOrigin && (
                <ArrivalTimeScrubber
                    slots={forecastSlots.map((slot) => {
                        const values = slot.percentByLot
                            ? Object.values(slot.percentByLot)
                            : [];
                        const selectedValue =
                            slot.percentByLot?.[activeSelectedId];
                        const average = values.length
                            ? Math.round(
                                  values.reduce((a, b) => a + b, 0) /
                                      values.length,
                              )
                            : null;
                        return {
                            hour: slot.hour,
                            percent: selectedValue ?? average,
                        };
                    })}
                    selectedHour={arrivalHour}
                    onChange={onArrivalHourChange}
                    loading={forecastLoading}
                    copy={copy}
                />
            )}
            <div className="customer-search-map-footer">
                <span>
                    <i className="customer-search-map-dot" />{" "}
                    {copy("Approved parking lot", "Bãi đỗ đã duyệt")}
                </span>
                <Link href="/customer/vehicles">
                    {copy("Manage vehicles", "Quản lý xe")}{" "}
                    <CustomerIcon name="arrow" size={15} />
                </Link>
            </div>
        </div>
    );
}

export function CustomerSearchMapSkeleton() {
    return (
        <div
            className="customer-search-map-column customer-search-map-skeleton"
            style={{
                minHeight: "520px",
                display: "flex",
                flexDirection: "column",
                borderRadius: "16px",
                background:
                    "var(--color-bg-secondary, rgba(255, 255, 255, 0.03))",
                border: "1px solid var(--color-border-subtle, rgba(255, 255, 255, 0.08))",
                padding: "1rem",
                animation: "pulse 1.5s infinite ease-in-out",
            }}
            aria-busy="true"
        >
            <div
                style={{
                    height: "36px",
                    width: "55%",
                    borderRadius: "8px",
                    background: "rgba(255, 255, 255, 0.06)",
                    marginBottom: "1rem",
                }}
            />
            <div
                style={{
                    flex: 1,
                    minHeight: "380px",
                    borderRadius: "12px",
                    background: "rgba(255, 255, 255, 0.04)",
                }}
            />
        </div>
    );
}
