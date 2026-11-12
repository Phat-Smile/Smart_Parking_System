"use client";

import { useEffect, useState } from "react";
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
    | { kind: "ready"; layout: ParkingLayout; usage: ZoneUtilization };

function FloorGeometry({ floor }: { floor: ParkingLayout["floors"][number] }) {
    const points = floor.zones.flatMap((zone) => [
        ...zone.slots.flatMap((slot) => (slot.position ? [slot.position] : [])),
        ...(zone.polygon ?? []),
        ...(zone.bounds
            ? [
                  { x: zone.bounds.x, y: zone.bounds.y },
                  {
                      x: zone.bounds.x + zone.bounds.width,
                      y: zone.bounds.y + zone.bounds.height,
                  },
              ]
            : []),
    ]);
    if (points.length === 0) return null;
    const minX = Math.min(...points.map((point) => point.x));
    const maxX = Math.max(...points.map((point) => point.x));
    const minY = Math.min(...points.map((point) => point.y));
    const maxY = Math.max(...points.map((point) => point.y));
    const x = (value: number) =>
        5 + ((value - minX) / Math.max(1, maxX - minX)) * 90;
    const y = (value: number) =>
        5 + ((value - minY) / Math.max(1, maxY - minY)) * 90;

    return (
        <div
            role="group"
            aria-label={`Parking layout of ${floor.name}`}
            style={{
                position: "relative",
                height: 340,
                borderRadius: 12,
                border: "1px solid #3c4054",
                background: "#111827",
                overflow: "hidden",
                marginBlock: 16,
            }}
        >
            {floor.zones.map(
                (zone) =>
                    zone.bounds && (
                        <div
                            key={zone.id}
                            aria-label={`Zone ${zone.code}`}
                            style={{
                                position: "absolute",
                                left: `${x(zone.bounds.x)}%`,
                                top: `${y(zone.bounds.y)}%`,
                                width: `${x(zone.bounds.x + zone.bounds.width) - x(zone.bounds.x)}%`,
                                height: `${y(zone.bounds.y + zone.bounds.height) - y(zone.bounds.y)}%`,
                                border: "1px dashed #8b80c8",
                                borderRadius: 8,
                                color: "#d8d2ff",
                                fontSize: 12,
                                padding: 4,
                            }}
                        >
                            {zone.code}
                        </div>
                    ),
            )}
            {floor.zones.flatMap((zone) =>
                zone.slots
                    .filter((slot) => slot.position)
                    .map((slot) => (
                        <span
                            key={slot.id}
                            title={`${zone.code} · ${slot.slotCode}: ${slot.effectiveStatus}`}
                            aria-label={`${zone.code} space ${slot.slotCode}, ${slot.effectiveStatus}`}
                            style={{
                                position: "absolute",
                                left: `${x(slot.position!.x)}%`,
                                top: `${y(slot.position!.y)}%`,
                                transform: "translate(-50%, -50%)",
                                padding: "5px 7px",
                                borderRadius: 6,
                                border: "1px solid #a5aabc",
                                fontSize: 11,
                                background:
                                    slot.effectiveStatus.toLowerCase() ===
                                    "available"
                                        ? "#153e33"
                                        : "#40313c",
                                color: "#fff",
                            }}
                        >
                            {slot.slotCode}
                        </span>
                    )),
            )}
        </div>
    );
}

export function CustomerLiveParkingLayout({ lotId }: { lotId: string }) {
    const { copy } = useCustomerLanguage();
    const [state, setState] = useState<LayoutState>({ kind: "loading" });
    const [view, setView] = useState<"2d" | "3d">("2d");

    useEffect(() => {
        const controller = new AbortController();
        Promise.all([
            getPublicParkingLayout(lotId, controller.signal),
            getPublicZoneUtilization(lotId, controller.signal),
        ])
            .then(([layout, usage]) => {
                if (!controller.signal.aborted)
                    setState({ kind: "ready", layout, usage });
            })
            .catch(() => {
                if (!controller.signal.aborted) setState({ kind: "error" });
            });
        return () => controller.abort();
    }, [lotId]);

    return (
        <section
            className="lot-detail-layout-section"
            aria-labelledby="live-layout-title"
        >
            <div className="lot-detail-layout-heading">
                <div>
                    <h2 id="live-layout-title">{copy("Current parking layout", "Sơ đồ bãi đỗ hiện tại")}</h2>
                    <p>
                        {copy("Live space status is for guidance. Parking staff assign the final space.",
                            "Tình trạng chỗ trống chỉ để tham khảo. Nhân viên bãi sẽ xác nhận vị trí cuối cùng.")}
                    </p>
                </div>
            </div>
            <div className="lot-detail-layout-switch" role="group" aria-label={copy("Layout view", "Kiểu xem sơ đồ")}>
                <button type="button" className={view === "2d" ? "active" : ""}
                    aria-pressed={view === "2d"} onClick={() => setView("2d")}>2D</button>
                <button type="button" className={view === "3d" ? "active" : ""}
                    aria-pressed={view === "3d"} onClick={() => setView("3d")}>3D</button>
            </div>
            {state.kind === "loading" && (
                <p role="status">{copy("Loading parking layout...", "Đang tải sơ đồ bãi đỗ...")}</p>
            )}
            {state.kind === "error" && (
                <p role="alert">{copy("The parking layout is unavailable right now.", "Hiện không thể tải sơ đồ bãi đỗ.")}</p>
            )}
            {state.kind === "ready" && state.layout.floors.length === 0 && (
                <p>{copy("This lot has no layout data yet.", "Bãi này chưa có dữ liệu sơ đồ.")}</p>
            )}
            {state.kind === "ready" &&
                state.layout.floors.map((floor) => (
                    <div key={floor.id} style={{ marginBlock: 20 }}>
                        <h3>{floor.name.includes("??")
                            ? copy("Floor name unavailable", "Chưa có tên tầng") : floor.name}</h3>
                        {floor.zones.some((zone) => zone.bounds || zone.polygon?.length || zone.slots.some((slot) => slot.position))
                            ? <div className={view === "3d" ? "lot-detail-layout-perspective" : undefined}>
                                <FloorGeometry floor={floor} />
                              </div>
                            : <div className="lot-detail-layout-unavailable" role="status">
                                <p>{copy("Schematic capacity view. Zone positions and 3D geometry are not available from this lot's API.",
                                    "Sơ đồ sức chứa minh họa. API của bãi chưa có vị trí khu vực và hình học 3D.")}</p>
                                <div className={`lot-detail-capacity-schematic ${view === "3d" ? "is-perspective" : ""}`}>
                                    {floor.zones.map((zone) => {
                                        const utilization = state.usage.zones.find((item) => item.zoneId === zone.id);
                                        const capacity = utilization?.usableCapacity ?? zone.capacity;
                                        const available = utilization?.available ?? Math.max(0, capacity - zone.currentVehicleCount);
                                        return <div className="lot-detail-capacity-zone" key={zone.id}>
                                            <strong>{zone.code}</strong>
                                            <span>{zone.vehicleGroup}</span>
                                            <div className="lot-detail-capacity-track"><i style={{ width: `${capacity > 0 ? Math.min(100, available / capacity * 100) : 0}%` }} /></div>
                                            <small>{available} / {capacity} {copy("available", "chỗ trống")}</small>
                                        </div>;
                                    })}
                                </div>
                              </div>}
                        {floor.zones.some((zone) => zone.bounds || zone.polygon?.length || zone.slots.some((slot) => slot.position)) && <div
                            style={{
                                display: "grid",
                                gap: 16,
                                gridTemplateColumns:
                                    "repeat(auto-fit, minmax(220px, 1fr))",
                            }}
                        >
                            {floor.zones.map((zone) => {
                                const utilization = state.usage.zones.find(
                                    (item) => item.zoneId === zone.id,
                                );
                                const slots = [...zone.slots].sort(
                                    (a, b) =>
                                        (a.position?.y ?? 0) -
                                            (b.position?.y ?? 0) ||
                                        (a.position?.x ?? 0) -
                                            (b.position?.x ?? 0),
                                );
                                return (
                                    <div
                                        key={zone.id}
                                        style={{
                                            border: "1px solid #3c4054",
                                            borderRadius: 12,
                                            padding: 16,
                                        }}
                                    >
                                        <h4>
                                            {zone.code} · {zone.vehicleGroup}
                                        </h4>
                                        <p>
                                            {utilization?.available ??
                                                Math.max(
                                                    0,
                                                    zone.capacity -
                                                        zone.currentVehicleCount,
                                                )}{" "}
                                            available of{" "}
                                            {utilization?.usableCapacity ??
                                                zone.capacity}
                                        </p>
                                        {slots.length > 0 && (
                                            <div
                                                style={{
                                                    display: "grid",
                                                    gridTemplateColumns:
                                                        "repeat(auto-fill, minmax(72px, 1fr))",
                                                    gap: 8,
                                                }}
                                            >
                                                {slots.map((slot) => (
                                                    <span
                                                        key={slot.id}
                                                        title={
                                                            slot.position
                                                                ? `Position ${slot.position.x}, ${slot.position.y}`
                                                                : undefined
                                                        }
                                                        style={{
                                                            padding: 8,
                                                            borderRadius: 8,
                                                            border: "1px solid #555b70",
                                                            background:
                                                                slot.effectiveStatus.toLowerCase() ===
                                                                "available"
                                                                    ? "#153e33"
                                                                    : "#40313c",
                                                        }}
                                                    >
                                                        {slot.slotCode}
                                                        <small
                                                            style={{
                                                                display:
                                                                    "block",
                                                            }}
                                                        >
                                                            {
                                                                slot.effectiveStatus
                                                            }
                                                        </small>
                                                    </span>
                                                ))}
                                            </div>
                                        )}
                                    </div>
                                );
                            })}
                        </div>}
                    </div>
                ))}
        </section>
    );
}
