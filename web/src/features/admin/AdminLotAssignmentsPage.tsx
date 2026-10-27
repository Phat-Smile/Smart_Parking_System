"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { AdminIcon } from "./AdminUi";
import { getAdminParkingLots } from "./reviewParkingLot";
import {
    assignLotMember,
    describeAssignmentError,
    getAvailableStaffAccounts,
    getLotAssignments,
    removeLotMember,
    type StaffAccountOption,
} from "./lotAssignmentsApi";
import type { AdminParkingLot, ParkingLotAssignmentMember } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function AdminLotAssignmentsPage() {
    const { t, language } = useLanguage();
    const [lots, setLots] = useState<AdminParkingLot[]>([]);
    const [selectedLotId, setSelectedLotId] = useState<string>("");
    const [assignments, setAssignments] = useState<
        ParkingLotAssignmentMember[]
    >([]);
    const [availableStaff, setAvailableStaff] = useState<StaffAccountOption[]>(
        [],
    );

    const [loadingLots, setLoadingLots] = useState(false);
    const [loadingAssignments, setLoadingAssignments] = useState(false);
    const [actionBusy, setActionBusy] = useState(false);

    // Form gán nhân viên mới
    const [selectedAccountId, setSelectedAccountId] = useState("");
    const [selectedRole, setSelectedRole] = useState<"Staff" | "Owner">(
        "Staff",
    );

    const [rawError, setRawError] = useState<unknown>(null);
    const [toast, setToast] = useState<string | null>(null);

    const errorMsg = useMemo(() => {
        if (!rawError) return "";
        if (typeof rawError === "string") return rawError;
        return describeAssignmentError(rawError, language);
    }, [rawError, language]);

    // Load danh sách bãi xe và nhân viên
    const loadInitialData = useCallback(async () => {
        setLoadingLots(true);
        setRawError(null);
        try {
            const [lotList, staffList] = await Promise.all([
                getAdminParkingLots("All"),
                getAvailableStaffAccounts(),
            ]);
            setLots(lotList);
            setAvailableStaff(staffList);
            if (lotList.length > 0 && !selectedLotId) {
                setSelectedLotId(lotList[0].id);
            }
        } catch (err) {
            setRawError(err);
        } finally {
            setLoadingLots(false);
        }
    }, [selectedLotId]);

    // Load danh sách phân công theo bãi được chọn
    const loadAssignments = useCallback(async (lotId: string) => {
        if (!lotId) return;
        setLoadingAssignments(true);
        try {
            const data = await getLotAssignments(lotId);
            setAssignments(data);
        } catch (err) {
            setRawError(err);
        } finally {
            setLoadingAssignments(false);
        }
    }, []);

    useEffect(() => {
        let active = true;
        Promise.all([getAdminParkingLots("All"), getAvailableStaffAccounts()])
            .then(([lotList, staffList]) => {
                if (!active) return;
                setLots(lotList);
                setAvailableStaff(staffList);
                if (lotList.length > 0) {
                    setSelectedLotId((prev) => prev || lotList[0].id);
                }
            })
            .catch((err) => {
                if (active) setRawError(err);
            });
        return () => {
            active = false;
        };
    }, []);

    useEffect(() => {
        if (!selectedLotId) return;
        let active = true;
        getLotAssignments(selectedLotId)
            .then((data) => {
                if (active) setAssignments(data);
            })
            .catch((err) => {
                if (active) setRawError(err);
            });
        return () => {
            active = false;
        };
    }, [selectedLotId]);

    useEffect(() => {
        if (!toast) return;
        const timer = setTimeout(() => setToast(null), 4000);
        return () => clearTimeout(timer);
    }, [toast]);

    const activeLot = useMemo(
        () => lots.find((l) => l.id === selectedLotId) || null,
        [lots, selectedLotId],
    );

    // Lọc danh sách nhân viên chưa được gán vào bãi này
    const assignableStaff = useMemo(() => {
        const assignedIds = new Set(assignments.map((a) => a.accountId));
        return availableStaff.filter((s) => !assignedIds.has(s.accountId));
    }, [availableStaff, assignments]);

    // Xử lý gán nhân viên (PUT)
    async function handleAssignMember(e: React.FormEvent) {
        e.preventDefault();
        if (!selectedLotId || !selectedAccountId) return;

        setActionBusy(true);
        setRawError(null);
        try {
            await assignLotMember(
                selectedLotId,
                selectedAccountId,
                selectedRole,
            );
            const staff = availableStaff.find(
                (s) => s.accountId === selectedAccountId,
            );
            setToast(
                `${t("approved")}: ${staff?.fullName || "staff"} ➔ ${activeLot?.name || ""}`,
            );
            setSelectedAccountId("");
            await loadAssignments(selectedLotId);
        } catch (err) {
            setRawError(err);
        } finally {
            setActionBusy(false);
        }
    }

    // Xử lý gỡ nhân viên (DELETE)
    async function handleRemoveMember(accountId: string, memberName: string) {
        const confirmText = t("assignmentsRemoveConfirm").replace(
            "{name}",
            memberName,
        );
        if (!confirm(confirmText)) return;

        setActionBusy(true);
        setRawError(null);
        try {
            await removeLotMember(selectedLotId, accountId);
            setToast(
                `${t("removeStaffBtn")}: ${memberName} (${activeLot?.name || ""})`,
            );
            await loadAssignments(selectedLotId);
        } catch (err) {
            setRawError(err);
        } finally {
            setActionBusy(false);
        }
    }

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">{t("assignmentsEyebrow")}</p>
                    <h1>{t("assignmentsTitle")}</h1>
                </div>
                <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                    <Link
                        href="/admin/parking-lots"
                        className="admin-btn admin-btn-ghost"
                    >
                        {t("assignmentsBackToLots")}
                    </Link>
                    <Link
                        href="/admin/owner-applications"
                        className="admin-btn admin-btn-ghost"
                    >
                        {t("assignmentsOwnerAppsLink")}
                    </Link>
                    <button
                        type="button"
                        onClick={() => {
                            loadInitialData();
                            if (selectedLotId) loadAssignments(selectedLotId);
                        }}
                        className="admin-btn admin-btn-ghost"
                        disabled={loadingLots || loadingAssignments}
                    >
                        🔄 {t("refresh")}
                    </button>
                </div>
            </header>

            {errorMsg && (
                <div
                    className="admin-banner"
                    role="alert"
                    style={{
                        background: "rgba(239, 68, 68, 0.15)",
                        border: "1px solid #ef4444",
                        color: "#f87171",
                    }}
                >
                    <AdminIcon name="alert" size={16} />
                    <span>{errorMsg}</span>
                    <button
                        type="button"
                        onClick={() => setRawError(null)}
                        style={{
                            marginLeft: "auto",
                            background: "none",
                            border: "none",
                            color: "inherit",
                            cursor: "pointer",
                        }}
                    >
                        ✕
                    </button>
                </div>
            )}

            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "320px 1fr",
                    gap: "24px",
                    alignItems: "start",
                }}
            >
                {/* Cột trái: Chọn bãi đỗ xe */}
                <section className="admin-card" style={{ padding: "16px" }}>
                    <h2
                        style={{
                            fontSize: "1rem",
                            margin: "0 0 12px",
                            display: "flex",
                            alignItems: "center",
                            gap: "8px",
                        }}
                    >
                        <span>🏢</span> {t("selectLotTitle")}
                    </h2>

                    {loadingLots ? (
                        <p className="admin-empty">
                            {t("assignmentsLoadingLots")}
                        </p>
                    ) : (
                        <div
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                gap: "8px",
                                maxHeight: "65vh",
                                overflowY: "auto",
                            }}
                        >
                            {lots.map((lot) => {
                                const isSelected = lot.id === selectedLotId;
                                return (
                                    <button
                                        key={lot.id}
                                        type="button"
                                        onClick={() => setSelectedLotId(lot.id)}
                                        style={{
                                            textAlign: "left",
                                            padding: "12px",
                                            borderRadius: "8px",
                                            border: `1px solid ${isSelected ? "var(--admin-lime, #a3e635)" : "#e5e7eb"}`,
                                            background: isSelected
                                                ? "rgba(163, 230, 53, 0.15)"
                                                : "#ffffff",
                                            color: "#111827",
                                            cursor: "pointer",
                                            transition: "all 0.15s ease",
                                        }}
                                    >
                                        <div
                                            style={{
                                                fontWeight: 600,
                                                fontSize: "0.95rem",
                                                color: "#111827",
                                            }}
                                        >
                                            {lot.name}
                                        </div>
                                        <div
                                            style={{
                                                fontSize: "0.8rem",
                                                color: "#4b5563",
                                                marginTop: "4px",
                                            }}
                                        >
                                            {t("lotOwnerHeader")}:{" "}
                                            {lot.ownerName}
                                        </div>
                                        <div
                                            style={{
                                                fontSize: "0.75rem",
                                                color: "#6b7280",
                                            }}
                                        >
                                            {lot.address || lot.city}
                                        </div>
                                    </button>
                                );
                            })}
                        </div>
                    )}
                </section>

                {/* Cột phải: Quản lý nhân viên của bãi đã chọn */}
                <div
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        gap: "20px",
                    }}
                >
                    {/* Bảng phân công hiện tại */}
                    <section className="admin-card admin-table-card">
                        <div
                            style={{
                                padding: "16px 20px",
                                borderBottom:
                                    "1px solid rgba(255,255,255,0.08)",
                            }}
                        >
                            <h2 style={{ fontSize: "1.1rem", margin: 0 }}>
                                {t("staffAtLotTitle")}:{" "}
                                {activeLot ? activeLot.name : "..."}
                            </h2>
                            <p
                                style={{
                                    margin: "4px 0 0",
                                    fontSize: "0.85rem",
                                    color: "var(--staff-text-muted, #94a3b8)",
                                }}
                            >
                                {t("staffAtLotSubtitle")}
                            </p>
                        </div>

                        <table className="admin-table">
                            <thead>
                                <tr>
                                    <th>{t("assignmentsColStaff")}</th>
                                    <th>{t("assignmentsColContact")}</th>
                                    <th>{t("assignmentsColRole")}</th>
                                    <th style={{ textAlign: "right" }}>
                                        {t("actions")}
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {assignments.map((member) => (
                                    <tr key={member.accountId}>
                                        <td>
                                            <strong>
                                                {member.accountFullName ||
                                                    t(
                                                        "assignmentsDefaultStaffName",
                                                    )}
                                            </strong>
                                            <small
                                                style={{
                                                    fontFamily: "monospace",
                                                    opacity: 0.7,
                                                }}
                                            >
                                                ID:{" "}
                                                {member.accountId.slice(0, 8)}
                                                ...
                                            </small>
                                        </td>
                                        <td>
                                            <div>
                                                {member.accountEmail || "—"}
                                            </div>
                                            <small>
                                                {member.accountPhoneNumber ||
                                                    "—"}
                                            </small>
                                        </td>
                                        <td>
                                            <span
                                                style={{
                                                    display: "inline-block",
                                                    padding: "2px 8px",
                                                    borderRadius: "4px",
                                                    fontSize: "0.75rem",
                                                    fontWeight: 600,
                                                    background:
                                                        member.role === "Owner"
                                                            ? "rgba(168, 85, 247, 0.15)"
                                                            : "rgba(59, 130, 246, 0.15)",
                                                    color:
                                                        member.role === "Owner"
                                                            ? "#c084fc"
                                                            : "#60a5fa",
                                                    border: `1px solid ${member.role === "Owner" ? "rgba(168, 85, 247, 0.3)" : "rgba(59, 130, 246, 0.3)"}`,
                                                }}
                                            >
                                                {member.role === "Owner"
                                                    ? t("roleOwner")
                                                    : t("roleStaff")}
                                            </span>
                                        </td>
                                        <td style={{ textAlign: "right" }}>
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-xs admin-btn-danger-ghost"
                                                onClick={() =>
                                                    handleRemoveMember(
                                                        member.accountId,
                                                        member.accountFullName ||
                                                            member.accountEmail ||
                                                            "staff",
                                                    )
                                                }
                                                disabled={actionBusy}
                                                title={t("removeStaffBtn")}
                                            >
                                                {t("removeStaffBtn")}
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>

                        {assignments.length === 0 && !loadingAssignments && (
                            <p className="admin-empty">
                                {t("noStaffAssigned")}
                            </p>
                        )}

                        {loadingAssignments && (
                            <p className="admin-empty">
                                {t("assignmentsLoadingAssignments")}
                            </p>
                        )}
                    </section>

                    {/* Biểu mẫu gán nhân viên mới vào bãi xe */}
                    <section className="admin-card" style={{ padding: "20px" }}>
                        <h3
                            style={{
                                fontSize: "1rem",
                                margin: "0 0 12px",
                                display: "flex",
                                alignItems: "center",
                                gap: "8px",
                            }}
                        >
                            <span>➕</span> {t("assignStaffTitle")}
                        </h3>

                        <form
                            onSubmit={handleAssignMember}
                            style={{
                                display: "grid",
                                gridTemplateColumns: "1fr 180px auto",
                                gap: "12px",
                                alignItems: "end",
                            }}
                        >
                            <label
                                className="admin-field"
                                style={{ margin: 0 }}
                            >
                                <span
                                    style={{
                                        fontWeight: 600,
                                        color: "#111827",
                                        marginBottom: "4px",
                                        display: "block",
                                    }}
                                >
                                    {t("selectStaffLabel")}
                                </span>
                                <select
                                    value={selectedAccountId}
                                    onChange={(e) =>
                                        setSelectedAccountId(e.target.value)
                                    }
                                    required
                                    style={{
                                        width: "100%",
                                        height: "42px",
                                        padding: "8px 12px",
                                        borderRadius: "8px",
                                        background: "#ffffff",
                                        color: "#111827",
                                        border: "1px solid #d1d5db",
                                        fontSize: "0.875rem",
                                        outline: "none",
                                    }}
                                >
                                    <option
                                        value=""
                                        style={{
                                            background: "#ffffff",
                                            color: "#111827",
                                        }}
                                    >
                                        {t("selectStaffDefault")}
                                    </option>
                                    {assignableStaff.map((staff) => (
                                        <option
                                            key={staff.accountId}
                                            value={staff.accountId}
                                            style={{
                                                background: "#ffffff",
                                                color: "#111827",
                                            }}
                                        >
                                            {staff.fullName} ({staff.email})
                                        </option>
                                    ))}
                                </select>
                            </label>

                            <label
                                className="admin-field"
                                style={{ margin: 0 }}
                            >
                                <span
                                    style={{
                                        fontWeight: 600,
                                        color: "#111827",
                                        marginBottom: "4px",
                                        display: "block",
                                    }}
                                >
                                    {t("assignRoleLabel")}
                                </span>
                                <select
                                    value={selectedRole}
                                    onChange={(e) =>
                                        setSelectedRole(
                                            e.target.value as "Staff" | "Owner",
                                        )
                                    }
                                    style={{
                                        width: "100%",
                                        height: "42px",
                                        padding: "8px 12px",
                                        borderRadius: "8px",
                                        background: "#ffffff",
                                        color: "#111827",
                                        border: "1px solid #d1d5db",
                                        fontSize: "0.875rem",
                                        outline: "none",
                                    }}
                                >
                                    <option
                                        value="Staff"
                                        style={{
                                            background: "#ffffff",
                                            color: "#111827",
                                        }}
                                    >
                                        {t("roleStaff")}
                                    </option>
                                    <option
                                        value="Owner"
                                        style={{
                                            background: "#ffffff",
                                            color: "#111827",
                                        }}
                                    >
                                        {t("roleOwner")}
                                    </option>
                                </select>
                            </label>

                            <button
                                type="submit"
                                className="admin-btn admin-btn-lime"
                                disabled={
                                    actionBusy ||
                                    !selectedAccountId ||
                                    !selectedLotId
                                }
                                style={{ height: "42px", fontWeight: 600 }}
                            >
                                {actionBusy
                                    ? t("assignmentsAssigning")
                                    : t("assignBtn")}
                            </button>
                        </form>
                    </section>
                </div>
            </div>

            {toast && (
                <div className="admin-toast" role="status">
                    {toast}
                </div>
            )}
        </>
    );
}
