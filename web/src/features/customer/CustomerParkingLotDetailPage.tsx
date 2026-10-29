"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ArrowLeft } from "lucide-react";
import { CustomerFooter, CustomerHeader } from "./CustomerShell";
import { ParkingLotDetailExperience } from "./ParkingLotDetailExperience";
import {
    getApprovedParkingLot,
    ParkingLotNotFoundError,
    type ParkingLotDetail,
} from "./parkingLotApi";

const typeLabels: Record<string, string> = {
    MultiStorey: "Multi-storey parking",
    Underground: "Underground parking",
    Outdoor: "Outdoor parking",
};

export function CustomerParkingLotDetailPage({
    id,
    fontClassName,
}: {
    id: string;
    fontClassName: string;
}) {
    const [lot, setLot] = useState<ParkingLotDetail | null>(null);
    const [state, setState] = useState<
        "loading" | "ready" | "not-found" | "error"
    >("loading");
    const validId =
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
            id,
        );

    useEffect(() => {
        if (!validId) return;
        const controller = new AbortController();
        getApprovedParkingLot(id, controller.signal)
            .then((result) => {
                setLot(result);
                setState("ready");
            })
            .catch((error: unknown) => {
                if (controller.signal.aborted) return;
                setState(
                    error instanceof ParkingLotNotFoundError
                        ? "not-found"
                        : "error",
                );
            });
        return () => controller.abort();
    }, [id, validId]);

    if (state === "ready" && lot) {
        return (
            <ParkingLotDetailExperience
                fontClassName={fontClassName}
                lot={{
                    id: lot.id,
                    name: lot.name,
                    address: lot.address,
                    type: typeLabels[lot.type] ?? lot.type,
                    image: "/images/parkwise/dashboard-parking-structure.webp",
                    photos: [
                        "/images/parkwise/dashboard-parking-structure.webp",
                    ],
                    isPreview: false,
                    latitude: lot.latitude,
                    longitude: lot.longitude,
                    openingHours: lot.openingHours?.days,
                }}
            />
        );
    }

    const visibleState = validId ? state : "not-found";
    return (
        <div
            lang="en"
            className={`parkwise-page customer-shell-page lot-detail-page ${fontClassName}`}
        >
            <CustomerHeader active="detail" />
            <main className="parkwise-container lot-detail-main">
                <Link href="/search" className="lot-detail-bottom-back">
                    <ArrowLeft size={18} /> Back to Find Parking
                </Link>
                <div
                    className="lot-detail-state"
                    role={visibleState === "error" ? "alert" : "status"}
                >
                    {visibleState === "loading" ? (
                        <p>Loading parking details…</p>
                    ) : visibleState === "not-found" ? (
                        <>
                            <h1>Parking lot not found</h1>
                            <p>
                                This lot may be unavailable or is not approved
                                for public viewing.
                            </p>
                        </>
                    ) : (
                        <>
                            <h1>Parking details are unavailable</h1>
                            <p>Please try again later.</p>
                        </>
                    )}
                    {visibleState !== "loading" && (
                        <Link href="/search">Browse parking lots</Link>
                    )}
                </div>
            </main>
            <CustomerFooter />
        </div>
    );
}
