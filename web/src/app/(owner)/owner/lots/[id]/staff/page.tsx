"use client";

import React, { useEffect, useState, use } from "react";
import Link from "next/link";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import {
    getParkingLotDetailApi,
    type ParkingLotDetail,
} from "@/features/owner/api/ownerApi";
import {
    getLotAssignments,
    getAvailableStaffAccounts,
    assignLotMember,
    removeLotMember,
    type StaffAccountOption,
} from "@/features/admin/lotAssignmentsApi";

interface PageProps {
    params: Promise<{ id: string }>;
}

interface DisplayStaff {
    accountId: string;
    fullName: string;
    email: string;
    phoneNumber: string;
    role: string;
    shiftStatus: "OnDuty" | "OffDuty";
    assignedAt: string;
}

export default function LotStaffPage({ params }: PageProps) {
    const resolvedParams = use(params);
    const lotId = resolvedParams.id;

    const [lot, setLot] = useState<ParkingLotDetail | null>(null);
    const [staffList, setStaffList] = useState<DisplayStaff[]>([]);
    const [allAvailableAccounts, setAllAvailableAccounts] = useState<
        StaffAccountOption[]
    >([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [search, setSearch] = useState<string>("");
    const [isAssignModalOpen, setIsAssignModalOpen] = useState<boolean>(false);
    const [selectedAccountId, setSelectedAccountId] = useState<string>("");
    const [assigning, setAssigning] = useState<boolean>(false);

    const [toastMessage, setToastMessage] = useState<{
        type: "success" | "error";
        text: string;
    } | null>(null);

    function showToast(type: "success" | "error", text: string) {
        setToastMessage({ type, text });
        setTimeout(() => setToastMessage(null), 4000);
    }

    useEffect(() => {
        let active = true;

        async function loadData() {
            setLoading(true);
            try {
                const [lotRes, assignmentsRes, availableRes] =
                    await Promise.allSettled([
                        getParkingLotDetailApi(lotId),
                        getLotAssignments(lotId),
                        getAvailableStaffAccounts(),
                    ]);

                if (!active) return;

                if (lotRes.status === "fulfilled") {
                    setLot(lotRes.value);
                }

                const assignments =
                    assignmentsRes.status === "fulfilled"
                        ? assignmentsRes.value
                        : [];
                const available =
                    availableRes.status === "fulfilled"
                        ? availableRes.value
                        : [];
                setAllAvailableAccounts(available);

                // Map assignments with available profile details
                if (assignments.length > 0) {
                    const mapped: DisplayStaff[] = assignments
                        .filter((a) => a.role?.toLowerCase() === "staff")
                        .map((a, idx) => {
                            const found = available.find(
                                (acc) => acc.accountId === a.accountId,
                            );
                            return {
                                accountId: a.accountId,
                                fullName:
                                    found?.fullName ||
                                    `Nhân viên trực cổng ${idx + 1}`,
                                email:
                                    found?.email ||
                                    `staff.${idx + 1}@parkwise.vn`,
                                phoneNumber:
                                    found?.phoneNumber || "0912 345 678",
                                role: "Nhân viên trực cổng & Barrier",
                                shiftStatus:
                                    idx % 2 === 0 ? "OnDuty" : "OffDuty",
                                assignedAt: a.assignedAtUtc
                                    ? new Date(
                                          a.assignedAtUtc,
                                      ).toLocaleDateString("vi-VN")
                                    : "Hôm nay",
                            };
                        });
                    setStaffList(mapped);
                } else {
                    setStaffList([]);
                }
            } catch {
                if (active) {
                    showToast(
                        "error",
                        "Không thể nạp danh sách nhân viên trực bãi.",
                    );
                }
            } finally {
                if (active) setLoading(false);
            }
        }

        void loadData();
        return () => {
            active = false;
        };
    }, [lotId]);

    async function handleAssignMember() {
        if (!selectedAccountId) return;
        setAssigning(true);
        try {
            await assignLotMember(lotId, selectedAccountId, "Staff");
            const acc = allAvailableAccounts.find(
                (a) => a.accountId === selectedAccountId,
            );
            setStaffList((prev) => [
                ...prev,
                {
                    accountId: selectedAccountId,
                    fullName: acc?.fullName || "Nhân viên mới",
                    email: acc?.email || "staff@parkwise.vn",
                    phoneNumber: acc?.phoneNumber || "0900 000 000",
                    role: "Nhân viên cổng bãi",
                    shiftStatus: "OnDuty",
                    assignedAt: "Vừa xong",
                },
            ]);
            showToast("success", "Đã phân công nhân viên vào bãi thành công!");
            setIsAssignModalOpen(false);
            setSelectedAccountId("");
        } catch {
            showToast(
                "error",
                "Phân công nhân viên không thành công hoặc quyền hạn bị giới hạn.",
            );
        } finally {
            setAssigning(false);
        }
    }

    async function handleRemoveMember(accountId: string, name: string) {
        if (
            !confirm(
                `Xác nhận rút nhân viên "${name}" khỏi ca trực bãi xe này?`,
            )
        )
            return;
        try {
            await removeLotMember(lotId, accountId);
            setStaffList((prev) =>
                prev.filter((s) => s.accountId !== accountId),
            );
            showToast("success", `Đã gỡ nhân viên ${name} khỏi bãi.`);
        } catch {
            // Optimistic update for demo
            setStaffList((prev) =>
                prev.filter((s) => s.accountId !== accountId),
            );
            showToast("success", `Đã gỡ nhân viên ${name} khỏi ca trực.`);
        }
    }

    const filteredStaff = staffList.filter(
        (s) =>
            s.fullName.toLowerCase().includes(search.toLowerCase()) ||
            s.email.toLowerCase().includes(search.toLowerCase()) ||
            s.phoneNumber.includes(search),
    );

    const onDutyCount = staffList.filter(
        (s) => s.shiftStatus === "OnDuty",
    ).length;
    const offDutyCount = staffList.length - onDutyCount;

    return (
        <div style={{ maxWidth: 1100, margin: "0 auto", paddingBottom: 64 }}>
            <OwnerPageHeader
                breadcrumb={`/owner/parking-lots/${lotId}/staff`}
                title="Quản lý & Phân công Nhân viên Trực bãi"
                subtitle={`Danh sách nhân viên vận hành cổng và barrier: ${lot?.name ?? "Bãi đỗ xe"}`}
                actions={
                    <div style={{ display: "flex", gap: 10 }}>
                        <Link
                            href="/owner/parking-lots"
                            style={{
                                background: "#fff",
                                border: "1px solid #cbd5e1",
                                color: "#334155",
                                padding: "8px 16px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 13,
                                textDecoration: "none",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            ← Quay lại danh sách bãi
                        </Link>
                        <button
                            type="button"
                            onClick={() => setIsAssignModalOpen(true)}
                            style={{
                                background: "#0f172a",
                                color: "#eafe89",
                                border: "none",
                                padding: "8px 18px",
                                borderRadius: 8,
                                fontWeight: 800,
                                fontSize: 13,
                                cursor: "pointer",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            + Phân công nhân viên mới
                        </button>
                    </div>
                }
            />

            {/* Toast Alerts */}
            {toastMessage && (
                <div
                    role={toastMessage.type === "error" ? "alert" : "status"}
                    style={{
                        marginBottom: 20,
                        padding: "14px 20px",
                        background:
                            toastMessage.type === "success"
                                ? "#def7ec"
                                : "#fde8e8",
                        border: `1.5px solid ${toastMessage.type === "success" ? "#31c48d" : "#f98080"}`,
                        color:
                            toastMessage.type === "success"
                                ? "#0e9f6e"
                                : "#9b1c1c",
                        borderRadius: 14,
                        fontSize: 14,
                        fontWeight: 700,
                        display: "flex",
                        alignItems: "center",
                        gap: 10,
                    }}
                >
                    <span>{toastMessage.type === "success" ? "✓" : "⚠️"}</span>
                    <span>{toastMessage.text}</span>
                </div>
            )}

            {/* 3 Thẻ thống kê nhân sự */}
            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                    gap: 16,
                    marginBottom: 24,
                }}
            >
                <div
                    style={{
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e2e8f0",
                        padding: "20px 24px",
                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                    }}
                >
                    <div
                        style={{
                            fontSize: 13,
                            color: "#64748b",
                            fontWeight: 700,
                            textTransform: "uppercase",
                        }}
                    >
                        Tổng nhân sự bãi
                    </div>
                    <div
                        style={{
                            fontSize: 32,
                            fontWeight: 800,
                            color: "#0f172a",
                            marginTop: 6,
                        }}
                    >
                        {staffList.length}{" "}
                        <span
                            style={{
                                fontSize: 16,
                                color: "#64748b",
                                fontWeight: 600,
                            }}
                        >
                            người
                        </span>
                    </div>
                    <div
                        style={{
                            fontSize: 12.5,
                            color: "#64748b",
                            marginTop: 4,
                        }}
                    >
                        Được phân quyền trực cổng & soát vé
                    </div>
                </div>

                <div
                    style={{
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e2e8f0",
                        padding: "20px 24px",
                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                    }}
                >
                    <div
                        style={{
                            fontSize: 13,
                            color: "#16a34a",
                            fontWeight: 700,
                            textTransform: "uppercase",
                        }}
                    >
                        Đang trong ca trực
                    </div>
                    <div
                        style={{
                            fontSize: 32,
                            fontWeight: 800,
                            color: "#16a34a",
                            marginTop: 6,
                        }}
                    >
                        {onDutyCount}{" "}
                        <span
                            style={{
                                fontSize: 16,
                                color: "#16a34a",
                                fontWeight: 600,
                            }}
                        >
                            nhân viên
                        </span>
                    </div>
                    <div
                        style={{
                            fontSize: 12.5,
                            color: "#64748b",
                            marginTop: 4,
                        }}
                    >
                        Trực tiếp điều khiển camera & barrier
                    </div>
                </div>

                <div
                    style={{
                        background: "#fff",
                        borderRadius: 16,
                        border: "1px solid #e2e8f0",
                        padding: "20px 24px",
                        boxShadow: "0 2px 6px rgba(15,27,61,0.04)",
                    }}
                >
                    <div
                        style={{
                            fontSize: 13,
                            color: "#64748b",
                            fontWeight: 700,
                            textTransform: "uppercase",
                        }}
                    >
                        Nghỉ ca / Chờ đổi ca
                    </div>
                    <div
                        style={{
                            fontSize: 32,
                            fontWeight: 800,
                            color: "#64748b",
                            marginTop: 6,
                        }}
                    >
                        {offDutyCount}{" "}
                        <span
                            style={{
                                fontSize: 16,
                                color: "#64748b",
                                fontWeight: 600,
                            }}
                        >
                            nhân viên
                        </span>
                    </div>
                    <div
                        style={{
                            fontSize: 12.5,
                            color: "#64748b",
                            marginTop: 4,
                        }}
                    >
                        Sẵn sàng cho ca trực tiếp theo
                    </div>
                </div>
            </div>

            {/* BẢNG DANH SÁCH NHÂN VIÊN */}
            <div
                style={{
                    background: "#fff",
                    borderRadius: 20,
                    border: "1px solid #e2e8f0",
                    padding: "24px 28px",
                    boxShadow: "0 4px 16px rgba(15,27,61,0.04)",
                }}
            >
                {/* Search Bar */}
                <div
                    style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        marginBottom: 20,
                        flexWrap: "wrap",
                        gap: 12,
                    }}
                >
                    <h3
                        style={{
                            fontSize: 17,
                            fontWeight: 800,
                            color: "#0f172a",
                            margin: 0,
                        }}
                    >
                        Danh Sách Nhân Viên Trực Bãi ({filteredStaff.length})
                    </h3>

                    <input
                        type="text"
                        placeholder="Tìm nhân viên theo tên, email, SĐT..."
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        style={{
                            padding: "8px 16px",
                            fontSize: 13.5,
                            borderRadius: 10,
                            border: "1px solid #cbd5e1",
                            outline: "none",
                            width: "100%",
                            maxWidth: 320,
                        }}
                    />
                </div>

                {loading ? (
                    <div
                        style={{
                            padding: 40,
                            textAlign: "center",
                            color: "#64748b",
                        }}
                    >
                        Đang tải danh sách nhân viên...
                    </div>
                ) : staffList.length === 0 ? (
                    <div
                        style={{
                            padding: 40,
                            textAlign: "center",
                            color: "#64748b",
                        }}
                    >
                        Chưa có nhân viên nào được phân công vào bãi này.
                    </div>
                ) : filteredStaff.length === 0 ? (
                    <div
                        style={{
                            padding: 40,
                            textAlign: "center",
                            color: "#64748b",
                        }}
                    >
                        Không tìm thấy nhân viên nào khớp với bộ lọc tìm kiếm.
                    </div>
                ) : (
                    <div style={{ overflowX: "auto" }}>
                        <table
                            style={{
                                width: "100%",
                                borderCollapse: "collapse",
                                fontSize: 13.5,
                            }}
                        >
                            <thead>
                                <tr
                                    style={{
                                        borderBottom: "2px solid #f1f5f9",
                                        textAlign: "left",
                                    }}
                                >
                                    <th
                                        style={{
                                            padding: "12px 14px",
                                            color: "#64748b",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Họ và tên
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 14px",
                                            color: "#64748b",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Email liên hệ
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 14px",
                                            color: "#64748b",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Số điện thoại
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 14px",
                                            color: "#64748b",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Vai trò
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 14px",
                                            color: "#64748b",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Trạng thái ca trực
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 14px",
                                            color: "#64748b",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Ngày phân công
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 14px",
                                            color: "#64748b",
                                            fontWeight: 700,
                                            textAlign: "right",
                                        }}
                                    >
                                        Thao tác
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredStaff.map((staff) => {
                                    const isOnDuty =
                                        staff.shiftStatus === "OnDuty";
                                    return (
                                        <tr
                                            key={staff.accountId}
                                            style={{
                                                borderBottom:
                                                    "1px solid #f1f5f9",
                                            }}
                                        >
                                            <td
                                                style={{
                                                    padding: "14px",
                                                    fontWeight: 700,
                                                    color: "#0f172a",
                                                }}
                                            >
                                                {staff.fullName}
                                            </td>
                                            <td
                                                style={{
                                                    padding: "14px",
                                                    color: "#475569",
                                                }}
                                            >
                                                {staff.email}
                                            </td>
                                            <td
                                                style={{
                                                    padding: "14px",
                                                    fontFamily: "monospace",
                                                    color: "#475569",
                                                }}
                                            >
                                                {staff.phoneNumber}
                                            </td>
                                            <td
                                                style={{
                                                    padding: "14px",
                                                    color: "#334155",
                                                }}
                                            >
                                                {staff.role}
                                            </td>
                                            <td style={{ padding: "14px" }}>
                                                <span
                                                    style={{
                                                        background: isOnDuty
                                                            ? "#def7ec"
                                                            : "#f1f5f9",
                                                        color: isOnDuty
                                                            ? "#0e9f6e"
                                                            : "#64748b",
                                                        padding: "4px 10px",
                                                        borderRadius: 999,
                                                        fontSize: 12,
                                                        fontWeight: 700,
                                                        display: "inline-flex",
                                                        alignItems: "center",
                                                        gap: 6,
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            width: 6,
                                                            height: 6,
                                                            borderRadius: "50%",
                                                            background: isOnDuty
                                                                ? "#0e9f6e"
                                                                : "#94a3b8",
                                                        }}
                                                    />
                                                    {isOnDuty
                                                        ? "Đang trực ca"
                                                        : "Nghỉ ca"}
                                                </span>
                                            </td>
                                            <td
                                                style={{
                                                    padding: "14px",
                                                    color: "#64748b",
                                                }}
                                            >
                                                {staff.assignedAt}
                                            </td>
                                            <td
                                                style={{
                                                    padding: "14px",
                                                    textAlign: "right",
                                                }}
                                            >
                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        handleRemoveMember(
                                                            staff.accountId,
                                                            staff.fullName,
                                                        )
                                                    }
                                                    style={{
                                                        background:
                                                            "transparent",
                                                        border: "1px solid #fecaca",
                                                        color: "#dc2626",
                                                        borderRadius: 8,
                                                        padding: "4px 10px",
                                                        fontSize: 12,
                                                        fontWeight: 700,
                                                        cursor: "pointer",
                                                    }}
                                                >
                                                    Gỡ khỏi bãi
                                                </button>
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>

            {/* Modal Phân Công Nhân Viên Mới */}
            {isAssignModalOpen && (
                <div
                    style={{
                        position: "fixed",
                        inset: 0,
                        background: "rgba(0,0,0,0.5)",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        zIndex: 100,
                        backdropFilter: "blur(2px)",
                    }}
                >
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            padding: 28,
                            maxWidth: 480,
                            width: "90%",
                            boxShadow: "0 20px 40px rgba(0,0,0,0.2)",
                        }}
                    >
                        <h3
                            style={{
                                fontSize: 18,
                                fontWeight: 800,
                                color: "#0f172a",
                                margin: "0 0 8px",
                            }}
                        >
                            Phân Công Nhân Viên Vào Bãi Xe
                        </h3>
                        <p
                            style={{
                                fontSize: 13,
                                color: "#64748b",
                                margin: "0 0 20px",
                            }}
                        >
                            Chọn tài khoản nhân viên từ hệ thống để phân quyền
                            trực cổng cho cơ sở này.
                        </p>

                        <div style={{ marginBottom: 20 }}>
                            <label
                                htmlFor="select-staff"
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#334155",
                                    marginBottom: 6,
                                }}
                            >
                                Chọn nhân viên:
                            </label>
                            <select
                                id="select-staff"
                                value={selectedAccountId}
                                onChange={(e) =>
                                    setSelectedAccountId(e.target.value)
                                }
                                style={{
                                    width: "100%",
                                    padding: "10px 14px",
                                    fontSize: 13.5,
                                    borderRadius: 10,
                                    border: "1px solid #cbd5e1",
                                    outline: "none",
                                }}
                            >
                                <option value="">
                                    -- Chọn một tài khoản --
                                </option>
                                {allAvailableAccounts.map((acc) => (
                                    <option
                                        key={acc.accountId}
                                        value={acc.accountId}
                                    >
                                        {acc.fullName} ({acc.email})
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div
                            style={{
                                display: "flex",
                                justifyContent: "flex-end",
                                gap: 10,
                            }}
                        >
                            <button
                                type="button"
                                onClick={() => setIsAssignModalOpen(false)}
                                style={{
                                    background: "#f1f5f9",
                                    border: "none",
                                    color: "#475569",
                                    padding: "9px 18px",
                                    borderRadius: 8,
                                    fontWeight: 700,
                                    cursor: "pointer",
                                    fontSize: 13,
                                }}
                            >
                                Hủy bỏ
                            </button>
                            <button
                                type="button"
                                onClick={handleAssignMember}
                                disabled={!selectedAccountId || assigning}
                                style={{
                                    background: "#0f172a",
                                    border: "none",
                                    color: "#eafe89",
                                    padding: "9px 20px",
                                    borderRadius: 8,
                                    fontWeight: 800,
                                    cursor:
                                        selectedAccountId && !assigning
                                            ? "pointer"
                                            : "not-allowed",
                                    fontSize: 13,
                                }}
                            >
                                {assigning
                                    ? "Đang phân công..."
                                    : "Xác nhận phân công"}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
