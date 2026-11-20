"use client";

import { type ReactNode } from "react";
import Image from "next/image";
import Link from "next/link";
import { CustomerIcon } from "../CustomerShell";
import { type MapCoordinates } from "../geocodeDestination";
import { type RecommendedParkingLot } from "../parkingLotApi";
import { type SearchResult } from "./useCustomerFindParking";

export type CustomerSearchResultsListProps = {
    resultsState: "loading" | "ready" | "error";
    visibleParkings: SearchResult[];
    activeSelectedId: string;
    onSelectId: (id: string) => void;
    allRecommendations: RecommendedParkingLot[] | null;
    searchOrigin: MapCoordinates | { longitude: number; latitude: number } | null;
    originKey: string;
    radiusKm: number;
    copy: (en: string, vi: string) => string;
    children?: ReactNode;
};

export function CustomerSearchResultsList({
    resultsState,
    visibleParkings,
    activeSelectedId,
    onSelectId,
    allRecommendations,
    searchOrigin,
    originKey,
    radiusKm,
    copy,
    children,
}: CustomerSearchResultsListProps) {
    return (
        <div className="customer-search-results">
            <div className="customer-search-results-note">
                <CustomerIcon name="location" size={17} />
                <span>
                    {resultsState === "loading"
                        ? copy(
                              "Loading approved parking lots…",
                              "Đang tải bãi đỗ đã duyệt…",
                          )
                        : resultsState === "error"
                          ? copy(
                                "Parking search is unavailable right now. Try again later.",
                                "Không thể tìm bãi đỗ lúc này. Vui lòng thử lại sau.",
                            )
                          : searchOrigin
                            ? `${copy("Approved parking lots within", "Bãi đỗ đã duyệt trong phạm vi")} ${radiusKm} km.`
                            : copy(
                                  "All approved parking lots. Search a destination to see the nearest ones.",
                                  "Tất cả bãi đỗ đã duyệt. Tìm điểm đến để xem bãi gần nhất.",
                              )}
                </span>
            </div>

            {children}

            <div className="customer-search-list">
                {visibleParkings.map((parking) => {
                    const lotRec = allRecommendations?.find(
                        (r) => r.lotId === parking.id,
                    );
                    return (
                        <article
                            className={`customer-search-result ${activeSelectedId === parking.id ? "selected" : ""}`}
                            key={parking.id}
                        >
                            <div className="customer-search-result-top">
                                <span>{parking.type.toUpperCase()}</span>
                                {lotRec && (
                                    <span
                                        className="customer-ai-highlight-badge"
                                        title={lotRec.reasons.join(" · ")}
                                    >
                                        {copy("Top", "Hạng")} {lotRec.rank}
                                        {lotRec.probabilityPercent !== null ? (
                                            <>
                                                ,{" "}
                                                {Math.round(
                                                    lotRec.probabilityPercent,
                                                )}
                                                % {copy("free", "còn chỗ")}
                                            </>
                                        ) : (
                                            <>
                                                ,{" "}
                                                {copy(
                                                    "distance rank",
                                                    "sắp theo khoảng cách",
                                                )}
                                            </>
                                        )}
                                    </span>
                                )}
                                {activeSelectedId === parking.id && (
                                    <strong>
                                        <CustomerIcon name="check" size={15} />{" "}
                                        {copy(
                                            "Selected on map",
                                            "Đã chọn trên bản đồ",
                                        )}
                                    </strong>
                                )}
                            </div>
                            <div className="customer-search-result-main">
                                <div className="customer-search-result-photo">
                                    <Image
                                        src="/images/parkwise/dashboard-parking-structure.webp"
                                        alt={copy(
                                            "Illustration of a parking facility",
                                            "Minh họa bãi đỗ xe",
                                        )}
                                        fill
                                        sizes="(max-width: 700px) 100vw, 200px"
                                    />
                                    <span className="customer-search-result-photo-label">
                                        {copy("Illustration", "Ảnh minh họa")}
                                    </span>
                                </div>
                                <div className="customer-search-result-content">
                                    <h2>{parking.name}</h2>
                                    <p className="customer-search-result-location">
                                        <CustomerIcon
                                            name="location"
                                            size={15}
                                        />{" "}
                                        {parking.address}
                                    </p>
                                    <div className="customer-search-result-metrics">
                                        <span>
                                            <CustomerIcon
                                                name="location"
                                                size={17}
                                            />
                                            <small>
                                                {copy(
                                                    "Distance",
                                                    "Khoảng cách",
                                                )}
                                            </small>
                                            <b>
                                                {parking.distanceKm == null
                                                    ? copy(
                                                          "Search to see",
                                                          "Tìm để xem",
                                                      )
                                                    : `${parking.distanceKm} km`}
                                            </b>
                                        </span>
                                        <span>
                                            <small>
                                                {copy(
                                                    "Starting price",
                                                    "Giá khởi điểm",
                                                )}
                                            </small>
                                            <b>
                                                {parking.metricsState ===
                                                "loading"
                                                    ? copy(
                                                          "Loading...",
                                                          "Đang tải...",
                                                      )
                                                    : parking.metricsState ===
                                                        "error"
                                                      ? copy(
                                                            "Unavailable",
                                                            "Chưa có dữ liệu",
                                                        )
                                                      : parking.startingPrice ==
                                                          null
                                                        ? copy(
                                                              "Not set",
                                                              "Chưa có giá",
                                                          )
                                                        : `${new Intl.NumberFormat("vi-VN").format(parking.startingPrice)} ₫`}
                                            </b>
                                        </span>
                                        <span>
                                            <small>
                                                {copy(
                                                    "Spaces now",
                                                    "Chỗ trống hiện tại",
                                                )}
                                            </small>
                                            <b>
                                                {parking.metricsState ===
                                                "loading"
                                                    ? copy(
                                                          "Loading...",
                                                          "Đang tải...",
                                                      )
                                                    : (parking.availableSpaces ??
                                                      copy(
                                                          "Unavailable",
                                                          "Chưa có dữ liệu",
                                                      ))}
                                            </b>
                                        </span>
                                    </div>
                                    {originKey && (
                                        <p className="customer-search-driving-time">
                                            <CustomerIcon
                                                name="clock"
                                                size={15}
                                            />
                                            {parking.drivingTimeMinutes == null
                                                ? copy(
                                                      "Driving time unavailable",
                                                      "Chưa có thời gian lái xe",
                                                  )
                                                : `~${parking.drivingTimeMinutes.toLocaleString(copy("en-US", "vi-VN"), { maximumFractionDigits: 1 })} ${copy("min drive", "phút lái xe")}`}
                                        </p>
                                    )}
                                    <div className="customer-search-result-actions">
                                        <button
                                            type="button"
                                            className="customer-shell-secondary"
                                            onClick={() =>
                                                onSelectId(parking.id)
                                            }
                                        >
                                            {copy(
                                                "View on map",
                                                "Xem trên bản đồ",
                                            )}{" "}
                                            <CustomerIcon
                                                name="arrow"
                                                size={15}
                                            />
                                        </button>
                                        <Link
                                            href={`/parking-lots/${parking.id}`}
                                            className="customer-shell-primary"
                                        >
                                            {copy(
                                                "View details",
                                                "Xem chi tiết",
                                            )}
                                            <CustomerIcon
                                                name="arrow"
                                                size={15}
                                            />
                                        </Link>
                                    </div>
                                </div>
                            </div>
                        </article>
                    );
                })}
            </div>

            {resultsState === "ready" && visibleParkings.length === 0 && (
                <div className="customer-search-empty">
                    <CustomerIcon name="search" size={28} />
                    <strong>
                        {copy(
                            "No approved parking lots found",
                            "Không tìm thấy bãi đỗ đã duyệt",
                        )}
                    </strong>
                    <p>
                        {copy(
                            "Try another destination or a larger distance.",
                            "Thử điểm đến khác hoặc tăng khoảng cách tìm kiếm.",
                        )}
                    </p>
                </div>
            )}
        </div>
    );
}
