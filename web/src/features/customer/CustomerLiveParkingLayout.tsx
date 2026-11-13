"use client";

import { useEffect, useState } from "react";
import { ParkingTwin } from "./components/ParkingTwin";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import {
    getPublicParkingLayout,
    getPublicZoneUtilization,
    type ParkingLayout,
    type ZoneUtilization,
} from "./parkingLayoutApi";

type LayoutState =
    | { kind: "loading" }
    | { kind: "error" }
    | {
          kind: "ready";
          layout: ParkingLayout;
          usage: ZoneUtilization;
          loadedAt: Date;
      };

const REFRESH_MS = 15_000;

function floorTitle(name: string, fallback: string) {
    return name.includes("??") ? fallback : name;
}

export function CustomerLiveParkingLayout({ lotId }: { lotId: string }) {
    const { copy } = useCustomerLanguage();
    const [state, setState] = useState<LayoutState>({ kind: "loading" });
    const [floorId, setFloorId] = useState<string | null>(null);

    useEffect(() => {
        let controller = new AbortController();
        const load = () => {
            controller.abort();
            controller = new AbortController();
            const signal = controller.signal;
            Promise.all([
                getPublicParkingLayout(lotId, signal),
                getPublicZoneUtilization(lotId, signal),
            ])
                .then(([layout, usage]) => {
                    if (!signal.aborted)
                        setState({
                            kind: "ready",
                            layout,
                            usage,
                            loadedAt: new Date(),
                        });
                })
                .catch(() => {
                    // Keep the last good snapshot when a refresh fails.
                    if (!signal.aborted)
                        setState((current) =>
                            current.kind === "ready"
                                ? current
                                : { kind: "error" },
                        );
                });
        };
        load();
        const timer = window.setInterval(load, REFRESH_MS);
        return () => {
            window.clearInterval(timer);
            controller.abort();
        };
    }, [lotId]);

    const floors = state.kind === "ready" ? state.layout.floors : [];
    const activeFloor =
        floors.find((floor) => floor.id === floorId) ?? floors[0] ?? null;

    return (
        <section
            className="lot-detail-layout-section"
            aria-labelledby="live-layout-title"
        >
            <div className="lot-detail-layout-heading">
                <div>
                    <h2 id="live-layout-title">
                        {copy(
                            "Current parking layout",
                            "Sơ đồ bãi đỗ hiện tại",
                        )}
                    </h2>
                    <p>
                        {copy(
                            "Live space status is for guidance. Parking staff assign the final space.",
                            "Tình trạng chỗ trống chỉ để tham khảo. Nhân viên bãi sẽ xác nhận vị trí cuối cùng.",
                        )}
                    </p>
                </div>
                {floors.length > 1 && (
                    <div
                        className="lot-detail-layout-switch"
                        role="group"
                        aria-label={copy("Floor", "Tầng")}
                    >
                        {floors.map((floor) => (
                            <button
                                key={floor.id}
                                type="button"
                                className={
                                    floor.id === activeFloor?.id ? "active" : ""
                                }
                                aria-pressed={floor.id === activeFloor?.id}
                                onClick={() => setFloorId(floor.id)}
                            >
                                {floorTitle(floor.name, copy("Floor", "Tầng"))}
                            </button>
                        ))}
                    </div>
                )}
            </div>
            {state.kind === "loading" && (
                <p role="status">
                    {copy(
                        "Loading parking layout...",
                        "Đang tải sơ đồ bãi đỗ...",
                    )}
                </p>
            )}
            {state.kind === "error" && (
                <p role="alert">
                    {copy(
                        "The parking layout is unavailable right now.",
                        "Hiện không thể tải sơ đồ bãi đỗ.",
                    )}
                </p>
            )}
            {state.kind === "ready" && !activeFloor && (
                <p>
                    {copy(
                        "This lot has no layout data yet.",
                        "Bãi này chưa có dữ liệu sơ đồ.",
                    )}
                </p>
            )}
            {state.kind === "ready" && activeFloor && (
                <div className="lot-twin-card">
                    <ParkingTwin
                        floor={activeFloor}
                        usage={state.usage}
                        copy={copy}
                    />
                    <div className="lot-twin-footer">
                        <span className="lot-twin-legend">
                            <i className="free" /> {copy("Free", "Trống")}
                            <i className="taken" /> {copy("Taken", "Có xe")}
                            <i className="held" /> {copy("Held", "Đang giữ")}
                        </span>
                        <small>
                            {copy("Updated", "Cập nhật")}{" "}
                            {state.loadedAt.toLocaleTimeString()}
                        </small>
                    </div>
                </div>
            )}
        </section>
    );
}
