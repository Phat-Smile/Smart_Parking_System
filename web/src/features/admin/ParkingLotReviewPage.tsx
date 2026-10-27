"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { ApiError } from "@/shared/errors/api-error";
import { AdminIcon, StatusPill, formatDate, vnd } from "./AdminUi";
import { findLot, recordDecision, useAdminLots } from "./adminLotsData";
import {
    MAX_REJECTION_REASON,
    approveParkingLot,
    describeReviewError,
    isParkingLotId,
    rejectParkingLot,
    validateRejectionReason,
} from "./reviewParkingLot";
import type {
    AdminParkingLot,
    LotFloorLayout,
    ReviewedParkingLot,
} from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

type Dialog = "approve" | "reject" | null;

function FloorPlan({ floor }: { floor: LotFloorLayout }) {
    const blocked = new Set(floor.blocked);
    return (
        <figure className="admin-floor">
            <figcaption>{floor.name}</figcaption>
            <div
                className="admin-floor-grid"
                style={{ gridTemplateColumns: `repeat(${floor.cols}, 1fr)` }}
            >
                {Array.from({ length: floor.rows * floor.cols }, (_, i) => (
                    <span
                        key={i}
                        className={blocked.has(i) ? "aisle" : undefined}
                    />
                ))}
            </div>
        </figure>
    );
}

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

function LotDetails({ lot }: { lot: AdminParkingLot }) {
    const mapUrl = `https://www.google.com/maps?q=${lot.latitude},${lot.longitude}`;
    return (
        <div className="admin-detail-grid">
            <section className="admin-card">
                <h2>Parking information</h2>
                <dl>
                    <Row label="Name">{lot.name}</Row>
                    <Row label="Owner">
                        {lot.ownerName} · {lot.ownerEmail}
                    </Row>
                    <Row label="Submitted">
                        {formatDate(lot.submittedAtUtc)}
                    </Row>
                    <Row label="Operating hours">
                        {lot.is24h
                            ? "Open 24/7"
                            : `${lot.openTime} – ${lot.closeTime}`}
                    </Row>
                    <Row label="Vehicle types">
                        <span className="admin-chips">
                            {lot.vehicleTypes.map((t) => (
                                <span key={t}>{t}</span>
                            ))}
                        </span>
                    </Row>
                    <Row label="Total capacity">{lot.totalCapacity} slots</Row>
                </dl>
            </section>
            <section className="admin-card">
                <h2>Address & GPS</h2>
                <p className="admin-address">
                    <AdminIcon name="pin" size={16} /> {lot.address}, {lot.city}
                </p>
                <div className="admin-coords">
                    <code>
                        {lot.latitude.toFixed(5)}, {lot.longitude.toFixed(5)}
                    </code>
                    <a href={mapUrl} target="_blank" rel="noreferrer">
                        Open in Maps ↗
                    </a>
                </div>
            </section>
            <section className="admin-card admin-span-2">
                <h2>Zones</h2>
                <table className="admin-table admin-table-compact">
                    <thead>
                        <tr>
                            <th>Zone</th>
                            <th>Vehicle</th>
                            <th>Slots</th>
                            <th>Hourly price</th>
                        </tr>
                    </thead>
                    <tbody>
                        {lot.zones.map((zone) => (
                            <tr key={zone.id}>
                                <td>
                                    <strong>{zone.name}</strong>
                                </td>
                                <td>{zone.vehicleType}</td>
                                <td>{zone.slots}</td>
                                <td>{vnd(zone.pricePerHourVnd)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </section>
            <section className="admin-card">
                <h2>Pricing</h2>
                <dl>
                    <Row label="First hour">
                        {vnd(lot.pricing.firstHourVnd)}
                    </Row>
                    <Row label="Each extra hour">
                        {vnd(lot.pricing.extraHourVnd)}
                    </Row>
                    <Row label="Daily cap">{vnd(lot.pricing.dailyCapVnd)}</Row>
                    <Row label="Overnight">{vnd(lot.pricing.overnightVnd)}</Row>
                </dl>
            </section>
            <section className="admin-card">
                <h2>Hold deposit & reservation policy</h2>
                <dl>
                    <Row label="Hold deposit">{vnd(lot.holdDepositVnd)}</Row>
                    <Row label="Hold window">
                        {lot.reservation.holdMinutes} min
                    </Row>
                    <Row label="Free cancellation">
                        first {lot.reservation.freeCancelMinutes} min
                    </Row>
                    <Row label="No-show">{lot.reservation.noShowRule}</Row>
                </dl>
            </section>
            <section className="admin-card admin-span-2">
                <div className="admin-card-head">
                    <h2>2D layout</h2>
                    <span className="admin-legend">
                        <i /> Slot <i className="aisle" /> Aisle
                    </span>
                </div>
                <div className="admin-floors">
                    {lot.layout.map((floor) => (
                        <FloorPlan key={floor.name} floor={floor} />
                    ))}
                </div>
            </section>
        </div>
    );
}

export function ParkingLotReviewPage({ id }: { id: string }) {
    const { t, language } = useLanguage();
    const lots = useAdminLots();
    const sample = findLot(lots, id);
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

    const status = sample?.status ?? backendResult?.approvalStatus ?? "Pending";
    const rejectionReason =
        sample?.rejectionReason ?? backendResult?.rejectionReason ?? null;
    const reviewed = status !== "Pending";
    const name =
        sample?.name ?? (language === "vi" ? "Bãi đỗ xe" : "Parking lot");

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
            recordDecision(result);
            setBackendResult(result);
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
            // When backend is offline and inspecting sample lot, fallback to local decision overlay for UI preview
            if (sample && !(error instanceof ApiError)) {
                const fallbackResult: ReviewedParkingLot = {
                    parkingLotId: id,
                    approvalStatus:
                        dialog === "approve" ? "Approved" : "Rejected",
                    rejectionReason: dialog === "reject" ? reason.trim() : null,
                };
                recordDecision(fallbackResult);
                setBackendResult(fallbackResult);
                setDialog(null);
                setToast(
                    fallbackResult.approvalStatus === "Approved"
                        ? language === "vi"
                            ? `${name} đã được duyệt và hiển thị công khai (chế độ xem trước).`
                            : `${name} is now active and visible to drivers (preview mode).`
                        : language === "vi"
                          ? `${name} đã bị từ chối (chế độ xem trước).`
                          : `${name} was rejected (preview mode).`,
                );
                return;
            }
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

            {!sample && (
                <p className="admin-sample-note">
                    <AdminIcon name="alert" size={15} />
                    Details for this ID aren&apos;t available yet (no admin
                    detail endpoint). You can still record a decision; the
                    backend validates that the lot exists and is Pending.
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

            {sample && <LotDetails lot={sample} />}

            <div className={`admin-decision ${reviewed ? "is-final" : ""}`}>
                <div>
                    <strong>
                        {reviewed ? "Decision recorded" : "Your decision"}
                    </strong>
                    <small>
                        {reviewed
                            ? `Reviewed ${formatDate(sample?.reviewedAtUtc ?? null)}. Decisions are final.`
                            : "Approving makes the lot active for drivers. Rejecting requires a reason."}
                    </small>
                </div>
                {!reviewed && (
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
