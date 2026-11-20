"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import {
    AdminEmptyState,
    AdminErrorState,
    AdminIcon,
    StatusPill,
    formatDate,
    formatWaitingDuration,
} from "./AdminUi";
import {
    countByStatus,
    refreshAdminLots,
    useAdminLots,
    useAdminLotsMeta,
} from "./adminLotsData";
import { describeReviewError } from "./reviewParkingLot";
import type { AdminParkingLot } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

interface ActivityTrendBucket {
    dateKey: string;
    label: string;
    submittedCount: number;
    approvedCount: number;
    rejectedCount: number;
}

function toUtcDateKey(iso: string | null | undefined): string | null {
    if (!iso) return null;
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return null;
    const yyyy = date.getUTCFullYear();
    const mm = String(date.getUTCMonth() + 1).padStart(2, "0");
    const dd = String(date.getUTCDate()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd}`;
}

function formatShortDateLabel(dateKey: string): string {
    const parts = dateKey.split("-");
    if (parts.length !== 3) return dateKey;
    return `${parts[2]}/${parts[1]}`;
}

/**
 * Hanya tổng hợp xu hướng từ dữ liệu thật có mốc thời gian (submittedAtUtc / reviewedAtUtc) từ API.
 * Tuyệt đối không tự sinh ngày giả hoặc số liệu mẫu nếu Backend chưa trả về timestamp.
 */
function buildRealActivityTrends(
    lots: AdminParkingLot[],
): ActivityTrendBucket[] {
    const map = new Map<
        string,
        { submittedCount: number; approvedCount: number; rejectedCount: number }
    >();

    for (const lot of lots) {
        const subKey = toUtcDateKey(lot.submittedAtUtc);
        if (subKey) {
            const current = map.get(subKey) ?? {
                submittedCount: 0,
                approvedCount: 0,
                rejectedCount: 0,
            };
            current.submittedCount += 1;
            map.set(subKey, current);
        }

        const revKey = toUtcDateKey(lot.reviewedAtUtc);
        if (revKey) {
            const current = map.get(revKey) ?? {
                submittedCount: 0,
                approvedCount: 0,
                rejectedCount: 0,
            };
            if (lot.status === "Approved") {
                current.approvedCount += 1;
            } else if (lot.status === "Rejected") {
                current.rejectedCount += 1;
            }
            map.set(revKey, current);
        }
    }

    return Array.from(map.entries())
        .sort(([a], [b]) => a.localeCompare(b))
        .slice(-7)
        .map(([dateKey, counts]) => ({
            dateKey,
            label: formatShortDateLabel(dateKey),
            submittedCount: counts.submittedCount,
            approvedCount: counts.approvedCount,
            rejectedCount: counts.rejectedCount,
        }));
}

export function AdminDashboardPage() {
    const { t, language } = useLanguage();
    const isVi = language === "vi";
    const lots = useAdminLots();
    const { isLoading, isRefreshing, hasLoaded, error, lastUpdatedAt } =
        useAdminLotsMeta();

    const [hoveredBarIndex, setHoveredBarIndex] = useState<number | null>(null);
    const [hoveredSegmentKey, setHoveredSegmentKey] = useState<string | null>(
        null,
    );
    const [decisionPage, setDecisionPage] = useState(1);

    const counts = countByStatus(lots);
    const totalCount = lots.length;
    const safeDenominator = totalCount || 1;

    // Oldest pending first
    const queue = useMemo(
        () =>
            lots
                .filter((lot) => lot.status === "Pending")
                .sort((a, b) =>
                    (a.submittedAtUtc ?? "9999").localeCompare(
                        b.submittedAtUtc ?? "9999",
                    ),
                ),
        [lots],
    );

    // Real decisions from API: lots that have been reviewed (Approved / Rejected or have reviewedAtUtc)
    const recent = useMemo(
        () =>
            lots
                .filter(
                    (lot) =>
                        Boolean(lot.reviewedAtUtc) ||
                        lot.status === "Approved" ||
                        lot.status === "Rejected",
                )
                .sort((a, b) =>
                    (b.reviewedAtUtc ?? "").localeCompare(
                        a.reviewedAtUtc ?? "",
                    ),
                ),
        [lots],
    );

    const DECISIONS_PER_PAGE = 2;
    const totalDecisionPages = Math.max(
        1,
        Math.ceil(recent.length / DECISIONS_PER_PAGE),
    );
    const validDecisionPage = Math.min(decisionPage, totalDecisionPages);
    const pagedDecisions = recent.slice(
        (validDecisionPage - 1) * DECISIONS_PER_PAGE,
        validDecisionPage * DECISIONS_PER_PAGE,
    );

    const trendBuckets = useMemo(() => buildRealActivityTrends(lots), [lots]);

    const approvedPct =
        totalCount > 0
            ? Math.round((counts.Approved / safeDenominator) * 100)
            : 0;
    const pendingPct =
        totalCount > 0
            ? Math.round((counts.Pending / safeDenominator) * 100)
            : 0;
    const rejectedPct =
        totalCount > 0 ? Math.max(0, 100 - approvedPct - pendingPct) : 0;

    const kpiCards = [
        {
            key: "total",
            tone: "total",
            icon: "building",
            label: isVi ? "Tổng số bãi xe" : "Total Parking Lots",
            value: totalCount,
            pct: totalCount > 0 ? 100 : 0,
            href: "/admin/parking-lots",
        },
        {
            key: "Approved",
            tone: "approved",
            icon: "checkCircle",
            label: t("adminDashActiveLots"),
            value: counts.Approved,
            pct: approvedPct,
            href: "/admin/parking-lots?status=Approved",
        },
        {
            key: "Pending",
            tone: "pending",
            icon: "clock",
            label: t("adminDashWaitingReview"),
            value: counts.Pending,
            pct: pendingPct,
            href: "/admin/parking-lots/pending",
        },
        {
            key: "Rejected",
            tone: "rejected",
            icon: "xCircle",
            label: t("adminDashRejectedLots"),
            value: counts.Rejected,
            pct: rejectedPct,
            href: "/admin/owner-applications",
        },
    ] as const;

    // Doughnut Chart geometry & segments derived from real counts
    const doughnutSize = 188;
    const doughnutStroke = 24;
    const doughnutRadius = (doughnutSize - doughnutStroke) / 2;
    const doughnutCircumference = 2 * Math.PI * doughnutRadius;

    const statusSegments = useMemo(() => {
        const raw = [
            {
                key: "Approved",
                label: isVi ? "Đang hoạt động" : "Active",
                count: counts.Approved,
                pct: approvedPct,
                color: "#16a34a",
                bgTint: "rgba(22, 163, 74, 0.1)",
            },
            {
                key: "Pending",
                label: isVi ? "Đang chờ duyệt" : "Pending review",
                count: counts.Pending,
                pct: pendingPct,
                color: "#f59e0b",
                bgTint: "rgba(245, 158, 11, 0.12)",
            },
            {
                key: "Rejected",
                label: isVi ? "Bị từ chối" : "Rejected",
                count: counts.Rejected,
                pct: rejectedPct,
                color: "#ea580c",
                bgTint: "rgba(234, 88, 12, 0.1)",
            },
        ];

        let accumulatedRatio = 0;
        return raw.map((item) => {
            const ratio = totalCount > 0 ? item.count / totalCount : 0;
            const dashLength = ratio * doughnutCircumference;
            const dashOffset = -accumulatedRatio * doughnutCircumference;
            accumulatedRatio += ratio;
            return {
                ...item,
                ratio,
                strokeDasharray: `${dashLength} ${doughnutCircumference}`,
                strokeDashoffset: dashOffset,
            };
        });
    }, [
        counts.Approved,
        counts.Pending,
        counts.Rejected,
        approvedPct,
        pendingPct,
        rejectedPct,
        totalCount,
        doughnutCircumference,
        isVi,
    ]);

    const activeSegment = statusSegments.find(
        (seg) => seg.key === hoveredSegmentKey,
    );

    const formattedUpdatedTime = lastUpdatedAt
        ? new Date(lastUpdatedAt).toLocaleTimeString(isVi ? "vi-VN" : "en-GB", {
              hour: "2-digit",
              minute: "2-digit",
              second: "2-digit",
          })
        : null;

    const showInitialSkeleton = isLoading && !hasLoaded && lots.length === 0;

    // Bar chart dimensions
    const chartWidth = 620;
    const chartHeight = 236;
    const padLeft = 38;
    const padRight = 18;
    const padTop = 20;
    const padBottom = 34;
    const plotW = chartWidth - padLeft - padRight;
    const plotH = chartHeight - padTop - padBottom;
    const maxTrendValue = useMemo(() => {
        if (trendBuckets.length === 0) return 4;
        const highest = Math.max(
            ...trendBuckets.map((b) =>
                Math.max(b.submittedCount, b.approvedCount),
            ),
            1,
        );
        return Math.max(4, Math.ceil(highest * 1.25));
    }, [trendBuckets]);

    return (
        <div className="admin-dashboard-container">
            {/* 1. Dashboard Heading */}
            <header className="admin-heading admin-dash-heading">
                <div className="admin-dash-heading-left">
                    <p className="admin-eyebrow">{t("adminDashEyebrow")}</p>
                    <h1>{t("adminDashTitle")}</h1>
                </div>

                <div className="admin-dash-heading-actions">
                    {formattedUpdatedTime && (
                        <span className="admin-last-updated" aria-live="polite">
                            <span
                                className={`admin-live-dot ${isRefreshing ? "is-syncing" : ""}`}
                                aria-hidden="true"
                            />
                            {isVi
                                ? `Cập nhật: ${formattedUpdatedTime}`
                                : `Updated: ${formattedUpdatedTime}`}
                        </span>
                    )}

                    <button
                        type="button"
                        onClick={() => void refreshAdminLots()}
                        disabled={isRefreshing}
                        className="admin-btn admin-btn-ghost admin-refresh-btn"
                        aria-label={
                            isVi ? "Làm mới dữ liệu" : "Refresh dashboard data"
                        }
                    >
                        <AdminIcon
                            name="refresh"
                            size={16}
                            className={isRefreshing ? "admin-spin" : undefined}
                        />
                        <span>
                            {isRefreshing
                                ? isVi
                                    ? "Đang cập nhật..."
                                    : "Refreshing..."
                                : t("refresh")}
                        </span>
                    </button>

                    <Link
                        href="/admin/parking-lots/pending"
                        className="admin-btn admin-btn-dark"
                    >
                        <AdminIcon name="clock" size={16} />
                        <span>{t("adminDashReviewQueueBtn")}</span>
                        {counts.Pending > 0 && (
                            <span className="admin-btn-counter">
                                {counts.Pending}
                            </span>
                        )}
                    </Link>
                </div>
            </header>

            {/* Error State when API fails */}
            {Boolean(error) && (
                <AdminErrorState
                    title={
                        isVi
                            ? "Không thể tải dữ liệu bảng điều khiển"
                            : "Unable to load dashboard data"
                    }
                    message={describeReviewError(error, language)}
                    onRetry={() => void refreshAdminLots()}
                    retryLabel={isVi ? "Thử lại" : "Retry"}
                />
            )}

            {/* 2. 4 KPI Cards */}
            {showInitialSkeleton ? (
                <section
                    className="admin-stats"
                    aria-label="Loading parking lot summary"
                    aria-busy="true"
                >
                    {[1, 2, 3, 4].map((idx) => (
                        <div
                            key={idx}
                            className="admin-stat admin-stat-skeleton"
                        >
                            <div className="admin-skel-line admin-skel-sm" />
                            <div className="admin-skel-line admin-skel-lg" />
                            <div className="admin-stat-meter-row">
                                <div className="admin-skel-bar" />
                                <div className="admin-skel-arrow" />
                            </div>
                        </div>
                    ))}
                </section>
            ) : (
                <section
                    className="admin-stats"
                    aria-label="Parking lot summary"
                >
                    {kpiCards.map((card) => (
                        <Link
                            key={card.key}
                            href={card.href}
                            className={`admin-stat admin-stat-${card.tone}`}
                        >
                            <div className="admin-stat-top">
                                <span className="admin-stat-label">
                                    {card.label}
                                </span>
                                <span
                                    className={`admin-stat-icon admin-stat-icon-${card.tone}`}
                                >
                                    <AdminIcon name={card.icon} size={20} />
                                </span>
                            </div>

                            <div className="admin-stat-value-row">
                                <strong>{card.value}</strong>
                                <span className="admin-stat-pct">
                                    {card.pct}%
                                </span>
                            </div>

                            <div className="admin-stat-meter-row">
                                <div className="admin-meter" aria-hidden="true">
                                    <i style={{ width: `${card.pct}%` }} />
                                </div>
                                <span
                                    className="admin-stat-arrow"
                                    aria-hidden="true"
                                >
                                    <AdminIcon name="arrow" size={16} />
                                </span>
                            </div>
                        </Link>
                    ))}
                </section>
            )}

            {/* 3. Main Analytics – 2 Columns (Left: Activity Trends ~66%, Right: Status Distribution ~34%) */}
            <section
                className="admin-analytics-grid"
                aria-label={
                    isVi
                        ? "Phân tích xu hướng và tỷ lệ trạng thái"
                        : "Activity trends and status distribution"
                }
            >
                {/* LEFT PANEL – Activity Trends (Bar Chart) */}
                <article className="admin-card admin-analytics-card admin-analytics-trends">
                    <div className="admin-card-head">
                        <div className="admin-card-title-group">
                            <div className="admin-card-title-row">
                                <h2>
                                    {isVi
                                        ? "Xu hướng hoạt động"
                                        : "Activity Trends"}
                                </h2>
                            </div>
                        </div>

                        <div
                            className="admin-chart-legend-inline"
                            aria-label="Chart legend"
                        >
                            <span className="admin-legend-chip">
                                <i className="admin-legend-dot is-lime" />
                                {isVi ? "Hồ sơ gửi mới" : "Submitted"}
                            </span>
                            <span className="admin-legend-chip">
                                <i className="admin-legend-dot is-green" />
                                {isVi ? "Đã phê duyệt" : "Approved"}
                            </span>
                        </div>
                    </div>

                    {showInitialSkeleton ? (
                        <div
                            className="admin-chart-skeleton"
                            aria-busy="true"
                        />
                    ) : trendBuckets.length === 0 ? (
                        <div className="admin-chart-empty-wrap">
                            <AdminEmptyState
                                icon="calendar"
                                title={
                                    isVi
                                        ? "Chưa có dữ liệu lịch sử từ hệ thống"
                                        : "No historical data from system"
                                }
                            />
                        </div>
                    ) : (
                        <div className="admin-bar-chart-wrap">
                            {hoveredBarIndex !== null &&
                                trendBuckets[hoveredBarIndex] && (
                                    <div
                                        className="admin-chart-tooltip"
                                        role="status"
                                    >
                                        <strong>
                                            {isVi ? "Ngày " : "Date "}
                                            {
                                                trendBuckets[hoveredBarIndex]
                                                    .label
                                            }
                                        </strong>
                                        <span>
                                            {isVi ? "Gửi mới:" : "Submitted:"}{" "}
                                            <b>
                                                {
                                                    trendBuckets[
                                                        hoveredBarIndex
                                                    ].submittedCount
                                                }
                                            </b>
                                        </span>
                                        <span>
                                            {isVi ? "Phê duyệt:" : "Approved:"}{" "}
                                            <b>
                                                {
                                                    trendBuckets[
                                                        hoveredBarIndex
                                                    ].approvedCount
                                                }
                                            </b>
                                        </span>
                                    </div>
                                )}

                            <svg
                                viewBox={`0 0 ${chartWidth} ${chartHeight}`}
                                className="admin-bar-chart-svg"
                                role="img"
                                aria-label={
                                    isVi
                                        ? "Biểu đồ cột xu hướng hoạt động"
                                        : "Activity trends bar chart"
                                }
                            >
                                {/* Horizontal grid lines */}
                                {[0, 0.25, 0.5, 0.75, 1].map((ratio, idx) => {
                                    const y = padTop + plotH * (1 - ratio);
                                    const val = Math.round(
                                        maxTrendValue * ratio,
                                    );
                                    return (
                                        <g key={idx}>
                                            <line
                                                x1={padLeft}
                                                y1={y}
                                                x2={chartWidth - padRight}
                                                y2={y}
                                                stroke="#e6e8f0"
                                                strokeDasharray={
                                                    ratio === 0
                                                        ? undefined
                                                        : "4 4"
                                                }
                                            />
                                            <text
                                                x={padLeft - 10}
                                                y={y + 4}
                                                textAnchor="end"
                                                fontSize="11"
                                                fontWeight="600"
                                                fill="#8c93a4"
                                            >
                                                {val}
                                            </text>
                                        </g>
                                    );
                                })}

                                {/* Grouped Bars */}
                                {trendBuckets.map((bucket, idx) => {
                                    const groupWidth =
                                        plotW / trendBuckets.length;
                                    const groupCenter =
                                        padLeft +
                                        idx * groupWidth +
                                        groupWidth / 2;
                                    const barWidth = Math.min(
                                        22,
                                        Math.max(10, groupWidth * 0.26),
                                    );
                                    const barGap = 5;

                                    const subHeight = Math.max(
                                        bucket.submittedCount > 0 ? 6 : 0,
                                        (bucket.submittedCount /
                                            maxTrendValue) *
                                            plotH,
                                    );
                                    const appHeight = Math.max(
                                        bucket.approvedCount > 0 ? 6 : 0,
                                        (bucket.approvedCount / maxTrendValue) *
                                            plotH,
                                    );

                                    const subX =
                                        groupCenter - barWidth - barGap / 2;
                                    const subY = padTop + plotH - subHeight;

                                    const appX = groupCenter + barGap / 2;
                                    const appY = padTop + plotH - appHeight;

                                    const isHovered = hoveredBarIndex === idx;

                                    return (
                                        <g
                                            key={bucket.dateKey}
                                            className="admin-bar-group"
                                            onMouseEnter={() =>
                                                setHoveredBarIndex(idx)
                                            }
                                            onMouseLeave={() =>
                                                setHoveredBarIndex(null)
                                            }
                                        >
                                            {/* Hover highlight column */}
                                            <rect
                                                x={
                                                    padLeft +
                                                    idx * groupWidth +
                                                    4
                                                }
                                                y={padTop}
                                                width={Math.max(
                                                    0,
                                                    groupWidth - 8,
                                                )}
                                                height={plotH}
                                                rx={8}
                                                fill={
                                                    isHovered
                                                        ? "rgba(23, 23, 23, 0.035)"
                                                        : "transparent"
                                                }
                                            />

                                            {/* Primary Bar: Submitted (ParkWise Neon Lime) */}
                                            <rect
                                                x={subX}
                                                y={subY}
                                                width={barWidth}
                                                height={subHeight}
                                                rx={5}
                                                fill="#d9f96f"
                                                stroke="#84cc16"
                                                strokeWidth={1}
                                            />

                                            {/* Secondary Bar: Approved (ParkWise Emerald Green) */}
                                            <rect
                                                x={appX}
                                                y={appY}
                                                width={barWidth}
                                                height={appHeight}
                                                rx={5}
                                                fill="#16a34a"
                                            />

                                            {/* X-axis label */}
                                            <text
                                                x={groupCenter}
                                                y={chartHeight - 10}
                                                textAnchor="middle"
                                                fontSize="11.5"
                                                fontWeight={
                                                    isHovered ? "800" : "600"
                                                }
                                                fill={
                                                    isHovered
                                                        ? "#171717"
                                                        : "#5d6474"
                                                }
                                            >
                                                {bucket.label}
                                            </text>
                                        </g>
                                    );
                                })}
                            </svg>
                        </div>
                    )}
                </article>

                {/* RIGHT PANEL – Status Distribution (Doughnut Chart) */}
                <article className="admin-card admin-analytics-card admin-analytics-distribution">
                    <div className="admin-card-head">
                        <div className="admin-card-title-group">
                            <div className="admin-card-title-row">
                                <h2>
                                    {isVi
                                        ? "Tỷ lệ trạng thái"
                                        : "Status Distribution"}
                                </h2>
                            </div>
                        </div>
                    </div>

                    {showInitialSkeleton ? (
                        <div
                            className="admin-chart-skeleton"
                            aria-busy="true"
                        />
                    ) : totalCount === 0 ? (
                        <div className="admin-chart-empty-wrap">
                            <AdminEmptyState
                                icon="lots"
                                title={
                                    isVi
                                        ? "Chưa có dữ liệu từ hệ thống"
                                        : "No data from system"
                                }
                                description={
                                    isVi
                                        ? "Chưa có hồ sơ bãi xe nào để tính toán tỷ lệ trạng thái."
                                        : "No parking lot records available to calculate distribution."
                                }
                            />
                        </div>
                    ) : (
                        <div className="admin-doughnut-layout">
                            <div className="admin-doughnut-figure">
                                <svg
                                    width={doughnutSize}
                                    height={doughnutSize}
                                    viewBox={`0 0 ${doughnutSize} ${doughnutSize}`}
                                    className="admin-doughnut-svg"
                                    role="img"
                                    aria-label={
                                        isVi
                                            ? "Biểu đồ tròn tỷ lệ trạng thái bãi đỗ xe"
                                            : "Parking lot status distribution doughnut chart"
                                    }
                                >
                                    <circle
                                        cx={doughnutSize / 2}
                                        cy={doughnutSize / 2}
                                        r={doughnutRadius}
                                        fill="none"
                                        stroke="#f1f4f9"
                                        strokeWidth={doughnutStroke}
                                    />
                                    {statusSegments.map((seg) =>
                                        seg.count > 0 ? (
                                            <circle
                                                key={seg.key}
                                                cx={doughnutSize / 2}
                                                cy={doughnutSize / 2}
                                                r={doughnutRadius}
                                                fill="none"
                                                stroke={seg.color}
                                                strokeWidth={
                                                    hoveredSegmentKey ===
                                                    seg.key
                                                        ? doughnutStroke + 4
                                                        : doughnutStroke
                                                }
                                                strokeDasharray={
                                                    seg.strokeDasharray
                                                }
                                                strokeDashoffset={
                                                    seg.strokeDashoffset
                                                }
                                                className="admin-doughnut-segment"
                                                onMouseEnter={() =>
                                                    setHoveredSegmentKey(
                                                        seg.key,
                                                    )
                                                }
                                                onMouseLeave={() =>
                                                    setHoveredSegmentKey(null)
                                                }
                                            />
                                        ) : null,
                                    )}
                                </svg>

                                <div className="admin-doughnut-center">
                                    <strong>
                                        {activeSegment
                                            ? `${activeSegment.pct}%`
                                            : totalCount}
                                    </strong>
                                    <span>
                                        {activeSegment
                                            ? activeSegment.label
                                            : isVi
                                              ? "Tổng bản ghi"
                                              : "Total records"}
                                    </span>
                                </div>
                            </div>

                            <ul
                                className="admin-doughnut-legend"
                                aria-label="Status breakdown"
                            >
                                {statusSegments.map((seg) => (
                                    <li
                                        key={seg.key}
                                        className={`admin-doughnut-legend-item ${
                                            hoveredSegmentKey === seg.key
                                                ? "is-hovered"
                                                : ""
                                        }`}
                                        onMouseEnter={() =>
                                            setHoveredSegmentKey(seg.key)
                                        }
                                        onMouseLeave={() =>
                                            setHoveredSegmentKey(null)
                                        }
                                    >
                                        <div className="admin-doughnut-legend-left">
                                            <span
                                                className="admin-doughnut-swatch"
                                                style={{
                                                    background: seg.color,
                                                }}
                                                aria-hidden="true"
                                            />
                                            <span className="admin-doughnut-legend-label">
                                                {seg.label}
                                            </span>
                                        </div>
                                        <div className="admin-doughnut-legend-right">
                                            <b className="admin-doughnut-legend-count">
                                                {seg.count}
                                            </b>
                                            <span
                                                className="admin-doughnut-legend-pct"
                                                style={{
                                                    background: seg.bgTint,
                                                    color: seg.color,
                                                }}
                                            >
                                                {seg.pct}%
                                            </span>
                                        </div>
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}
                </article>
            </section>

            {/* 4. Bottom Section – 2 Columns (Pending Applications & Recent Decisions) */}
            <div className="admin-columns">
                {/* LEFT: Khu vực hồ sơ chờ duyệt lâu nhất */}
                <section className="admin-card admin-card-elevated">
                    <div className="admin-card-head">
                        <div className="admin-card-title-group">
                            <div className="admin-card-title-row">
                                <h2>{t("adminDashOldestPending")}</h2>
                                {queue.length > 0 && (
                                    <span className="admin-section-count">
                                        {queue.length}
                                    </span>
                                )}
                            </div>
                        </div>
                        <Link
                            href="/admin/parking-lots/pending"
                            className="admin-card-head-link"
                        >
                            <span>{t("adminDashSeeAll")}</span>
                            <AdminIcon name="arrow" size={14} />
                        </Link>
                    </div>

                    {showInitialSkeleton ? (
                        <div className="admin-skeleton-list" aria-busy="true">
                            {[1, 2].map((n) => (
                                <div key={n} className="admin-skeleton-row">
                                    <div className="admin-skel-avatar" />
                                    <div className="admin-skel-col">
                                        <div className="admin-skel-line admin-skel-md" />
                                        <div className="admin-skel-line admin-skel-sm" />
                                    </div>
                                    <div className="admin-skel-pill" />
                                </div>
                            ))}
                        </div>
                    ) : queue.length === 0 ? (
                        <div className="admin-column-empty-wrap">
                            <AdminEmptyState
                                icon="checkCircle"
                                title={
                                    isVi
                                        ? "Tất cả hồ sơ đã được xử lý"
                                        : "All applications reviewed"
                                }
                                description={t("adminDashNothingToReview")}
                                action={
                                    <Link
                                        href="/admin/parking-lots"
                                        className="admin-btn admin-btn-ghost admin-btn-sm"
                                    >
                                        {isVi
                                            ? "Xem danh sách tất cả bãi xe"
                                            : "View all parking lots"}
                                    </Link>
                                }
                            />
                        </div>
                    ) : (
                        <ul className="admin-pending-queue">
                            {queue.slice(0, 2).map((lot, index) => {
                                const waitingBadge = formatWaitingDuration(
                                    lot.submittedAtUtc,
                                    language,
                                );
                                const ownerDisplay =
                                    lot.ownerName ??
                                    (lot.ownerUserId
                                        ? `${lot.ownerUserId.slice(0, 8)}...`
                                        : "—");
                                return (
                                    <li
                                        key={lot.id}
                                        className="admin-pending-item"
                                    >
                                        <span
                                            className="admin-pending-rank"
                                            title={
                                                isVi
                                                    ? `Ưu tiên #${index + 1}`
                                                    : `Priority #${index + 1}`
                                            }
                                        >
                                            #{index + 1}
                                        </span>

                                        <div className="admin-pending-main">
                                            <div className="admin-pending-title-row">
                                                <Link
                                                    href={`/admin/parking-lots/${lot.id}`}
                                                    className="admin-pending-lot-name"
                                                >
                                                    <strong>{lot.name}</strong>
                                                </Link>
                                                <StatusPill
                                                    status={lot.status}
                                                />
                                                {waitingBadge && (
                                                    <span className="admin-waiting-badge">
                                                        <AdminIcon
                                                            name="clock"
                                                            size={12}
                                                        />
                                                        {waitingBadge}
                                                    </span>
                                                )}
                                            </div>

                                            {lot.address && (
                                                <p className="admin-pending-address">
                                                    <AdminIcon
                                                        name="pin"
                                                        size={13}
                                                    />
                                                    <span>{lot.address}</span>
                                                </p>
                                            )}

                                            <div className="admin-pending-meta">
                                                <span>
                                                    <AdminIcon
                                                        name="user"
                                                        size={12}
                                                    />
                                                    {isVi
                                                        ? "Chủ bãi:"
                                                        : "Owner:"}{" "}
                                                    <b
                                                        title={
                                                            lot.ownerName ??
                                                            lot.ownerUserId ??
                                                            undefined
                                                        }
                                                    >
                                                        {ownerDisplay}
                                                    </b>
                                                </span>
                                                <span className="admin-meta-dot">
                                                    ·
                                                </span>
                                                <span>
                                                    <AdminIcon
                                                        name="calendar"
                                                        size={12}
                                                    />
                                                    {t("adminDashSubmittedAt")}:{" "}
                                                    <b>
                                                        {formatDate(
                                                            lot.submittedAtUtc ??
                                                                null,
                                                        )}
                                                    </b>
                                                </span>
                                                {lot.type && (
                                                    <>
                                                        <span className="admin-meta-dot">
                                                            ·
                                                        </span>
                                                        <span className="admin-lot-type-tag">
                                                            {lot.type}
                                                        </span>
                                                    </>
                                                )}
                                            </div>
                                        </div>

                                        <div className="admin-pending-actions">
                                            <Link
                                                href={`/admin/parking-lots/${lot.id}`}
                                                className="admin-btn admin-btn-lime admin-btn-sm"
                                            >
                                                <span>
                                                    {t("adminDashReviewBtn")}
                                                </span>
                                                <AdminIcon
                                                    name="arrow"
                                                    size={14}
                                                />
                                            </Link>
                                        </div>
                                    </li>
                                );
                            })}
                        </ul>
                    )}
                </section>

                {/* RIGHT: Quyết định gần đây — Activity Timeline */}
                <section className="admin-card admin-card-elevated">
                    <div className="admin-card-head">
                        <div className="admin-card-title-group">
                            <div className="admin-card-title-row">
                                <h2>{t("adminDashRecentDecisions")}</h2>
                            </div>
                        </div>
                        <Link
                            href="/admin/parking-lots"
                            className="admin-card-head-link"
                        >
                            <span>
                                {isVi ? "Toàn bộ lịch sử" : "Full history"}
                            </span>
                            <AdminIcon name="arrow" size={14} />
                        </Link>
                    </div>

                    {showInitialSkeleton ? (
                        <div className="admin-skeleton-list" aria-busy="true">
                            {[1, 2].map((n) => (
                                <div key={n} className="admin-skeleton-row">
                                    <div className="admin-skel-avatar" />
                                    <div className="admin-skel-col">
                                        <div className="admin-skel-line admin-skel-md" />
                                        <div className="admin-skel-line admin-skel-sm" />
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : recent.length === 0 ? (
                        <div className="admin-column-empty-wrap">
                            <AdminEmptyState
                                icon="history"
                                title={
                                    isVi
                                        ? "Chưa có quyết định thẩm định"
                                        : "No review decisions yet"
                                }
                                description={t("adminDashNoDecisions")}
                                action={
                                    <Link
                                        href="/admin/audit-logs"
                                        className="admin-btn admin-btn-ghost admin-btn-sm"
                                    >
                                        {isVi
                                            ? "Mở nhật ký kiểm toán hệ thống"
                                            : "Open system audit logs"}
                                    </Link>
                                }
                            />
                        </div>
                    ) : (
                        <>
                            <ol className="admin-timeline">
                                {pagedDecisions.map((lot) => {
                                    const isApproved =
                                        lot.status === "Approved";
                                    const isRejected =
                                        lot.status === "Rejected";
                                    const toneClass = isApproved
                                        ? "is-approved"
                                        : isRejected
                                          ? "is-rejected"
                                          : "is-updated";
                                    const actionText = isApproved
                                        ? isVi
                                            ? "Phê duyệt bãi đỗ xe"
                                            : "Approved parking lot"
                                        : isRejected
                                          ? isVi
                                              ? "Từ chối hồ sơ bãi xe"
                                              : "Rejected parking lot"
                                          : isVi
                                            ? "Cập nhật hồ sơ"
                                            : "Updated application";

                                    return (
                                        <li
                                            key={lot.id}
                                            className={`admin-timeline-item ${toneClass}`}
                                        >
                                            <span
                                                className="admin-timeline-marker"
                                                aria-hidden="true"
                                            >
                                                <AdminIcon
                                                    name={
                                                        isApproved
                                                            ? "check"
                                                            : isRejected
                                                              ? "close"
                                                              : "clock"
                                                    }
                                                    size={13}
                                                />
                                            </span>

                                            <div className="admin-timeline-body">
                                                <div className="admin-timeline-top">
                                                    <span className="admin-timeline-action">
                                                        {actionText}
                                                    </span>
                                                    <StatusPill
                                                        status={lot.status}
                                                    />
                                                </div>

                                                <Link
                                                    href={`/admin/parking-lots/${lot.id}`}
                                                    className="admin-timeline-target"
                                                >
                                                    <strong>{lot.name}</strong>
                                                </Link>

                                                {lot.address && (
                                                    <small className="admin-timeline-sub">
                                                        {lot.address}
                                                    </small>
                                                )}

                                                {isRejected &&
                                                    lot.rejectionReason && (
                                                        <p className="admin-timeline-reason">
                                                            {isVi
                                                                ? "Lý do:"
                                                                : "Reason:"}{" "}
                                                            {
                                                                lot.rejectionReason
                                                            }
                                                        </p>
                                                    )}

                                                <div className="admin-timeline-meta">
                                                    <span>
                                                        {isVi
                                                            ? "Thực hiện bởi:"
                                                            : "Actor:"}{" "}
                                                        <b>SystemAdmin</b>
                                                    </span>
                                                    {lot.reviewedAtUtc && (
                                                        <>
                                                            <span className="admin-meta-dot">
                                                                ·
                                                            </span>
                                                            <time
                                                                dateTime={
                                                                    lot.reviewedAtUtc
                                                                }
                                                            >
                                                                {formatDate(
                                                                    lot.reviewedAtUtc,
                                                                )}
                                                            </time>
                                                        </>
                                                    )}
                                                </div>
                                            </div>
                                        </li>
                                    );
                                })}
                            </ol>

                            <div className="admin-timeline-footer">
                                {totalDecisionPages > 1 && (
                                    <div
                                        className="admin-decisions-pagination"
                                        role="navigation"
                                        aria-label={
                                            isVi
                                                ? "Phân trang quyết định gần đây"
                                                : "Recent decisions pagination"
                                        }
                                    >
                                        <button
                                            type="button"
                                            className="admin-pager-btn admin-pager-arrow"
                                            disabled={validDecisionPage <= 1}
                                            onClick={() =>
                                                setDecisionPage((p) =>
                                                    Math.max(1, p - 1),
                                                )
                                            }
                                            title={
                                                isVi
                                                    ? "Trang trước"
                                                    : "Previous page"
                                            }
                                            aria-label={
                                                isVi
                                                    ? "Trang trước"
                                                    : "Previous page"
                                            }
                                        >
                                            <AdminIcon name="back" size={13} />
                                        </button>

                                        <div className="admin-pager-numbers">
                                            {Array.from({
                                                length: totalDecisionPages,
                                            }).map((_, idx) => {
                                                const pageNum = idx + 1;
                                                const isActive =
                                                    pageNum ===
                                                    validDecisionPage;
                                                return (
                                                    <button
                                                        key={pageNum}
                                                        type="button"
                                                        className={`admin-pager-btn admin-pager-num ${
                                                            isActive
                                                                ? "is-active"
                                                                : ""
                                                        }`}
                                                        onClick={() =>
                                                            setDecisionPage(
                                                                pageNum,
                                                            )
                                                        }
                                                        aria-current={
                                                            isActive
                                                                ? "page"
                                                                : undefined
                                                        }
                                                    >
                                                        {pageNum}
                                                    </button>
                                                );
                                            })}
                                        </div>

                                        <button
                                            type="button"
                                            className="admin-pager-btn admin-pager-arrow"
                                            disabled={
                                                validDecisionPage >=
                                                totalDecisionPages
                                            }
                                            onClick={() =>
                                                setDecisionPage((p) =>
                                                    Math.min(
                                                        totalDecisionPages,
                                                        p + 1,
                                                    ),
                                                )
                                            }
                                            title={
                                                isVi ? "Trang sau" : "Next page"
                                            }
                                            aria-label={
                                                isVi ? "Trang sau" : "Next page"
                                            }
                                        >
                                            <AdminIcon name="arrow" size={13} />
                                        </button>
                                    </div>
                                )}

                                <Link
                                    href="/admin/audit-logs"
                                    className="admin-btn admin-btn-ghost admin-btn-sm admin-timeline-audit-link"
                                >
                                    <AdminIcon name="shield" size={15} />
                                    <span>
                                        {isVi
                                            ? "Xem toàn bộ lịch sử kiểm toán"
                                            : "View full audit trail"}
                                    </span>
                                </Link>
                            </div>
                        </>
                    )}
                </section>
            </div>
        </div>
    );
}
