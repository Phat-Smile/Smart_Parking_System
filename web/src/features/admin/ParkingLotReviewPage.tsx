"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { AdminIcon, StatusPill, vnd } from "./AdminUi";
import { refreshAdminLots } from "./adminLotsData";
import {
    MAX_REJECTION_REASON,
    approveParkingLot,
    describeReviewError,
    getAdminParkingLotDetail,
    getAdminParkingLotLayout,
    getAdminParkingLotPricing,
    isParkingLotId,
    rejectParkingLot,
    validateRejectionReason,
} from "./reviewParkingLot";
import type {
    AdminLotLayout,
    AdminLotPricing,
    AdminParkingLotDetail,
    ReviewedParkingLot,
} from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

type Dialog = "approve" | "reject" | null;

// What the admin reviews, read from the #128 admin routes (any approval state).
type LotReview = {
    detail: AdminParkingLotDetail;
    layout: AdminLotLayout;
    prices: { car: AdminLotPricing | null; motorbike: AdminLotPricing | null };
};

// .NET DayOfWeek: 0 is Sunday.
const DAY_NAMES = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

function Row({
    label,
    children,
}: {
    label: string;
    children: React.ReactNode;
}) {
    return (
        <div className="admin-row">
            <dt>{label}</dt>
            <dd>{children}</dd>
        </div>
    );
}

function openingHoursText(detail: AdminParkingLotDetail) {
    if (!detail.openingHours) return "Not set yet";
    return detail.openingHours.days
        .map((day) => {
            const hours = day.isClosed
                ? "closed"
                : day.is24Hours
                  ? "24h"
                  : `${day.opensAt ?? "?"}–${day.closesAt ?? "?"}`;
            return `${DAY_NAMES[day.dayOfWeek] ?? day.dayOfWeek} ${hours}`;
        })
        .join(" · ");
}

function PriceRows({ price }: { price: AdminLotPricing | null }) {
    if (!price) return <Row label="Price">Not set yet</Row>;
    return (
        <>
            <Row label="Billing">
                {price.billingMode}
                {price.blockMinutes ? ` · ${price.blockMinutes} min block` : ""}
            </Row>
            <Row label="Price">{vnd(price.price)}</Row>
            <Row label="Overnight">{vnd(price.overnightFee)}</Row>
            <Row label="Version">v{price.version}</Row>
        </>
    );
}

function LotDetails({ review }: { review: LotReview }) {
    const { detail, layout, prices } = review;
    const mapUrl = `https://www.google.com/maps?q=${detail.latitude},${detail.longitude}`;
    const zones = layout.floors.flatMap((floor) =>
        floor.zones.map((zone) => ({ floor: floor.name, ...zone })),
    );
    return (
        <div className="admin-detail-grid">
            <section className="admin-card">
                <h2>Parking information</h2>
                <dl>
                    <Row label="Name">{detail.name}</Row>
                    <Row label="Owner account">
                        <code>{detail.ownerUserId ?? "-"}</code>
                    </Row>
                    <Row label="Type">{detail.type}</Row>
                    <Row label="Operating hours">
                        {openingHoursText(detail)}
                    </Row>
                </dl>
            </section>
            <section className="admin-card">
                <h2>Address & GPS</h2>
                <p className="admin-address">
                    <AdminIcon name="pin" size={16} /> {detail.address}
                </p>
                <div className="admin-coords">
                    <code>
                        {Number(detail.latitude).toFixed(5)},{" "}
                        {Number(detail.longitude).toFixed(5)}
                    </code>
                    <a href={mapUrl} target="_blank" rel="noreferrer">
                        Open in Maps ↗
                    </a>
                </div>
            </section>
            <section className="admin-card admin-span-2">
                <h2>Layout (version {layout.version})</h2>
                {zones.length === 0 ? (
                    <p>The owner has not saved a layout yet.</p>
                ) : (
                    <table className="admin-table admin-table-compact">
                        <thead>
                            <tr>
                                <th>Floor</th>
                                <th>Zone</th>
                                <th>Vehicle</th>
                                <th>Capacity</th>
                                <th>Slots</th>
                            </tr>
                        </thead>
                        <tbody>
                            {zones.map((zone) => (
                                <tr key={zone.id}>
                                    <td>{zone.floor}</td>
                                    <td>
                                        <strong>{zone.code}</strong>
                                    </td>
                                    <td>{zone.vehicleGroup}</td>
                                    <td>{zone.capacity}</td>
                                    <td>{zone.slots.length}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </section>
            <section className="admin-card">
                <h2>Car price</h2>
                <dl>
                    <PriceRows price={prices.car} />
                </dl>
            </section>
            <section className="admin-card">
                <h2>Motorbike price</h2>
                <dl>
                    <PriceRows price={prices.motorbike} />
                </dl>
            </section>
        </div>
    );
}

export function ParkingLotReviewPage({ id }: { id: string }) {
    const { t, language } = useLanguage();
    const [review, setReview] = useState<LotReview | null>(null);
    const [loadError, setLoadError] = useState<unknown>(null);
    const [backendResult, setBackendResult] =
        useState<ReviewedParkingLot | null>(null);
    const [dialog, setDialog] = useState<Dialog>(null);
    const [reason, setReason] = useState("");
    const [reasonError, setReasonError] = useState("");
    const [rawSubmitError, setRawSubmitError] = useState<unknown>(null);
    const [submitting, setSubmitting] = useState(false);
    const [toast, setToast] = useState("");

    const submitError = useMemo(() => {
        if (!rawSubmitError) return "";
        if (typeof rawSubmitError === "string") return rawSubmitError;
        return describeReviewError(rawSubmitError, language);
    }, [rawSubmitError, language]);

    useEffect(() => {
        if (!isParkingLotId(id)) return;
        let active = true;
        Promise.all([
            getAdminParkingLotDetail(id),
            getAdminParkingLotLayout(id),
            getAdminParkingLotPricing(id, "Car"),
            getAdminParkingLotPricing(id, "Motorbike"),
        ])
            .then(([detail, layout, car, motorbike]) => {
                if (active)
                    setReview({ detail, layout, prices: { car, motorbike } });
            })
            .catch((error: unknown) => {
                if (active) setLoadError(error);
            });
        return () => {
            active = false;
        };
    }, [id]);

    useEffect(() => {
        if (!toast) return;
        const timer = window.setTimeout(() => setToast(""), 4000);
        return () => window.clearTimeout(timer);
    }, [toast]);

    useEffect(() => {
        if (!dialog) return;
        const onKey = (e: KeyboardEvent) =>
            e.key === "Escape" && !submitting && setDialog(null);
        window.addEventListener("keydown", onKey);
        return () => window.removeEventListener("keydown", onKey);
    }, [dialog, submitting]);

    if (!isParkingLotId(id)) {
        return (
            <section className="admin-card admin-empty-state">
                <h1>
                    {language === "vi"
                        ? "Mã bãi đỗ xe không hợp lệ"
                        : "Invalid parking lot ID"}
                </h1>
                <p>
                    {language === "vi"
                        ? "Đường dẫn phải chứa mã UUID hợp lệ của bãi xe."
                        : "The address must contain a parking lot UUID."}
                </p>
                <Link
                    href="/admin/parking-lots"
                    className="admin-btn admin-btn-dark"
                >
                    {language === "vi"
                        ? "Quay lại danh sách bãi xe"
                        : "Back to parking lots"}
                </Link>
            </section>
        );
    }

    const status =
        backendResult?.approvalStatus ??
        review?.detail.approvalStatus ??
        "Pending";
    const rejectionReason =
        backendResult?.rejectionReason ??
        review?.detail.rejectionReason ??
        null;
    const reviewed = status !== "Pending";
    const name =
        review?.detail.name ??
        (language === "vi" ? "Bãi đỗ xe" : "Parking lot");

    function open(kind: Dialog) {
        setDialog(kind);
        setRawSubmitError(null);
        setReasonError("");
        if (kind === "reject") setReason("");
    }

    async function confirm() {
        if (dialog === "reject") {
            const message = validateRejectionReason(reason, language);
            if (message) {
                setReasonError(message);
                return;
            }
        }
        setSubmitting(true);
        setRawSubmitError(null);
        try {
            const result =
                dialog === "approve"
                    ? await approveParkingLot(id)
                    : await rejectParkingLot(id, reason);
            setBackendResult(result);
            void refreshAdminLots();
            setDialog(null);
            setToast(
                result.approvalStatus === "Approved"
                    ? language === "vi"
                        ? `${name} đã được duyệt và hiển thị công khai cho tài xế.`
                        : `${name} is now active and visible to drivers.`
                    : language === "vi"
                      ? `${name} đã bị từ chối. Chủ bãi sẽ thấy lý do chi tiết.`
                      : `${name} was rejected. The owner will see the reason.`,
            );
        } catch (error) {
            setRawSubmitError(error);
        } finally {
            setSubmitting(false);
        }
    }

    return (
        <>
            <Link href="/admin/parking-lots/pending" className="admin-back">
                <AdminIcon name="back" size={16} /> Back to review queue
            </Link>
            <header className="admin-heading admin-heading-detail">
                <div>
                    <p className="admin-eyebrow">Parking lot approval</p>
                    <h1>{name}</h1>
                    <p className="admin-id">
                        ID <code>{id}</code>
                    </p>
                </div>
                <StatusPill status={status} />
            </header>

            {!review && !loadError && (
                <p className="admin-sample-note">Loading…</p>
            )}
            {loadError !== null && (
                <p className="admin-sample-note" role="alert">
                    <AdminIcon name="alert" size={15} />
                    {describeReviewError(loadError, language)}
                </p>
            )}

            {status === "Rejected" && rejectionReason && (
                <div className="admin-callout admin-callout-rejected">
                    <strong>Rejection reason shown to the owner</strong>
                    <p>{rejectionReason}</p>
                </div>
            )}
            {status === "Approved" && (
                <div className="admin-callout admin-callout-approved">
                    <strong>Active</strong>
                    <p>
                        This lot is approved and appears in the public catalog.
                    </p>
                </div>
            )}

            {review && <LotDetails review={review} />}

            <div className={`admin-decision ${reviewed ? "is-final" : ""}`}>
                <div>
                    <strong>
                        {reviewed ? "Decision recorded" : "Your decision"}
                    </strong>
                    <small>
                        {reviewed
                            ? "Decisions are final."
                            : "Approving makes the lot active for drivers. Rejecting requires a reason."}
                    </small>
                </div>
                {!reviewed && review && (
                    <div className="admin-decision-actions">
                        <button
                            type="button"
                            className="admin-btn admin-btn-danger-ghost"
                            id="review-reject"
                            onClick={() => open("reject")}
                        >
                            <AdminIcon name="close" size={16} /> Reject
                        </button>
                        <button
                            type="button"
                            className="admin-btn admin-btn-lime"
                            id="review-approve"
                            onClick={() => open("approve")}
                        >
                            <AdminIcon name="check" size={16} /> Approve
                        </button>
                    </div>
                )}
            </div>

            {dialog && (
                <div
                    className="admin-modal-backdrop"
                    onClick={() => !submitting && setDialog(null)}
                >
                    <div
                        className="admin-modal"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="review-dialog-title"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <span className={`admin-modal-icon ${dialog}`}>
                            <AdminIcon
                                name={dialog === "approve" ? "check" : "close"}
                                size={22}
                            />
                        </span>
                        <h2 id="review-dialog-title">
                            {dialog === "approve"
                                ? "Approve this parking lot?"
                                : "Reject this parking lot?"}
                        </h2>
                        <p>
                            {dialog === "approve"
                                ? `${name} becomes active and visible to drivers. This can't be undone here.`
                                : "Tell the owner what needs to change. They'll see this reason on their parking lot."}
                        </p>
                        {dialog === "reject" && (
                            <label className="admin-field">
                                <span>Reason</span>
                                <textarea
                                    id="review-reject-reason"
                                    rows={4}
                                    autoFocus
                                    value={reason}
                                    maxLength={MAX_REJECTION_REASON + 50}
                                    aria-invalid={Boolean(reasonError)}
                                    onChange={(e) => {
                                        setReason(e.target.value);
                                        setReasonError("");
                                    }}
                                    placeholder="e.g. Missing required fire safety documentation"
                                />
                                <small
                                    className={
                                        reason.trim().length >
                                        MAX_REJECTION_REASON
                                            ? "over"
                                            : undefined
                                    }
                                >
                                    {reason.trim().length}/
                                    {MAX_REJECTION_REASON}
                                </small>
                                {reasonError && (
                                    <em role="alert">{reasonError}</em>
                                )}
                            </label>
                        )}
                        {submitError && (
                            <p className="admin-modal-error" role="alert">
                                {submitError}
                            </p>
                        )}
                        <div className="admin-modal-actions">
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost"
                                onClick={() => setDialog(null)}
                                disabled={submitting}
                            >
                                Cancel
                            </button>
                            <button
                                type="button"
                                id="review-confirm"
                                className={`admin-btn ${dialog === "approve" ? "admin-btn-lime" : "admin-btn-danger"}`}
                                onClick={confirm}
                                disabled={submitting}
                            >
                                {submitting
                                    ? "Saving…"
                                    : dialog === "approve"
                                      ? "Yes, approve"
                                      : "Reject lot"}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {toast && (
                <div className="admin-toast" role="status">
                    {toast}
                </div>
            )}
        </>
    );
}
