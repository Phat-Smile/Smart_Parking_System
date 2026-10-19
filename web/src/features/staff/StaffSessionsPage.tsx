"use client";

import React, { useState } from "react";
import { PlateBadge, QrCodeModal, StatusPill, FormattedTime, formatVnd } from "./StaffUi";
import { cancelParkingSession, describeStaffError } from "./staffApi";
import { useStaffData } from "./staffData";
import type { CustomerType, StaffSession } from "./types";

export function StaffSessionsPage() {
    const { sessions, cancelSession } = useStaffData();

    const [filterType, setFilterType] = useState<CustomerType | "All">("All");
    const [search, setSearch] = useState("");
    const [selectedQrSession, setSelectedQrSession] = useState<StaffSession | null>(null);

    // Cancel modal state
    const [cancellingSession, setCancellingSession] = useState<StaffSession | null>(null);
    const [cancelReason, setCancelReason] = useState("Vehicle driver requested turn around");
    const [cancelBusy, setCancelBusy] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");

    const filtered = sessions
        .filter((s) => (filterType === "All" ? true : s.customerType === filterType))
        .filter(
            (s) =>
                !search.trim() ||
                s.plateNumber.toLowerCase().includes(search.trim().toLowerCase()) ||
                s.slotCode.toLowerCase().includes(search.trim().toLowerCase()) ||
                (s.driverName && s.driverName.toLowerCase().includes(search.trim().toLowerCase())),
        );

    async function handleConfirmCancel() {
        if (!cancellingSession) return;
        setCancelBusy(true);
        setErrorMsg("");

        try {
            // Call POST /api/parking/sessions/{id}/cancel
            await cancelParkingSession(cancellingSession.id, cancelReason);
            cancelSession(cancellingSession.id);
            setCancellingSession(null);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setCancelBusy(false);
        }
    }

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">Guest &amp; Active Session Management</h1>
                    <p className="staff-subtitle">
                        Inspect active parked vehicles, view guest QR tickets, and handle cancellations.
                    </p>
                </div>
            </header>

            {errorMsg && (
                <div className="staff-alert staff-alert-danger">
                    <span>⚠️ {errorMsg}</span>
                    <button type="button" onClick={() => setErrorMsg("")}>
                        ✕
                    </button>
                </div>
            )}

            {/* Filter toolbar */}
            <div className="staff-toolbar">
                <div className="staff-search-box">
                    <span className="search-icon">🔍</span>
                    <input
                        type="text"
                        className="staff-input staff-search-input"
                        placeholder="Search license plate, slot, or driver name..."
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                    />
                </div>

                <div className="staff-filter-tabs">
                    {(["All", "Guest", "Registered"] as const).map((tab) => (
                        <button
                            key={tab}
                            type="button"
                            className={`staff-filter-btn ${filterType === tab ? "active" : ""}`}
                            onClick={() => setFilterType(tab)}
                        >
                            {tab === "Guest"
                                ? "Guest Vehicles"
                                : tab === "Registered"
                                  ? "Registered App Users"
                                  : "All Sessions"}
                        </button>
                    ))}
                </div>
            </div>

            {/* Sessions table */}
            <div className="staff-card staff-table-card">
                <table className="staff-table">
                    <thead>
                        <tr>
                            <th>License Plate</th>
                            <th>Category</th>
                            <th>Vehicle</th>
                            <th>Spot</th>
                            <th>Entry Time</th>
                            <th>Session Status</th>
                            <th>Payment Status</th>
                            <th>Session QR</th>
                            <th className="text-right">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.length === 0 ? (
                            <tr>
                                <td colSpan={9} className="staff-empty">
                                    No parking sessions match your search.
                                </td>
                            </tr>
                        ) : (
                            filtered.map((s) => (
                                <tr key={s.id}>
                                    <td>
                                        <PlateBadge plate={s.plateNumber} />
                                    </td>
                                    <td>
                                        <span
                                            className={`staff-cust-type ${
                                                s.customerType === "Guest"
                                                    ? "type-guest"
                                                    : "type-reg"
                                            }`}
                                        >
                                            {s.customerType}
                                        </span>
                                    </td>
                                    <td>{s.vehicleType}</td>
                                    <td>
                                        <span className="staff-slot-code">
                                            {s.slotCode}
                                        </span>
                                    </td>
                                    <td>
                                        <FormattedTime iso={s.entryTime} dateTime />
                                    </td>
                                    <td>
                                        <StatusPill status={s.status} />
                                    </td>
                                    <td>
                                        <StatusPill status={s.paymentStatus} />
                                        {s.amountDueVnd > 0 && (
                                            <small className="staff-due-badge">
                                                Due: {formatVnd(s.amountDueVnd)}
                                            </small>
                                        )}
                                    </td>
                                    <td>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-ghost staff-btn-sm"
                                            onClick={() => setSelectedQrSession(s)}
                                        >
                                            🎟️ View QR
                                        </button>
                                    </td>
                                    <td className="text-right">
                                        {s.status === "Active" && (
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-danger-ghost staff-btn-sm"
                                                onClick={() =>
                                                    setCancellingSession(s)
                                                }
                                            >
                                                Cancel Entry
                                            </button>
                                        )}
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

            {/* QR View Modal */}
            {selectedQrSession && (
                <QrCodeModal
                    qrToken={selectedQrSession.qrToken}
                    plateNumber={selectedQrSession.plateNumber}
                    vehicleType={selectedQrSession.vehicleType}
                    entryTime={selectedQrSession.entryTime}
                    onClose={() => setSelectedQrSession(null)}
                />
            )}

            {/* Cancel Session Modal */}
            {cancellingSession && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => setCancellingSession(null)}
                >
                    <div
                        className="staff-modal-card"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="staff-modal-head">
                            <h3>Cancel Parking Session</h3>
                            <button
                                type="button"
                                className="staff-modal-close"
                                onClick={() => setCancellingSession(null)}
                            >
                                ✕
                            </button>
                        </div>

                        <div className="staff-modal-body">
                            <p>
                                Are you sure you want to cancel the session for{" "}
                                <strong>{cancellingSession.plateNumber}</strong>?
                                Use this when the barrier was opened but the vehicle did
                                not enter.
                            </p>

                            <div className="staff-form-group">
                                <label htmlFor="cancel-reason">Reason for cancellation</label>
                                <textarea
                                    id="cancel-reason"
                                    className="staff-textarea"
                                    rows={3}
                                    value={cancelReason}
                                    onChange={(e) => setCancelReason(e.target.value)}
                                    placeholder="Enter reason..."
                                />
                            </div>

                            <div className="staff-modal-actions">
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary"
                                    onClick={() => setCancellingSession(null)}
                                >
                                    Go back
                                </button>
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-danger"
                                    disabled={cancelBusy || !cancelReason.trim()}
                                    onClick={handleConfirmCancel}
                                >
                                    {cancelBusy
                                        ? "Cancelling..."
                                        : "Confirm Cancellation"}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
