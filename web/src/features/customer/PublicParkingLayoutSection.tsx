"use client";

import { useEffect, useState } from "react";
import { getGatewayBaseUrl } from "@/shared/config/env";
import { useCustomerLanguage } from "./i18n/customerLanguage";

type PublicSlot = { id: string; slotCode: string; effectiveStatus: string };
type PublicZone = {
    id: string;
    code: string;
    vehicleGroup: string;
    capacity: number;
    currentVehicleCount: number;
    slots: PublicSlot[];
};
type PublicFloor = { id: string; name: string; zones: PublicZone[] };
type PublicLayout = { lotId: string; version: number; floors: PublicFloor[] };

// Public-only layout: NEVER use mock preview spots for a real lot.
// This component shows floor, zone and slot summaries from the actual layout API.
export function PublicParkingLayoutSection({ lotId }: { lotId: string }) {
    const { copy } = useCustomerLanguage();
    const [result, setResult] = useState<{
        lotId: string;
        state: "ready" | "error";
        layout: PublicLayout | null;
    } | null>(null);
    // A changed lotId must show loading, never the previous lot's layout.
    const state = result?.lotId === lotId ? result.state : "loading";
    const layout = result?.lotId === lotId ? result.layout : null;

    useEffect(() => {
        const controller = new AbortController();
        fetch(
            `${getGatewayBaseUrl()}/api/parking/lots/${encodeURIComponent(lotId)}/layout`,
            {
                signal: controller.signal,
                cache: "no-store",
            },
        )
            .then(async (response) => {
                if (!response.ok)
                    throw new Error(`Layout HTTP ${response.status}`);
                return response.json() as Promise<PublicLayout>;
            })
            .then((data) => {
                if (controller.signal.aborted) return;
                setResult({ lotId, state: "ready", layout: data });
            })
            .catch(() => {
                if (!controller.signal.aborted) {
                    setResult({ lotId, state: "error", layout: null });
                }
            });
        return () => controller.abort();
    }, [lotId]);

    return (
        <section
            className="lot-detail-layout-section"
            aria-label={copy("Live parking layout", "Sơ đồ bãi hiện tại")}
        >
            <div className="lot-detail-layout-heading">
                <div>
                    <h2>
                        {copy("Current parking layout", "Sơ đồ bãi hiện tại")}
                    </h2>
                    <p>
                        {copy(
                            "Current zone and space counts; individual spaces are assigned at the lot.",
                            "Số chỗ và khu vực hiện tại; nhân viên bãi sẽ phân ô khi xe đến.",
                        )}
                    </p>
                </div>
            </div>
            {state === "loading" && (
                <p>{copy("Loading layout…", "Đang tải sơ đồ bãi…")}</p>
            )}
            {state === "error" && (
                <p role="status">
                    {copy("Layout unavailable.", "Chưa thể tải sơ đồ bãi.")}
                </p>
            )}
            {state === "ready" && layout?.floors.length === 0 && (
                <p>
                    {copy(
                        "No floor layout published.",
                        "Chưa công bố sơ đồ tầng.",
                    )}
                </p>
            )}
            {state === "ready" &&
                layout?.floors.map((floor) => (
                    <div
                        key={floor.id}
                        style={{
                            padding: 14,
                            marginTop: 10,
                            border: "1px solid #dce3ed",
                            borderRadius: 12,
                        }}
                    >
                        <h3>{floor.name}</h3>
                        {floor.zones.map((zone) => (
                            <div
                                key={zone.id}
                                style={{
                                    marginTop: 8,
                                    padding: 8,
                                    background: "#f6f8fc",
                                    borderRadius: 8,
                                }}
                            >
                                <strong>
                                    {zone.code} · {zone.vehicleGroup}
                                </strong>
                                <p>
                                    {copy(
                                        "Declared capacity",
                                        "Sức chứa khai báo",
                                    )}
                                    : {zone.capacity}
                                </p>
                                {zone.vehicleGroup.toLowerCase() ===
                                    "motorbike" && (
                                    <p>
                                        {copy(
                                            "Currently occupied",
                                            "Đang sử dụng",
                                        )}
                                        : {zone.currentVehicleCount}
                                    </p>
                                )}
                                {zone.slots.length > 0 && (
                                    <ul
                                        style={{
                                            display: "flex",
                                            flexWrap: "wrap",
                                            gap: 7,
                                            padding: 0,
                                            listStyle: "none",
                                        }}
                                    >
                                        {zone.slots.map((slot) => (
                                            <li
                                                key={slot.id}
                                                style={{
                                                    border: "1px solid #d6dce6",
                                                    padding: "4px 8px",
                                                    borderRadius: 6,
                                                }}
                                            >
                                                {slot.slotCode}:{" "}
                                                {slot.effectiveStatus}
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>
                        ))}
                    </div>
                ))}
        </section>
    );
}
