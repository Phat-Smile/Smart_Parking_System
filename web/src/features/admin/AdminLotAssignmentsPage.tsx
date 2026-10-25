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

export function AdminLotAssignmentsPage() {
    const [lots, setLots] = useState<AdminParkingLot[]>([]);
    const [selectedLotId, setSelectedLotId] = useState<string>("");
    const [assignments, setAssignments] = useState<ParkingLotAssignmentMember[]>([]);
    const [availableStaff, setAvailableStaff] = useState<StaffAccountOption[]>([]);

    const [loadingLots, setLoadingLots] = useState(false);
    const [loadingAssignments, setLoadingAssignments] = useState(false);
    const [actionBusy, setActionBusy] = useState(false);

    // Form gán nhân viên mới
    const [selectedAccountId, setSelectedAccountId] = useState("");
    const [selectedRole, setSelectedRole] = useState<"Staff" | "Owner">("Staff");

    const [errorMsg, setErrorMsg] = useState("");
    const [toast, setToast] = useState<string | null>(null);

    // Load danh sách bãi xe và nhân viên
    const loadInitialData = useCallback(async () => {
        setLoadingLots(true);
        setErrorMsg("");
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
            setErrorMsg(describeAssignmentError(err));
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
            setErrorMsg(describeAssignmentError(err));
        } finally {
            setLoadingAssignments(false);
        }
    }, []);

    useEffect(() => {
        let active = true;
        Promise.all([
            getAdminParkingLots("All"),
            getAvailableStaffAccounts(),
        ])
            .then(([lotList, staffList]) => {
                if (!active) return;
                setLots(lotList);
                setAvailableStaff(staffList);
                if (lotList.length > 0) {
                    setSelectedLotId((prev) => prev || lotList[0].id);
                }
            })
            .catch((err) => {
                if (active) setErrorMsg(describeAssignmentError(err));
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
                if (active) setErrorMsg(describeAssignmentError(err));
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
        setErrorMsg("");
        try {
            await assignLotMember(selectedLotId, selectedAccountId, selectedRole);
            const staff = availableStaff.find((s) => s.accountId === selectedAccountId);
            setToast(`Đã gán ${staff?.fullName || "tài khoản"} vào bãi ${activeLot?.name || ""}.`);
            setSelectedAccountId("");
            await loadAssignments(selectedLotId);
        } catch (err) {
            setErrorMsg(describeAssignmentError(err));
        } finally {
            setActionBusy(false);
        }
    }

    // Xử lý gỡ nhân viên (DELETE)
    async function handleRemoveMember(accountId: string, memberName: string) {
        if (!confirm(`Bạn có chắc muốn gỡ ${memberName} khỏi bãi đỗ xe này?`)) return;

        setActionBusy(true);
        setErrorMsg("");
        try {
            await removeLotMember(selectedLotId, accountId);
            setToast(`Đã gỡ ${memberName} khỏi bãi ${activeLot?.name || ""}.`);
            await loadAssignments(selectedLotId);
        } catch (err) {
            setErrorMsg(describeAssignmentError(err));
        } finally {
            setActionBusy(false);
        }
    }

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">Quản trị vận hành</p>
                    <h1>Phân công Nhân viên trực bãi</h1>
                </div>
                <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                    <Link
                        href="/admin/parking-lots"
                        className="admin-btn admin-btn-ghost"
                    >
                        ← Danh sách bãi xe
                    </Link>
                    <Link
                        href="/admin/owner-applications"
                        className="admin-btn admin-btn-ghost"
                    >
                        Duyệt hồ sơ Chủ bãi
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
                        🔄 Làm mới
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
                        onClick={() => setErrorMsg("")}
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
                    <h2 style={{ fontSize: "1rem", margin: "0 0 12px", display: "flex", alignItems: "center", gap: "8px" }}>
                        <span>🏢</span> Chọn bãi đỗ xe
                    </h2>

                    {loadingLots ? (
                        <p className="admin-empty">Đang tải danh sách bãi...</p>
                    ) : (
                        <div style={{ display: "flex", flexDirection: "column", gap: "8px", maxHeight: "65vh", overflowY: "auto" }}>
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
                                            border: `1px solid ${isSelected ? "var(--admin-lime, #a3e635)" : "rgba(255, 255, 255, 0.08)"}`,
                                            background: isSelected ? "rgba(163, 230, 53, 0.1)" : "rgba(255, 255, 255, 0.02)",
                                            color: "inherit",
                                            cursor: "pointer",
                                            transition: "all 0.15s ease",
                                        }}
                                    >
                                        <div style={{ fontWeight: 600, fontSize: "0.95rem" }}>
                                            {lot.name}
                                        </div>
                                        <div style={{ fontSize: "0.8rem", color: "var(--staff-text-muted, #94a3b8)", marginTop: "4px" }}>
                                            Chủ bãi: {lot.ownerName}
                                        </div>
                                        <div style={{ fontSize: "0.75rem", color: "var(--staff-text-muted, #94a3b8)" }}>
                                            {lot.address || lot.city}
                                        </div>
                                    </button>
                                );
                            })}
                        </div>
                    )}
                </section>

                {/* Cột phải: Quản lý nhân viên của bãi đã chọn */}
                <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
                    {/* Bảng phân công hiện tại */}
                    <section className="admin-card admin-table-card">
                        <div style={{ padding: "16px 20px", borderBottom: "1px solid rgba(255,255,255,0.08)" }}>
                            <h2 style={{ fontSize: "1.1rem", margin: 0 }}>
                                Nhân viên trực tại: {activeLot ? activeLot.name : "..."}
                            </h2>
                            <p style={{ margin: "4px 0 0", fontSize: "0.85rem", color: "var(--staff-text-muted, #94a3b8)" }}>
                                Quản lý danh sách nhân sự được phân quyền kiểm soát cổng vào / cổng ra (Staff Portal) tại bãi này.
                            </p>
                        </div>

                        <table className="admin-table">
                            <thead>
                                <tr>
                                    <th>Nhân viên / Họ tên</th>
                                    <th>Email & Số điện thoại</th>
                                    <th>Vai trò tại bãi</th>
                                    <th style={{ textAlign: "right" }}>Thao tác</th>
                                </tr>
                            </thead>
                            <tbody>
                                {assignments.map((member) => (
                                    <tr key={member.accountId}>
                                        <td>
                                            <strong>{member.accountFullName || "Nhân viên trực bãi"}</strong>
                                            <small style={{ fontFamily: "monospace", opacity: 0.7 }}>
                                                ID: {member.accountId.slice(0, 8)}...
                                            </small>
                                        </td>
                                        <td>
                                            <div>{member.accountEmail || "—"}</div>
                                            <small>{member.accountPhoneNumber || "—"}</small>
                                        </td>
                                        <td>
                                            <span
                                                style={{
                                                    display: "inline-block",
                                                    padding: "2px 8px",
                                                    borderRadius: "4px",
                                                    fontSize: "0.75rem",
                                                    fontWeight: 600,
                                                    background: member.role === "Owner" ? "rgba(168, 85, 247, 0.15)" : "rgba(59, 130, 246, 0.15)",
                                                    color: member.role === "Owner" ? "#c084fc" : "#60a5fa",
                                                    border: `1px solid ${member.role === "Owner" ? "rgba(168, 85, 247, 0.3)" : "rgba(59, 130, 246, 0.3)"}`,
                                                }}
                                            >
                                                {member.role === "Owner" ? "Chủ bãi (Owner)" : "Nhân viên trực (Staff)"}
                                            </span>
                                        </td>
                                        <td style={{ textAlign: "right" }}>
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-xs admin-btn-danger-ghost"
                                                onClick={() =>
                                                    handleRemoveMember(
                                                        member.accountId,
                                                        member.accountFullName || member.accountEmail || "nhân viên",
                                                    )
                                                }
                                                disabled={actionBusy}
                                                title="Gỡ nhân viên khỏi bãi đỗ xe này"
                                            >
                                                Gỡ khỏi bãi
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>

                        {assignments.length === 0 && !loadingAssignments && (
                            <p className="admin-empty">
                                Chưa có nhân viên nào được phân công vào bãi đỗ xe này. Hãy thêm nhân viên ở biểu mẫu bên dưới.
                            </p>
                        )}

                        {loadingAssignments && (
                            <p className="admin-empty">Đang tải danh sách phân công...</p>
                        )}
                    </section>

                    {/* Biểu mẫu gán nhân viên mới vào bãi xe (PUT /api/parking/admin/lots/{lotId}/assignments/{accountId}) */}
                    <section className="admin-card" style={{ padding: "20px" }}>
                        <h3 style={{ fontSize: "1rem", margin: "0 0 12px", display: "flex", alignItems: "center", gap: "8px" }}>
                            <span>➕</span> Phân công thêm nhân sự vào bãi
                        </h3>

                        <form onSubmit={handleAssignMember} style={{ display: "grid", gridTemplateColumns: "1fr 180px auto", gap: "12px", alignItems: "end" }}>
                            <label className="admin-field" style={{ margin: 0 }}>
                                <span>Chọn nhân viên:</span>
                                <select
                                    value={selectedAccountId}
                                    onChange={(e) => setSelectedAccountId(e.target.value)}
                                    required
                                    style={{
                                        width: "100%",
                                        padding: "8px 12px",
                                        borderRadius: "6px",
                                        background: "var(--admin-bg-subtle, #1a1e24)",
                                        color: "inherit",
                                        border: "1px solid rgba(255, 255, 255, 0.15)",
                                    }}
                                >
                                    <option value="">-- Chọn nhân viên từ hệ thống --</option>
                                    {assignableStaff.map((staff) => (
                                        <option key={staff.accountId} value={staff.accountId}>
                                            {staff.fullName} ({staff.email})
                                        </option>
                                    ))}
                                </select>
                            </label>

                            <label className="admin-field" style={{ margin: 0 }}>
                                <span>Vai trò tại bãi:</span>
                                <select
                                    value={selectedRole}
                                    onChange={(e) => setSelectedRole(e.target.value as "Staff" | "Owner")}
                                    style={{
                                        width: "100%",
                                        padding: "8px 12px",
                                        borderRadius: "6px",
                                        background: "var(--admin-bg-subtle, #1a1e24)",
                                        color: "inherit",
                                        border: "1px solid rgba(255, 255, 255, 0.15)",
                                    }}
                                >
                                    <option value="Staff">Nhân viên trực (Staff)</option>
                                    <option value="Owner">Quản lý bãi (Owner)</option>
                                </select>
                            </label>

                            <button
                                type="submit"
                                className="admin-btn admin-btn-lime"
                                disabled={actionBusy || !selectedAccountId || !selectedLotId}
                                style={{ height: "40px" }}
                            >
                                {actionBusy ? "Đang gán..." : "Gán vào bãi"}
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
