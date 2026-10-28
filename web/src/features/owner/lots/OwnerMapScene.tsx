"use client";

import React, { useEffect } from "react";
import Map, { Marker, Popup, NavigationControl } from "react-map-gl/mapbox";
import { useAllLots } from "./useOwnerMapStore";
import { getLotCoordinates } from "./lotGeo";
import { LotPin } from "./LotPin";
import { useLotInfo, formatVnd, formatDateVi } from "./lotInfo";

interface OwnerMapSceneProps {
    token: string;
    selectedLotId: number | null;
    onSelectLot: (id: number | null) => void;
    lots: ReturnType<typeof useAllLots>;
}

export default function OwnerMapScene({
    token,
    selectedLotId,
    onSelectLot,
    lots,
}: OwnerMapSceneProps) {
    const selectedLot = lots.find((l) => l.id === selectedLotId);
    const selectedLotInfo = useLotInfo(selectedLotId);

    const activeCoords = selectedLotId
        ? getLotCoordinates(selectedLotId)
        : [106.705, 10.76];

    // Esc key listener to close popup
    useEffect(() => {
        function handleKeyDown(e: KeyboardEvent) {
            if (e.key === "Escape" && selectedLotId) {
                onSelectLot(null);
            }
        }
        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [selectedLotId, onSelectLot]);

    // Status badge colors
    const statusBg =
        selectedLot?.ap === "approved"
            ? "#def7ec"
            : selectedLot?.ap === "pending"
              ? "#fef3c7"
              : "#fde8e8";
    const statusColor =
        selectedLot?.ap === "approved"
            ? "#0e9f6e"
            : selectedLot?.ap === "pending"
              ? "#b45309"
              : "#e5484d";
    const statusText =
        selectedLot?.ap === "approved"
            ? "Đang hoạt động"
            : selectedLot?.ap === "pending"
              ? "Chờ duyệt"
              : "Bị từ chối";

    // Date line for popup
    let dateText = "";
    if (selectedLot?.ap === "approved") {
        dateText = `Hoạt động từ ${formatDateVi(selectedLotInfo?.openedAt)}`;
    } else if (selectedLot?.ap === "pending") {
        dateText = `Gửi hồ sơ ngày ${formatDateVi(selectedLotInfo?.submittedAt)}`;
    } else if (selectedLot?.ap === "rejected") {
        dateText = `Bị từ chối ngày ${formatDateVi(selectedLotInfo?.rejectedAt)}`;
    }

    const [mapType, setMapType] = React.useState<"streets" | "satellite">(
        "streets",
    );

    return (
        <div style={{ position: "relative", width: "100%", height: "100%" }}>
            {/* Google Maps Top-Right Map/Satellite Switcher */}
            <div
                style={{
                    position: "absolute",
                    top: 14,
                    right: 14,
                    zIndex: 10,
                    display: "flex",
                    background: "#ffffff",
                    borderRadius: 8,
                    boxShadow: "0 2px 6px rgba(0,0,0,0.3)",
                    overflow: "hidden",
                    border: "1px solid #dadce0",
                }}
            >
                <button
                    type="button"
                    onClick={() => setMapType("streets")}
                    style={{
                        padding: "7px 14px",
                        fontSize: 12,
                        fontWeight: 700,
                        border: "none",
                        background:
                            mapType === "streets" ? "#ffffff" : "#f8f9fa",
                        color: mapType === "streets" ? "#1a73e8" : "#5f6368",
                        cursor: "pointer",
                        borderRight: "1px solid #dadce0",
                    }}
                >
                    Bản đồ
                </button>
                <button
                    type="button"
                    onClick={() => setMapType("satellite")}
                    style={{
                        padding: "7px 14px",
                        fontSize: 12,
                        fontWeight: 700,
                        border: "none",
                        background:
                            mapType === "satellite" ? "#ffffff" : "#f8f9fa",
                        color: mapType === "satellite" ? "#1a73e8" : "#5f6368",
                        cursor: "pointer",
                    }}
                >
                    Vệ tinh
                </button>
            </div>

            <Map
                key={`${selectedLotId ? `lot-${selectedLotId}` : "default-map"}-${mapType}`}
                initialViewState={{
                    longitude: activeCoords[0],
                    latitude: activeCoords[1],
                    zoom: selectedLotId ? 15 : 13.5,
                }}
                mapboxAccessToken={token}
                style={{ width: "100%", height: "100%" }}
                mapStyle={
                    mapType === "streets"
                        ? "mapbox://styles/mapbox/streets-v12"
                        : "mapbox://styles/mapbox/satellite-streets-v12"
                }
                aria-label="Bản đồ vị trí các bãi đỗ xe"
                onClick={() => {
                    // Click outside closes the popup without jumping
                    if (selectedLotId) onSelectLot(null);
                }}
            >
                <NavigationControl position="bottom-right" />

                {lots.map((lot) => {
                    const coords = getLotCoordinates(lot.id);
                    const isSelected = selectedLotId === lot.id;
                    return (
                        <Marker
                            key={lot.id}
                            longitude={coords[0]}
                            latitude={coords[1]}
                            anchor="center"
                        >
                            <div onClick={(e) => e.stopPropagation()}>
                                <LotPin
                                    lot={lot}
                                    isSelected={isSelected}
                                    onClick={() =>
                                        onSelectLot(isSelected ? null : lot.id)
                                    }
                                />
                            </div>
                        </Marker>
                    );
                })}

                {/* Floating Small Card / Popup on selected lot */}
                {selectedLot && (
                    <Popup
                        longitude={activeCoords[0]}
                        latitude={activeCoords[1]}
                        anchor="bottom"
                        offset={24}
                        closeButton={true}
                        closeOnClick={false}
                        onClose={() => onSelectLot(null)}
                    >
                        <div
                            onClick={(e) => e.stopPropagation()}
                            role="dialog"
                            aria-label={`Thông tin nhanh bãi ${selectedLot.name}`}
                            style={{
                                padding: "6px 8px",
                                minWidth: 220,
                                maxWidth: 280,
                                fontFamily: "inherit",
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                    gap: 8,
                                    marginBottom: 6,
                                }}
                            >
                                <strong
                                    style={{ fontSize: 13.5, color: "#0f1b3d" }}
                                >
                                    {selectedLot.name}
                                </strong>
                                <span
                                    style={{
                                        fontSize: 11,
                                        padding: "2px 8px",
                                        borderRadius: 99,
                                        background: statusBg,
                                        color: statusColor,
                                        fontWeight: 700,
                                        whiteSpace: "nowrap",
                                    }}
                                >
                                    {statusText}
                                </span>
                            </div>

                            {dateText && (
                                <div
                                    style={{
                                        fontSize: 11.5,
                                        color: "#6a7491",
                                        marginBottom: 4,
                                    }}
                                >
                                    {dateText}
                                </div>
                            )}

                            <div
                                style={{
                                    fontSize: 12.5,
                                    fontWeight: 700,
                                    color: "#2563eb",
                                    marginBottom: 2,
                                }}
                            >
                                Ô tô từ{" "}
                                {formatVnd(
                                    selectedLotInfo?.prices?.carFirstHour,
                                )}
                                /giờ đầu
                            </div>

                            <div style={{ fontSize: 11.5, color: "#6a7491" }}>
                                Giờ mở cửa:{" "}
                                {selectedLotInfo?.openingHours ||
                                    `${selectedLot.op} – ${selectedLot.cl2}`}
                            </div>
                        </div>
                    </Popup>
                )}
            </Map>
        </div>
    );
}
