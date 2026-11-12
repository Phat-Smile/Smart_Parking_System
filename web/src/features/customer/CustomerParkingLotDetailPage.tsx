"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ArrowLeft } from "lucide-react";
import { CustomerFooter, CustomerHeader } from "./CustomerShell";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { ParkingLotDetailExperience } from "./ParkingLotDetailExperience";
import {
    describeLotAvailability,
    describeLotPricing,
    estimateLotBookingPricing,
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
    const { language, copy } = useCustomerLanguage();
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
                    type: copy(
                        typeLabels[lot.type] ?? lot.type,
                        (
                            {
                                MultiStorey: "Bãi đỗ nhiều tầng",
                                Underground: "Bãi đỗ ngầm",
                                Outdoor: "Bãi đỗ ngoài trời",
                            } as Record<string, string>
                        )[lot.type] ?? lot.type,
                    ),
                    image: "/images/parkwise/dashboard-parking-structure.webp",
                    photos: [
                        "/images/parkwise/dashboard-parking-structure.webp",
                    ],
                    isPreview: false,
                    latitude: lot.latitude,
                    longitude: lot.longitude,
                    openingHours: lot.openingHours?.days,
                    pricing: lot.pricing,
                    hourlyPrice: estimateLotBookingPricing(
                        lot.pricing,
                        1,
                        "CAR",
                    )?.rateValue,
                    priceText: describeLotPricing(lot.pricing),
                    spacesText: describeLotAvailability(lot.availability),
                }}
            />
        );
    }

    const visibleState = validId ? state : "not-found";
    return (
        <div
            lang={language}
            className={`parkwise-page customer-shell-page lot-detail-page ${fontClassName}`}
        >
            <CustomerHeader active="detail" />
            <main className="parkwise-container lot-detail-main">
                <Link href="/search" className="lot-detail-bottom-back">
                    <ArrowLeft size={18} />{" "}
                    {copy("Back to Find Parking", "Quay lại tìm bãi đỗ")}
                </Link>
                <div
                    className="lot-detail-state"
                    role={visibleState === "error" ? "alert" : "status"}
                >
                    {visibleState === "loading" ? (
                        <p>
                            {copy(
                                "Loading parking details…",
                                "Đang tải thông tin bãi đỗ…",
                            )}
                        </p>
                    ) : visibleState === "not-found" ? (
                        <>
                            <h1>
                                {copy(
                                    "Parking lot not found",
                                    "Không tìm thấy bãi đỗ",
                                )}
                            </h1>
                            <p>
                                {copy(
                                    "This lot may be unavailable or is not approved for public viewing.",
                                    "Bãi đỗ có thể không còn hoạt động hoặc chưa được duyệt để hiển thị.",
                                )}
                            </p>
                        </>
                    ) : (
                        <>
                            <h1>
                                {copy(
                                    "Parking details are unavailable",
                                    "Chưa thể xem thông tin bãi đỗ",
                                )}
                            </h1>
                            <p>
                                {copy(
                                    "Please try again later.",
                                    "Vui lòng thử lại sau.",
                                )}
                            </p>
                        </>
                    )}
                    {visibleState !== "loading" && (
                        <Link href="/search">
                            {copy("Browse parking lots", "Xem các bãi đỗ")}
                        </Link>
                    )}
                </div>
            </main>
            <CustomerFooter />
        </div>
    );
}
