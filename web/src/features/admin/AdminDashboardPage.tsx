"use client";

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
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function AdminDashboardPage() {
    const { t, language } = useLanguage();
    const isVi = language === "vi";
    const lots = useAdminLots();
    const { isLoading, isRefreshing, hasLoaded, error, lastUpdatedAt } =
        useAdminLotsMeta();

    const counts = countByStatus(lots);
    const totalCount = lots.length;
    const safeDenominator = totalCount || 1;

    // Oldest pending first
    const queue = lots
        .filter((lot) => lot.status === "Pending")
        .sort((a, b) =>
            (a.submittedAtUtc ?? "9999").localeCompare(
                b.submittedAtUtc ?? "9999",
            ),
        );

    // Real decisions from API: lots that have been reviewed (Approved / Rejected or have reviewedAtUtc)
    const recent = lots
        .filter(
            (lot) =>
                Boolean(lot.reviewedAtUtc) ||
                lot.status === "Approved" ||
                lot.status === "Rejected",
        )
        .sort((a, b) =>
            (b.reviewedAtUtc ?? "").localeCompare(a.reviewedAtUtc ?? ""),
        )
        .slice(0, 5);

    const approvedPct =
        totalCount > 0
            ? Math.round((counts.Approved / safeDenominator) * 100)
            : 0;
    const pendingPct =
        totalCount > 0
            ? Math.round((counts.Pending / safeDenominator) * 100)
            : 0;
    const rejectedPct =
        totalCount > 0
            ? Math.round((counts.Rejected / safeDenominator) * 100)
            : 0;

    const kpiCards = [
        {
            key: "total",
            tone: "total",
            icon: "building",
            label: isVi ? "Tổng số bãi xe" : "Total Parking Lots",
            value: totalCount,
            pct: totalCount > 0 ? 100 : 0,
            desc: isVi
                ? "Tổng số hồ sơ bãi xe ghi nhận trên hệ thống"
                : "Total parking lot records registered in system",
            meta: isVi
                ? `${counts.Approved} hoạt động · ${counts.Pending} chờ duyệt`
                : `${counts.Approved} active · ${counts.Pending} pending`,
            href: "/admin/parking-lots",
        },
        {
            key: "Approved",
            tone: "approved",
            icon: "checkCircle",
            label: t("adminDashActiveLots"),
            value: counts.Approved,
            pct: approvedPct,
            desc: isVi
                ? "Đã phê duyệt đạt chuẩn và mở đón khách"
                : "Approved and publicly visible to customers",
            meta: isVi
                ? `Chiếm ${approvedPct}% tổng số bãi xe`
                : `${approvedPct}% of total parking lots`,
            href: "/admin/parking-lots?status=Approved",
        },
        {
            key: "Pending",
            tone: "pending",
            icon: "clock",
            label: t("adminDashWaitingReview"),
            value: counts.Pending,
            pct: pendingPct,
            desc: isVi
                ? "Hồ sơ đang chờ Admin thẩm định & phê duyệt"
                : "Applications awaiting administrator inspection",
            meta:
                counts.Pending > 0
                    ? isVi
                        ? `Cần xử lý (${pendingPct}% tổng hồ sơ)`
                        : `Action needed (${pendingPct}% of total)`
                    : isVi
                      ? "Hàng đợi đã được xử lý sạch"
                      : "Review queue is clear",
            href: "/admin/parking-lots/pending",
        },
        {
            key: "Rejected",
            tone: "rejected",
            icon: "xCircle",
            label: t("adminDashRejectedLots"),
            value: counts.Rejected,
            pct: rejectedPct,
            desc: isVi
                ? "Hồ sơ chưa đạt chuẩn, chờ chủ bãi bổ sung"
                : "Did not pass review, awaiting owner revision",
            meta: isVi
                ? `Chiếm ${rejectedPct}% tổng số bãi xe`
                : `${rejectedPct}% of total parking lots`,
            href: "/admin/parking-lots?status=Rejected",
        },
    ] as const;

    const formattedUpdatedTime = lastUpdatedAt
        ? new Date(lastUpdatedAt).toLocaleTimeString(isVi ? "vi-VN" : "en-GB", {
              hour: "2-digit",
              minute: "2-digit",
              second: "2-digit",
          })
        : null;

    const showInitialSkeleton = isLoading && !hasLoaded && lots.length === 0;

    return (
        <>
            {/* C. Dashboard Heading */}
            <header className="admin-heading admin-dash-heading">
                <div className="admin-dash-heading-left">
                    <p className="admin-eyebrow">{t("adminDashEyebrow")}</p>
                    <h1>{t("adminDashTitle")}</h1>
                    <p className="admin-heading-desc">
                        {isVi
                            ? "Theo dõi toàn diện tiến độ thẩm định bãi đỗ xe, hồ sơ đối tác và lịch sử phê duyệt trên hệ thống ParkWise."
                            : "Monitor parking lot verifications, partner applications, and approval decisions across ParkWise."}
                    </p>
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

            {/* D. 4 KPI Cards */}
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
                            <div className="admin-skel-line admin-skel-md" />
                            <div className="admin-skel-bar" />
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

                            <p className="admin-stat-desc">{card.desc}</p>

                            <div className="admin-meter" aria-hidden="true">
                                <i style={{ width: `${card.pct}%` }} />
                            </div>

                            <div className="admin-stat-foot">
                                <small>{card.meta}</small>
                                <AdminIcon name="arrow" size={14} />
                            </div>
                        </Link>
                    ))}
                </section>
            )}

            {/* E & F. Main Two-Column Section */}
            <div className="admin-columns">
                {/* E. Khu vực hồ sơ chờ duyệt lâu nhất */}
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
                            <p className="admin-card-subtitle">
                                {isVi
                                    ? "Ưu tiên hiển thị các bãi đỗ xe gửi yêu cầu sớm nhất đang chờ thẩm định"
                                    : "Prioritizing earliest submitted parking lot applications awaiting review"}
                            </p>
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
                            {[1, 2, 3].map((n) => (
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
                    ) : (
                        <ul className="admin-pending-queue">
                            {queue.slice(0, 5).map((lot, index) => {
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

                {/* F. Quyết định gần đây — Activity Timeline */}
                <section className="admin-card admin-card-elevated">
                    <div className="admin-card-head">
                        <div className="admin-card-title-group">
                            <div className="admin-card-title-row">
                                <h2>{t("adminDashRecentDecisions")}</h2>
                            </div>
                            <p className="admin-card-subtitle">
                                {isVi
                                    ? "Dòng thời gian các quyết định phê duyệt và từ chối bãi xe thực tế"
                                    : "Activity timeline of recent parking lot approval and rejection decisions"}
                            </p>
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
                            {[1, 2, 3].map((n) => (
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
                    ) : (
                        <>
                            <ol className="admin-timeline">
                                {recent.map((lot) => {
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
        </>
    );
}
