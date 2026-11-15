"use client";

import React, { useState, useEffect, useRef } from "react";
import dynamic from "next/dynamic";
import {
    MapPin,
    Search,
    Loader2,
    Crosshair,
    Maximize2,
    Minimize2,
    AlertCircle,
    CheckCircle2,
    Info,
} from "lucide-react";
import { getPublicMapboxToken } from "@/shared/config/env";
import {
    resolveOwnerLotAddress,
    type GeocodedDestination,
} from "../api/ownerApi";
import { ApiError } from "@/shared/errors/api-error";
import "mapbox-gl/dist/mapbox-gl.css";

// Dynamic import for react-map-gl to avoid SSR / window issues
const Map = dynamic(
    () => import("react-map-gl/mapbox").then((mod) => mod.default),
    { ssr: false },
);
const Marker = dynamic(
    () => import("react-map-gl/mapbox").then((mod) => mod.Marker),
    { ssr: false },
);
const NavigationControl = dynamic(
    () => import("react-map-gl/mapbox").then((mod) => mod.NavigationControl),
    { ssr: false },
);

export interface OwnerLocationPickerProps {
    address: string;
    latitude: number | null;
    longitude: number | null;
    confirmedOnMap: boolean;
    onChange: (location: {
        address: string;
        latitude: number | null;
        longitude: number | null;
        confirmedOnMap: boolean;
    }) => void;
    disabled?: boolean;
}

export function OwnerLocationPicker({
    address,
    latitude,
    longitude,
    confirmedOnMap,
    onChange,
    disabled = false,
}: OwnerLocationPickerProps) {
    const mapboxToken = getPublicMapboxToken();
    const [isSearching, setIsSearching] = useState(false);
    const [statusMessage, setStatusMessage] = useState<{
        type: "error" | "warning" | "info" | "success";
        text: string;
    } | null>(null);
    const [isFullScreen, setIsFullScreen] = useState(false);
    const [interactiveScroll, setInteractiveScroll] = useState(false);

    // Initial map center defaults to Ho Chi Minh City if not set
    const [viewState, setViewState] = useState({
        latitude: latitude ?? 10.7769,
        longitude: longitude ?? 106.7009,
        zoom: latitude && longitude ? 16 : 13,
    });

    // Update internal viewState when external lat/lng change
    useEffect(() => {
        if (latitude !== null && longitude !== null) {
            setViewState((prev) => ({
                ...prev,
                latitude,
                longitude,
                zoom: Math.max(prev.zoom, 15),
            }));
        }
    }, [latitude, longitude]);

    const handleSearchAddress = async (e?: React.FormEvent) => {
        if (e) e.preventDefault();
        const trimmedAddress = address.trim();
        if (!trimmedAddress) {
            setStatusMessage({
                type: "warning",
                text: "Vui lòng nhập địa chỉ trước khi tìm kiếm.",
            });
            return;
        }

        if (trimmedAddress.length < 3 || trimmedAddress.length > 200) {
            setStatusMessage({
                type: "warning",
                text: "Địa chỉ tìm kiếm cần từ 3 đến 200 ký tự.",
            });
            return;
        }

        setIsSearching(true);
        setStatusMessage(null);

        try {
            const res = await resolveOwnerLotAddress(trimmedAddress);
            if (res.candidates && res.candidates.length > 0) {
                const candidate = res.candidates[0];
                // Chỉ dời tâm bản đồ tới vị trí gợi ý, KHÔNG tự động cắm ghim cổng vào và KHÔNG tự xác nhận
                setViewState({
                    latitude: candidate.latitude,
                    longitude: candidate.longitude,
                    zoom: 16,
                });
                setStatusMessage({
                    type: "info",
                    text: `Đã đưa bản đồ đến khu vực gợi ý gần đúng. Vui lòng chạm bản đồ hoặc kéo ghim để xác định chính xác CỔNG VÀO bãi xe.`,
                });
            } else {
                setStatusMessage({
                    type: "info",
                    text: "Chưa tìm thấy địa chỉ phù hợp. Bạn vẫn có thể tìm khu vực và đặt ghim thủ công trên bản đồ.",
                });
            }
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                if (err.status === 400) {
                    setStatusMessage({
                        type: "warning",
                        text: "Địa chỉ tìm kiếm không hợp lệ (cần từ 3 đến 200 ký tự).",
                    });
                    return;
                }
                if (err.status === 429) {
                    setStatusMessage({
                        type: "warning",
                        text: "Quá giới hạn tìm kiếm địa chỉ. Vui lòng thử lại sau hoặc tự ghim vị trí trên bản đồ.",
                    });
                    return;
                }
                if (err.status === 503) {
                    setStatusMessage({
                        type: "warning",
                        text: "Dịch vụ tìm kiếm địa chỉ tạm thời không khả dụng. Bạn vẫn có thể tự ghim vị trí trên bản đồ.",
                    });
                    return;
                }
            }
            setStatusMessage({
                type: "error",
                text: "Không thể kết nối đến dịch vụ tìm kiếm địa chỉ. Bạn vẫn có thể tự ghim vị trí trên bản đồ.",
            });
        } finally {
            setIsSearching(false);
        }
    };

    const handleUseCurrentLocation = () => {
        if (typeof navigator === "undefined" || !navigator.geolocation) {
            setStatusMessage({
                type: "warning",
                text: "Trình duyệt không hỗ trợ định vị vị trí hiện tại.",
            });
            return;
        }

        setStatusMessage({
            type: "info",
            text: "Đang lấy vị trí hiện tại...",
        });

        navigator.geolocation.getCurrentPosition(
            (position) => {
                const { latitude: lat, longitude: lng } = position.coords;
                if (lat === 0 && lng === 0) return;
                setViewState({
                    latitude: lat,
                    longitude: lng,
                    zoom: 16,
                });
                onChange({
                    address,
                    latitude: lat,
                    longitude: lng,
                    confirmedOnMap: false,
                });
                setStatusMessage({
                    type: "info",
                    text: "Đã lấy tọa độ thiết bị tham khảo. Vui lòng kéo ghim đến đúng cổng vào và nhấn 'Xác nhận vị trí cổng vào'.",
                });
            },
            (error) => {
                setStatusMessage({
                    type: "warning",
                    text: "Không thể lấy vị trí thiết bị: " + error.message,
                });
            },
            { timeout: 10000, enableHighAccuracy: true },
        );
    };

    const handleMapClick = (e: { lngLat: { lat: number; lng: number } }) => {
        if (disabled) return;
        const { lat, lng } = e.lngLat;
        if (lat === 0 && lng === 0) {
            setStatusMessage({
                type: "warning",
                text: "Tọa độ (0, 0) không hợp lệ. Vui lòng chọn vị trí cổng vào thực tế.",
            });
            return;
        }
        onChange({
            address,
            latitude: lat,
            longitude: lng,
            confirmedOnMap: false,
        });
        setStatusMessage({
            type: "success",
            text: "Đã ghim vị trí cổng vào. Nhấn nút 'Xác nhận vị trí cổng vào' bên dưới để hoàn tất.",
        });
    };

    const handleMarkerDragEnd = (e: {
        lngLat: { lat: number; lng: number };
    }) => {
        if (disabled) return;
        const { lat, lng } = e.lngLat;
        if (lat === 0 && lng === 0) {
            setStatusMessage({
                type: "warning",
                text: "Tọa độ (0, 0) không hợp lệ. Vui lòng chọn vị trí cổng vào thực tế.",
            });
            return;
        }
        onChange({
            address,
            latitude: lat,
            longitude: lng,
            confirmedOnMap: false,
        });
        setStatusMessage({
            type: "success",
            text: "Vị trí đã thay đổi. Vui lòng nhấn 'Xác nhận vị trí cổng vào' để cập nhật.",
        });
    };

    return (
        <div className="owner-location-picker space-y-3">
            {/* 1. Ô nhập địa chỉ và Nút Tìm */}
            <div className="flex flex-col sm:flex-row gap-2">
                <div className="relative flex-1">
                    <input
                        type="text"
                        value={address}
                        disabled={disabled || isSearching}
                        onChange={(e) =>
                            onChange({
                                address: e.target.value,
                                latitude,
                                longitude,
                                confirmedOnMap,
                            })
                        }
                        placeholder="Nhập số nhà, tên đường, phường/xã, quận/huyện..."
                        className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none disabled:bg-gray-100 disabled:text-gray-500"
                        data-testid="owner-lot-address-input"
                    />
                </div>
                <button
                    type="button"
                    disabled={disabled || isSearching || !address.trim()}
                    onClick={() => handleSearchAddress()}
                    className="inline-flex items-center justify-center gap-1.5 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm font-medium hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
                    data-testid="owner-lot-search-btn"
                >
                    {isSearching ? (
                        <>
                            <Loader2 size={16} className="animate-spin" />
                            <span>Đang tìm...</span>
                        </>
                    ) : (
                        <>
                            <Search size={16} />
                            <span>Tìm</span>
                        </>
                    )}
                </button>
            </div>

            {/* Thông báo trạng thái */}
            {statusMessage && (
                <div
                    className={`flex items-start gap-2 p-2.5 rounded-lg text-xs ${
                        statusMessage.type === "error"
                            ? "bg-red-50 text-red-700 border border-red-200"
                            : statusMessage.type === "warning"
                              ? "bg-amber-50 text-amber-700 border border-amber-200"
                              : statusMessage.type === "success"
                                ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
                                : "bg-blue-50 text-blue-700 border border-blue-200"
                    }`}
                    role="alert"
                    data-testid="owner-location-status"
                >
                    {statusMessage.type === "error" && (
                        <AlertCircle size={15} className="mt-0.5 shrink-0" />
                    )}
                    {statusMessage.type === "warning" && (
                        <AlertCircle size={15} className="mt-0.5 shrink-0" />
                    )}
                    {statusMessage.type === "success" && (
                        <CheckCircle2 size={15} className="mt-0.5 shrink-0" />
                    )}
                    {statusMessage.type === "info" && (
                        <Info size={15} className="mt-0.5 shrink-0" />
                    )}
                    <span>{statusMessage.text}</span>
                </div>
            )}

            {/* 2. Khung bản đồ hoặc Cảnh báo không có Mapbox Token */}
            {!mapboxToken ? (
                <div
                    className="p-4 border rounded-xl bg-amber-50 border-amber-200 text-amber-800 text-sm space-y-2"
                    data-testid="mapbox-missing-notice"
                >
                    <div className="flex items-center gap-2 font-semibold">
                        <AlertCircle size={16} className="text-amber-600" />
                        <span>Chưa cấu hình bản đồ tương tác</span>
                    </div>
                    <p className="text-xs text-amber-700">
                        Hệ thống vẫn hỗ trợ tìm kiếm vị trí theo địa chỉ bằng
                        nút Tìm. Để xác nhận vị trí hợp lệ, bạn phải có tọa độ
                        cổng vào trước khi gửi duyệt bãi đỗ.
                    </p>
                    {latitude !== null && longitude !== null && (
                        <div className="text-xs font-mono bg-white p-2 rounded border border-amber-300">
                            Tọa độ đã nhận: {latitude.toFixed(6)},{" "}
                            {longitude.toFixed(6)}
                        </div>
                    )}
                </div>
            ) : (
                <div
                    className={`relative border rounded-xl overflow-hidden transition-all duration-300 bg-gray-50 ${
                        isFullScreen
                            ? "fixed inset-0 z-50 rounded-none bg-white"
                            : "h-[300px] sm:h-[360px]"
                    }`}
                    onClick={() => setInteractiveScroll(true)}
                    onMouseLeave={() => setInteractiveScroll(false)}
                    data-testid="owner-location-map-container"
                >
                    <Map
                        mapboxAccessToken={mapboxToken}
                        {...viewState}
                        onMove={(evt) => setViewState(evt.viewState)}
                        onClick={handleMapClick}
                        onError={() => {
                            setStatusMessage({
                                type: "error",
                                text: "Không thể tải bản đồ tương tác. Bạn vẫn có thể thử lại hoặc kiểm tra kết nối mạng.",
                            });
                        }}
                        scrollZoom={interactiveScroll}
                        style={{ width: "100%", height: "100%" }}
                        mapStyle="mapbox://styles/mapbox/streets-v12"
                    >
                        <NavigationControl position="top-right" />

                        {latitude !== null && longitude !== null && (
                            <Marker
                                latitude={latitude}
                                longitude={longitude}
                                anchor="bottom"
                                draggable={!disabled}
                                onDragEnd={handleMarkerDragEnd}
                            >
                                <div
                                    className="cursor-grab active:cursor-grabbing transform hover:scale-110 transition-transform"
                                    title="Kéo ghim để định vị chính xác cổng vào"
                                    data-testid="owner-lot-pin"
                                >
                                    <MapPin
                                        size={32}
                                        className="text-red-600 fill-red-500 drop-shadow-md"
                                    />
                                </div>
                            </Marker>
                        )}
                    </Map>

                    {/* Toolbar điều khiển trên bản đồ */}
                    <div className="absolute top-2 left-2 flex flex-col gap-1.5 z-10">
                        <button
                            type="button"
                            onClick={handleUseCurrentLocation}
                            disabled={disabled}
                            title="Định vị vị trí hiện tại của thiết bị"
                            className="p-2 bg-white text-gray-700 rounded-lg shadow-md border hover:bg-gray-50 disabled:opacity-50 text-xs font-medium flex items-center gap-1.5"
                            data-testid="btn-use-current-location"
                        >
                            <Crosshair size={15} className="text-blue-600" />
                            <span className="hidden sm:inline">
                                Vị trí hiện tại
                            </span>
                        </button>
                    </div>

                    <div className="absolute top-2 right-12 z-10">
                        <button
                            type="button"
                            onClick={() => setIsFullScreen(!isFullScreen)}
                            title={
                                isFullScreen
                                    ? "Thu nhỏ bản đồ"
                                    : "Phóng to toàn màn hình"
                            }
                            className="p-2 bg-white text-gray-700 rounded-lg shadow-md border hover:bg-gray-50 text-xs flex items-center justify-center"
                            data-testid="btn-toggle-fullscreen"
                        >
                            {isFullScreen ? (
                                <Minimize2 size={15} />
                            ) : (
                                <Maximize2 size={15} />
                            )}
                        </button>
                    </div>

                    {/* Chú thích bản đồ & Bản quyền OpenStreetMap */}
                    <div className="absolute bottom-1 left-2 z-10 bg-white/85 backdrop-blur-xs px-2 py-0.5 rounded text-[10px] text-gray-600 pointer-events-none">
                        Chạm bản đồ hoặc kéo ghim để đặt cổng vào · ©
                        OpenStreetMap contributors
                    </div>
                </div>
            )}

            {/* 3. Hiển thị tọa độ đối chiếu & Trạng thái xác nhận ghim */}
            <div className="flex flex-wrap items-center justify-between text-xs text-gray-600 gap-2 px-1">
                <div className="flex items-center gap-2">
                    <span className="font-medium text-gray-700">
                        Tọa độ cổng vào:
                    </span>
                    {latitude !== null && longitude !== null ? (
                        <span
                            className="font-mono bg-gray-100 px-2 py-0.5 rounded border text-gray-800"
                            data-testid="display-coords"
                        >
                            {latitude.toFixed(6)}, {longitude.toFixed(6)}
                        </span>
                    ) : (
                        <span className="text-gray-400 italic">
                            Chưa chọn vị trí trên bản đồ
                        </span>
                    )}
                </div>

                {confirmedOnMap && latitude !== null && longitude !== null && (
                    <div
                        className="inline-flex items-center gap-1 text-emerald-600 font-medium bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-200"
                        data-testid="confirmed-badge"
                    >
                        <CheckCircle2 size={13} />
                        <span>Đã xác nhận vị trí trên bản đồ</span>
                    </div>
                )}
            </div>
        </div>
    );
}
