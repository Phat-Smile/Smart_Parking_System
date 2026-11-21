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
                    image:
                        lot.publicInfo?.photoUrls?.[0] ??
                        "/images/parkwise/dashboard-parking-structure.webp",
                    // Do not count the illustrative fallback image as a published photo.
                    photos: lot.publicInfo?.photoUrls ?? [],
                    photoIsIllustrative:
                        lot.publicInfo?.photosAreIllustrative ??
                        !lot.publicInfo?.photoUrls?.length,
                    isPreview: false,
                    latitude: lot.latitude,
                    longitude: lot.longitude,
                    openingHours: lot.openingHours?.days,
                    pricing: lot.pricing,
                    // Only CAR pricing may be used as the car booking fallback.
                    hourlyPrice: estimateLotBookingPricing(
                        lot.pricing?.filter(
                            (price) =>
                                price.vehicleType.toUpperCase() === "CAR",
                        ),
                        1,
                        "CAR",
                    )?.rateValue,
                    priceText: describeLotPricing(lot.pricing),
                    spacesText: describeLotAvailability(lot.availability),
                    compatibilityDeclared: Array.isArray(lot.compatibility),
                    compatible: lot.compatibility,
                    amenities: lot.amenities,
                    prebookable: lot.prebookable ?? false,
                    bookableVehicleTypes: lot.bookableVehicleTypes ?? [],
                    description: lot.publicInfo?.description ?? undefined,
                    entryInstructions:
                        lot.publicInfo?.entryInstructions ?? undefined,
                    rules: lot.publicInfo?.rules,
                    paymentMethods: lot.publicInfo?.paymentMethods,
                    evChargingPoints: lot.publicInfo?.evChargingPoints,
                    floorCount: lot.floorCount ?? undefined,
                    carCapacity: lot.carCapacity ?? undefined,
                    motorbikeCapacity: lot.motorbikeCapacity ?? undefined,
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
                    {copy("Quay lại tìm bãi đỗ", "Quay lại tìm bãi đỗ")}
                </Link>
                <div
                    className="lot-detail-state"
                    role={visibleState === "error" ? "alert" : "status"}
                >
                    {visibleState === "loading" ? (
                        <p>
                            {copy(
                                "Đang tải thông tin bãi đỗ…",
                                "Đang tải thông tin bãi đỗ…",
                            )}
                        </p>
                    ) : visibleState === "not-found" ? (
                        <>
                            <h1>
                                {copy(
                                    "Không tìm thấy bãi đỗ",
                                    "Không tìm thấy bãi đỗ",
                                )}
                            </h1>
                            <p>
                                {copy(
                                    "Bãi đỗ có thể không còn hoạt động hoặc chưa được duyệt để hiển thị.",
                                    "Bãi đỗ có thể không còn hoạt động hoặc chưa được duyệt để hiển thị.",
                                )}
                            </p>
                        </>
                    ) : (
                        <>
                            <h1>
                                {copy(
                                    "Chưa thể xem thông tin bãi đỗ",
                                    "Chưa thể xem thông tin bãi đỗ",
                                )}
                            </h1>
                            <p>
                                {copy(
                                    "Vui lòng thử lại sau.",
                                    "Vui lòng thử lại sau.",
                                )}
                            </p>
                        </>
                    )}
                    {visibleState !== "loading" && (
                        <Link href="/search">
                            {copy("Xem các bãi đỗ", "Xem các bãi đỗ")}
                        </Link>
                    )}
                </div>
            </main>
            <CustomerFooter />
        </div>
    );
}
