"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { AdminIcon, StatusPill } from "./AdminUi";
import { getAdminParkingLots } from "./reviewParkingLot";
import {
    SHIFT_DETAILS,
    assignLotMember,
    describeAssignmentError,
    getAvailableStaffAccounts,
    getLotAssignments,
    removeLotMember,
    type StaffAccountOption,
    type ShiftType,
} from "./lotAssignmentsApi";
import type {
    AdminParkingLot,
    ApprovalStatus,
    ParkingLotAssignmentMember,
} from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

type LotStatusFilter = "All" | ApprovalStatus;
type MemberRoleFilter = "All" | "Staff" | "Owner";

const SHIFT_KEYS: ShiftType[] = ["morning", "afternoon", "night", "allday"];

function getInitials(name?: string | null, email?: string | null): string {
    const source = (name || email || "NV").trim();
    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
        return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
    }
    return source.slice(0, 2).toUpperCase();
}

function formatShortDate(iso?: string | null, isVi = true): string {
    if (!iso) return isVi ? "Chưa cập nhật" : "Not updated";
    const d = new Date(iso);
    if (Number.isNaN(d.getTime()))
        return isVi ? "Chưa cập nhật" : "Not updated";
    return d.toLocaleDateString(isVi ? "vi-VN" : "en-GB", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
    });
}

export function AdminLotAssignmentsPage() {
    const { t, language } = useLanguage();
    const isVi = language === "vi";

    const [lots, setLots] = useState<AdminParkingLot[]>([]);
    const [selectedLotId, setSelectedLotId] = useState<string>("");
    const [assignments, setAssignments] = useState<
        ParkingLotAssignmentMember[]
    >([]);
    const [availableStaff, setAvailableStaff] = useState<StaffAccountOption[]>(
        [],
    );
    const [allAssignedMap, setAllAssignedMap] = useState<
        Map<string, { lotId: string; lotName: string }>
    >(new Map());
    const [lotMemberCounts, setLotMemberCounts] = useState<Map<string, number>>(
        new Map(),
    );

    const [loadingLots, setLoadingLots] = useState(true);
    const [loadingAssignments, setLoadingAssignments] = useState(false);
    const [actionBusy, setActionBusy] = useState(false);

    // Search & filter state for Parking Lots selector
    const [lotSearch, setLotSearch] = useState("");
    const [lotStatusFilter, setLotStatusFilter] =
        useState<LotStatusFilter>("All");

    // Search & filter state for Assigned Staff table
    const [memberSearch, setMemberSearch] = useState("");
    const [memberRoleFilter, setMemberRoleFilter] =
        useState<MemberRoleFilter>("All");

    // Modal state: Assign / Edit Assignment
    const [isAssignModalOpen, setIsAssignModalOpen] = useState(false);
    const [editingMember, setEditingMember] =
        useState<ParkingLotAssignmentMember | null>(null);
    const [modalLotId, setModalLotId] = useState<string>("");
    const [selectedAccountId, setSelectedAccountId] = useState("");
    const [selectedShift, setSelectedShift] = useState<ShiftType>("morning");
    const [selectedRole, setSelectedRole] = useState<"Staff" | "Owner">(
        "Staff",
    );
    const [modalError, setModalError] = useState<string>("");

    // Modal state: Confirm Remove Member
    const [memberToRemove, setMemberToRemove] = useState<{
        accountId: string;
        fullName: string;
        email: string;
        role: "Staff" | "Owner";
        shiftLabel: string;
    } | null>(null);

    const [rawError, setRawError] = useState<unknown>(null);
    const [toast, setToast] = useState<string | null>(null);

    const errorMsg = useMemo(() => {
        if (!rawError) return "";
        if (typeof rawError === "string") return rawError;
        return describeAssignmentError(rawError, language);
    }, [rawError, language]);

    const refreshAllAssigned = useCallback(
        async (lotList: AdminParkingLot[]) => {
            if (!lotList || lotList.length === 0) {
                setAllAssignedMap(new Map());
                setLotMemberCounts(new Map());
                return;
            }
            const mapping = new Map<
                string,
                { lotId: string; lotName: string }
            >();
            const counts = new Map<string, number>();
            const lotNames = new Map<string, string>(
                lotList.map((l) => [l.id.toLowerCase(), l.name]),
            );

            try {
                const shiftRes = await fetch("/api/staff-shifts");
                if (shiftRes.ok) {
                    const shiftList = await shiftRes.json();
                    if (Array.isArray(shiftList)) {
                        shiftList.forEach(
                            (s: { accountId?: string; lotId?: string }) => {
                                if (s.accountId && s.lotId) {
                                    mapping.set(s.accountId.toLowerCase(), {
                                        lotId: s.lotId,
                                        lotName:
                                            lotNames.get(
                                                s.lotId.toLowerCase(),
                                            ) ||
                                            (isVi
                                                ? "Bãi đỗ khác"
                                                : "Other lot"),
                                    });
                                }
                            },
                        );
                    }
                }
            } catch {
                // ignore
            }

            await Promise.allSettled(
                lotList.map(async (lot) => {
                    try {
                        const members = await getLotAssignments(lot.id);
                        counts.set(lot.id, members.length);
                        members.forEach((m) => {
                            mapping.set(m.accountId.toLowerCase(), {
                                lotId: lot.id,
                                lotName: lot.name,
                            });
                        });
                    } catch {
                        // ignore
                    }
                }),
            );
            setAllAssignedMap(mapping);
            setLotMemberCounts(counts);
        },
        [isVi],
    );

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
            refreshAllAssigned(lotList).catch(() => {});
            if (lotList.length > 0 && !selectedLotId) {
                setSelectedLotId(lotList[0].id);
            }
        } catch (err) {
            setRawError(err);
        } finally {
            setLoadingLots(false);
        }
    }, [refreshAllAssigned, selectedLotId]);

    const loadAssignments = useCallback(async (lotId: string) => {
        if (!lotId) return;
        setLoadingAssignments(true);
        try {
            const data = await getLotAssignments(lotId);
            setAssignments(data);
            setLotMemberCounts((prev) => {
                const next = new Map(prev);
                next.set(lotId, data.length);
                return next;
            });
        } catch (err) {
            setRawError(err);
        } finally {
            setLoadingAssignments(false);
        }
    }, []);

    function handleSelectLot(lotId: string) {
        if (lotId === selectedLotId) return;
        setLoadingAssignments(true);
        setSelectedLotId(lotId);
    }

    useEffect(() => {
        let active = true;
        Promise.all([getAdminParkingLots("All"), getAvailableStaffAccounts()])
            .then(([lotList, staffList]) => {
                if (!active) return;
                setLots(lotList);
                setAvailableStaff(staffList);
                refreshAllAssigned(lotList).catch(() => {});
                if (lotList.length > 0) {
                    setSelectedLotId((prev) => prev || lotList[0].id);
                }
            })
            .catch((err) => {
                if (active) setRawError(err);
            })
            .finally(() => {
                if (active) setLoadingLots(false);
            });
        return () => {
            active = false;
        };
    }, [refreshAllAssigned]);

    useEffect(() => {
        if (!selectedLotId) return;
        let active = true;
        getLotAssignments(selectedLotId)
            .then((data) => {
                if (!active) return;
                setAssignments(data);
                setLotMemberCounts((prev) => {
                    const next = new Map(prev);
                    next.set(selectedLotId, data.length);
                    return next;
                });
            })
            .catch((err) => {
                if (active) setRawError(err);
            })
            .finally(() => {
                if (active) setLoadingAssignments(false);
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

    // Map accountId -> StaffAccountOption for real name/email/phone enrichment
    const staffLookupMap = useMemo(() => {
        const map = new Map<string, StaffAccountOption>();
        availableStaff.forEach((s) => {
            map.set(s.accountId.toLowerCase(), s);
        });
        return map;
    }, [availableStaff]);

    const activeLot = useMemo(
        () => lots.find((l) => l.id === selectedLotId) || null,
        [lots, selectedLotId],
    );

    // Enrich assignments with real identity info from availableStaff
    const enrichedAssignments = useMemo(() => {
        return assignments.map((member) => {
            const matched = staffLookupMap.get(member.accountId.toLowerCase());
            const shiftKey: ShiftType = member.shift || "morning";
            const shiftInfo = SHIFT_DETAILS[shiftKey] || SHIFT_DETAILS.morning;
            return {
                ...member,
                accountFullName:
                    member.accountFullName ||
                    matched?.fullName ||
                    t("assignmentsDefaultStaffName"),
                accountEmail: member.accountEmail || matched?.email || "",
                accountPhoneNumber:
                    member.accountPhoneNumber || matched?.phoneNumber || "",
                shift: shiftKey,
                shiftName: member.shiftName || shiftInfo.shiftName,
                hours: member.hours || shiftInfo.hours,
            };
        });
    }, [assignments, staffLookupMap, t]);

    // Filtered assignments for table display
    const filteredAssignments = useMemo(() => {
        const q = memberSearch.trim().toLowerCase();
        return enrichedAssignments.filter((m) => {
            if (memberRoleFilter !== "All" && m.role !== memberRoleFilter) {
                return false;
            }
            if (!q) return true;
            return (
                (m.accountFullName || "").toLowerCase().includes(q) ||
                (m.accountEmail || "").toLowerCase().includes(q) ||
                (m.accountPhoneNumber || "").toLowerCase().includes(q) ||
                m.accountId.toLowerCase().includes(q)
            );
        });
    }, [enrichedAssignments, memberRoleFilter, memberSearch]);

    // Filtered parking lots for left panel
    const filteredLots = useMemo(() => {
        const q = lotSearch.trim().toLowerCase();
        return lots.filter((lot) => {
            if (lotStatusFilter !== "All" && lot.status !== lotStatusFilter) {
                return false;
            }
            if (!q) return true;
            const ownerProfile = lot.ownerUserId
                ? staffLookupMap.get(lot.ownerUserId.toLowerCase())
                : undefined;
            const ownerLabel =
                lot.ownerName ||
                ownerProfile?.fullName ||
                ownerProfile?.email ||
                lot.ownerUserId ||
                "";
            return (
                lot.name.toLowerCase().includes(q) ||
                (lot.address || "").toLowerCase().includes(q) ||
                (lot.city || "").toLowerCase().includes(q) ||
                ownerLabel.toLowerCase().includes(q)
            );
        });
    }, [lots, lotSearch, lotStatusFilter, staffLookupMap]);

    // 1 nhân viên chỉ quản lý 1 bãi duy nhất (Admin chỉ phân 1 người 1 bãi)
    const assignableStaff = useMemo(() => {
        return availableStaff.filter((s) => {
            const assigned = allAssignedMap.get(s.accountId.toLowerCase());
            return !assigned;
        });
    }, [availableStaff, allAssignedMap]);

    const busyStaff = useMemo(() => {
        return availableStaff
            .map((s) => ({
                staff: s,
                assignedTo: allAssignedMap.get(s.accountId.toLowerCase()),
            }))
            .filter(
                (
                    item,
                ): item is {
                    staff: StaffAccountOption;
                    assignedTo: { lotId: string; lotName: string };
                } => Boolean(item.assignedTo),
            );
    }, [availableStaff, allAssignedMap]);

    // 4 Real KPI Metrics
    const kpiMetrics = useMemo(() => {
        const totalLots = lots.length;
        const activeLotsCount = lots.filter(
            (l) => l.status === "Approved",
        ).length;
        const availableToAssignCount = assignableStaff.length;
        const currentlyAssignedCount = allAssignedMap.size;
        const unassignedLotsCount = lots.filter((lot) => {
            const count =
                lot.id === selectedLotId
                    ? assignments.length
                    : lotMemberCounts.get(lot.id);
            return count === 0;
        }).length;

        return {
            totalLots,
            activeLotsCount,
            availableToAssignCount,
            totalStaffPool: availableStaff.length,
            currentlyAssignedCount,
            unassignedLotsCount,
        };
    }, [
        lots,
        assignableStaff.length,
        availableStaff.length,
        allAssignedMap.size,
        selectedLotId,
        assignments.length,
        lotMemberCounts,
    ]);

    // Shift breakdown at the currently selected lot
    const shiftBreakdown = useMemo(() => {
        return SHIFT_KEYS.map((key) => {
            const detail = SHIFT_DETAILS[key];
            const membersInShift = enrichedAssignments.filter(
                (m) => (m.shift || "morning") === key,
            );
            return {
                key,
                shiftName: detail.shiftName,
                hours: detail.hours,
                members: membersInShift,
            };
        });
    }, [enrichedAssignments]);

    const selectedStaffPreview = useMemo(() => {
        if (!selectedAccountId) return null;
        return (
            staffLookupMap.get(selectedAccountId.toLowerCase()) ||
            availableStaff.find((s) => s.accountId === selectedAccountId) ||
            null
        );
    }, [selectedAccountId, staffLookupMap, availableStaff]);

    function formatRoleLabel(role: "Staff" | "Owner"): string {
        if (role === "Owner") {
            return isVi ? "Chủ bãi" : "Parking Lot Owner";
        }
        return isVi ? "Nhân viên trực bãi" : "Parking Staff";
    }

    function resolveLotOwnerName(lot: AdminParkingLot): string {
        if (lot.ownerName && lot.ownerName !== "—") return lot.ownerName;
        if (lot.ownerUserId) {
            const found = staffLookupMap.get(lot.ownerUserId.toLowerCase());
            if (found?.fullName) return found.fullName;
            if (found?.email) return found.email;
            return `ID: ${lot.ownerUserId.slice(0, 8)}...`;
        }
        return isVi ? "Chưa cập nhật" : "Not updated";
    }

    function openCreateAssignModal(defaultAccountId = "") {
        setEditingMember(null);
        setModalLotId(selectedLotId || lots[0]?.id || "");
        setSelectedAccountId(defaultAccountId);
        setSelectedRole("Staff");
        setSelectedShift("morning");
        setModalError("");
        setIsAssignModalOpen(true);
    }

    function openEditAssignModal(member: ParkingLotAssignmentMember) {
        setEditingMember(member);
        setModalLotId(selectedLotId);
        setSelectedAccountId(member.accountId);
        setSelectedRole(member.role);
        setSelectedShift(member.shift || "morning");
        setModalError("");
        setIsAssignModalOpen(true);
    }

    function closeAssignModal() {
        if (actionBusy) return;
        setIsAssignModalOpen(false);
        setEditingMember(null);
        setModalError("");
    }

    // Xử lý gán hoặc cập nhật phân công nhân viên (PUT)
    async function handleAssignMember(e: React.FormEvent) {
        e.preventDefault();
        const targetLotId = modalLotId || selectedLotId;
        if (!targetLotId || !selectedAccountId) return;

        const alreadyAssigned = allAssignedMap.get(
            selectedAccountId.toLowerCase(),
        );
        if (
            alreadyAssigned &&
            alreadyAssigned.lotId.toLowerCase() !== targetLotId.toLowerCase()
        ) {
            const msg = isVi
                ? `Nhân viên này đã được phân công trực tại "${alreadyAssigned.lotName}". Hệ thống quy định mỗi nhân viên chỉ trực 1 bãi duy nhất. Vui lòng gỡ nhân viên khỏi bãi cũ trước khi phân công bãi mới.`
                : `This staff member is already assigned to "${alreadyAssigned.lotName}". Each staff member can only be assigned to one parking lot at a time.`;
            setModalError(msg);
            setRawError(msg);
            return;
        }

        setActionBusy(true);
        setModalError("");
        setRawError(null);
        try {
            await assignLotMember(
                targetLotId,
                selectedAccountId,
                selectedRole,
                selectedShift,
            );
            const staff = availableStaff.find(
                (s) => s.accountId === selectedAccountId,
            );
            const targetLot =
                lots.find((l) => l.id === targetLotId) || activeLot;
            setToast(
                `${t("approved")}: ${staff?.fullName || editingMember?.accountFullName || "Staff"} ➔ ${targetLot?.name || ""}`,
            );
            setSelectedAccountId("");
            setIsAssignModalOpen(false);
            setEditingMember(null);
            if (targetLotId !== selectedLotId) {
                setSelectedLotId(targetLotId);
            } else {
                await loadAssignments(targetLotId);
            }
            refreshAllAssigned(lots).catch(() => {});
        } catch (err) {
            setRawError(err);
            setModalError(describeAssignmentError(err, language));
        } finally {
            setActionBusy(false);
        }
    }

    // Xử lý gỡ nhân viên (DELETE)
    async function executeRemoveMember(accountId: string, memberName: string) {
        if (!selectedLotId) return;
        setActionBusy(true);
        setRawError(null);
        try {
            await removeLotMember(selectedLotId, accountId);
            setToast(
                `${t("removeStaffBtn")}: ${memberName} (${activeLot?.name || ""})`,
            );
            setMemberToRemove(null);
            await loadAssignments(selectedLotId);
            refreshAllAssigned(lots).catch(() => {});
        } catch (err) {
            setRawError(err);
        } finally {
            setActionBusy(false);
        }
    }

    return (
        <div className="admin-assign-page">
            {/* A. Page Header */}
            <header className="admin-heading admin-assign-header">
                <div className="admin-assign-header-copy">
                    <p className="admin-eyebrow">{t("assignmentsEyebrow")}</p>
                    <h1>{t("assignmentsTitle")}</h1>
                    <p className="admin-assign-subtitle">
                        {isVi
                            ? "Quản lý nhân sự, vai trò và ca trực tại các bãi đỗ xe."
                            : "Manage staff members, operational roles, and shift schedules across parking lots."}
                    </p>
                </div>
                <div className="admin-assign-header-actions">
                    <button
                        type="button"
                        className="admin-btn admin-btn-lime admin-assign-primary-cta"
                        onClick={() => openCreateAssignModal()}
                        disabled={loadingLots || lots.length === 0}
                    >
                        <AdminIcon name="plus" size={15} />
                        <span>
                            {isVi
                                ? "+ Phân công nhân viên"
                                : "+ Assign Staff Member"}
                        </span>
                    </button>
                    <Link
                        href="/admin/parking-lots"
                        className="admin-btn admin-btn-ghost"
                    >
                        <AdminIcon name="lots" size={15} />
                        <span>{t("assignmentsBackToLots")}</span>
                    </Link>
                    <Link
                        href="/admin/owner-applications"
                        className="admin-btn admin-btn-ghost"
                    >
                        <AdminIcon name="users" size={15} />
                        <span>{t("assignmentsOwnerAppsLink")}</span>
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
                        <AdminIcon name="refresh" size={15} />
                        <span>{t("refresh")}</span>
                    </button>
                </div>
            </header>

            {/* B. 4 Statistic Cards (100% Real Data) */}
            <section
                className="admin-assign-kpi-grid"
                aria-label={
                    isVi
                        ? "Thống kê phân công nhân sự"
                        : "Assignment statistics"
                }
            >
                <article className="admin-card admin-assign-kpi-card">
                    <div className="admin-assign-kpi-icon is-neutral">
                        <AdminIcon name="lots" size={18} />
                    </div>
                    <div className="admin-assign-kpi-body">
                        <span className="admin-assign-kpi-label">
                            {isVi ? "Tổng số bãi đỗ xe" : "Total Parking Lots"}
                        </span>
                        <strong className="admin-assign-kpi-value">
                            {kpiMetrics.totalLots}
                        </strong>
                        <small className="admin-assign-kpi-desc">
                            {isVi
                                ? `${kpiMetrics.activeLotsCount} bãi đang hoạt động`
                                : `${kpiMetrics.activeLotsCount} active lots`}
                        </small>
                    </div>
                </article>

                <article className="admin-card admin-assign-kpi-card">
                    <div className="admin-assign-kpi-icon is-green">
                        <AdminIcon name="users" size={18} />
                    </div>
                    <div className="admin-assign-kpi-body">
                        <span className="admin-assign-kpi-label">
                            {isVi
                                ? "Nhân viên có thể phân công"
                                : "Available to Assign"}
                        </span>
                        <strong className="admin-assign-kpi-value is-green">
                            {kpiMetrics.availableToAssignCount}
                        </strong>
                        <small className="admin-assign-kpi-desc">
                            {isVi
                                ? `Trên tổng số ${kpiMetrics.totalStaffPool} tài khoản nhân sự`
                                : `Out of ${kpiMetrics.totalStaffPool} total accounts`}
                        </small>
                    </div>
                </article>

                <article className="admin-card admin-assign-kpi-card">
                    <div className="admin-assign-kpi-icon is-blue">
                        <AdminIcon name="check" size={18} />
                    </div>
                    <div className="admin-assign-kpi-body">
                        <span className="admin-assign-kpi-label">
                            {isVi
                                ? "Nhân viên đang phân công"
                                : "Currently Assigned"}
                        </span>
                        <strong className="admin-assign-kpi-value is-blue">
                            {kpiMetrics.currentlyAssignedCount}
                        </strong>
                        <small className="admin-assign-kpi-desc">
                            {isVi
                                ? `${assignments.length} nhân sự tại bãi đang chọn`
                                : `${assignments.length} assigned to selected lot`}
                        </small>
                    </div>
                </article>

                <article className="admin-card admin-assign-kpi-card">
                    <div className="admin-assign-kpi-icon is-amber">
                        <AdminIcon name="clock" size={18} />
                    </div>
                    <div className="admin-assign-kpi-body">
                        <span className="admin-assign-kpi-label">
                            {isVi
                                ? "Bãi chưa có nhân sự"
                                : "Lots Without Staff"}
                        </span>
                        <strong className="admin-assign-kpi-value is-amber">
                            {kpiMetrics.unassignedLotsCount}
                        </strong>
                        <small className="admin-assign-kpi-desc">
                            {kpiMetrics.unassignedLotsCount > 0
                                ? isVi
                                    ? "Cần bổ sung nhân viên trực bãi"
                                    : "Requires staff assignment"
                                : isVi
                                  ? "Tất cả bãi đã có nhân sự"
                                  : "All lots staffed"}
                        </small>
                    </div>
                </article>
            </section>

            {/* Error Banner */}
            {errorMsg && (
                <div className="admin-error-box" role="alert">
                    <div className="admin-error-box-main">
                        <AdminIcon name="alert" size={18} />
                        <div>
                            <strong>
                                {isVi
                                    ? "Không thể hoàn tất thao tác"
                                    : "Action could not be completed"}
                            </strong>
                            <p>{errorMsg}</p>
                        </div>
                    </div>
                    <button
                        type="button"
                        className="admin-btn admin-btn-xs admin-btn-ghost"
                        onClick={() => setRawError(null)}
                    >
                        ✕
                    </button>
                </div>
            )}

            {/* Main 2-Column Workspace */}
            <div className="admin-assign-workspace">
                {/* C. Left Panel: Parking Lot Selector */}
                <aside className="admin-card admin-assign-lots-panel">
                    <div className="admin-assign-lots-head">
                        <div>
                            <h2>
                                <AdminIcon name="lots" size={16} />
                                <span>{t("selectLotTitle")}</span>
                            </h2>
                            <small>
                                {isVi
                                    ? `${filteredLots.length}/${lots.length} bãi đỗ xe`
                                    : `${filteredLots.length}/${lots.length} parking lots`}
                            </small>
                        </div>
                    </div>

                    {/* Search & Status Filter for Lots */}
                    <div className="admin-assign-lots-filters">
                        <div className="admin-assign-lot-search">
                            <AdminIcon name="search" size={14} />
                            <input
                                type="text"
                                value={lotSearch}
                                onChange={(e) => setLotSearch(e.target.value)}
                                placeholder={
                                    isVi
                                        ? "Tìm theo tên bãi, địa chỉ, chủ bãi..."
                                        : "Search lot name, address, owner..."
                                }
                                aria-label={
                                    isVi
                                        ? "Tìm kiếm bãi đỗ xe"
                                        : "Search parking lots"
                                }
                            />
                            {lotSearch && (
                                <button
                                    type="button"
                                    className="admin-search-clear-btn"
                                    onClick={() => setLotSearch("")}
                                    title={isVi ? "Xóa tìm kiếm" : "Clear"}
                                >
                                    ✕
                                </button>
                            )}
                        </div>

                        <div
                            className="admin-assign-lot-status-pills"
                            role="tablist"
                            aria-label={
                                isVi
                                    ? "Lọc trạng thái bãi"
                                    : "Filter lot status"
                            }
                        >
                            {(
                                [
                                    {
                                        key: "All",
                                        label: isVi ? "Tất cả" : "All",
                                    },
                                    {
                                        key: "Approved",
                                        label: isVi ? "Hoạt động" : "Active",
                                    },
                                    {
                                        key: "Pending",
                                        label: isVi ? "Chờ duyệt" : "Pending",
                                    },
                                ] as const
                            ).map((tab) => (
                                <button
                                    key={tab.key}
                                    type="button"
                                    role="tab"
                                    aria-selected={lotStatusFilter === tab.key}
                                    className={`admin-assign-status-pill ${lotStatusFilter === tab.key ? "is-active" : ""}`}
                                    onClick={() => setLotStatusFilter(tab.key)}
                                >
                                    {tab.label}
                                </button>
                            ))}
                        </div>
                    </div>

                    {/* Lot List Items */}
                    {loadingLots ? (
                        <div
                            className="admin-assign-lots-skeleton"
                            aria-busy="true"
                        >
                            {[1, 2, 3].map((idx) => (
                                <div
                                    key={idx}
                                    className="admin-assign-lot-skeleton-card"
                                />
                            ))}
                            <p className="admin-empty">
                                {t("assignmentsLoadingLots")}
                            </p>
                        </div>
                    ) : filteredLots.length === 0 ? (
                        <div className="admin-assign-empty-box">
                            <AdminIcon name="lots" size={24} />
                            <strong>
                                {lots.length === 0
                                    ? isVi
                                        ? "Chưa có bãi đỗ xe nào"
                                        : "No parking lots available"
                                    : isVi
                                      ? "Không tìm thấy bãi đỗ xe phù hợp"
                                      : "No matching parking lots"}
                            </strong>
                            <p>
                                {lots.length === 0
                                    ? isVi
                                        ? "Hệ thống chưa ghi nhận bãi đỗ xe nào để phân công nhân sự."
                                        : "There are no parking lots registered in the system yet."
                                    : isVi
                                      ? "Hãy thử từ khóa khác hoặc đặt lại bộ lọc trạng thái."
                                      : "Try another keyword or reset the status filter."}
                            </p>
                            {(lotSearch || lotStatusFilter !== "All") && (
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-xs admin-btn-ghost"
                                    onClick={() => {
                                        setLotSearch("");
                                        setLotStatusFilter("All");
                                    }}
                                >
                                    {isVi ? "Xóa bộ lọc" : "Clear filters"}
                                </button>
                            )}
                        </div>
                    ) : (
                        <div
                            className="admin-assign-lots-list"
                            role="listbox"
                            aria-label={t("selectLotTitle")}
                        >
                            {filteredLots.map((lot) => {
                                const isSelected = lot.id === selectedLotId;
                                const staffCount = isSelected
                                    ? assignments.length
                                    : lotMemberCounts.get(lot.id);
                                const ownerText = resolveLotOwnerName(lot);

                                return (
                                    <button
                                        key={lot.id}
                                        type="button"
                                        role="option"
                                        aria-selected={isSelected}
                                        onClick={() => handleSelectLot(lot.id)}
                                        className={`admin-assign-lot-item ${isSelected ? "is-selected" : ""}`}
                                    >
                                        <div className="admin-assign-lot-item-top">
                                            <strong className="admin-assign-lot-name">
                                                {lot.name}
                                            </strong>
                                            <StatusPill status={lot.status} />
                                        </div>

                                        <div className="admin-assign-lot-address">
                                            <AdminIcon name="pin" size={13} />
                                            <span>
                                                {lot.address ||
                                                    lot.city ||
                                                    (isVi
                                                        ? "Chưa cập nhật địa chỉ"
                                                        : "Address not updated")}
                                            </span>
                                        </div>

                                        <div className="admin-assign-lot-footer">
                                            <span className="admin-assign-lot-owner">
                                                {t("lotOwnerHeader")}:{" "}
                                                <strong>{ownerText}</strong>
                                            </span>
                                            <span
                                                className={`admin-assign-lot-count-badge ${staffCount && staffCount > 0 ? "has-staff" : "is-empty"}`}
                                            >
                                                <AdminIcon
                                                    name="users"
                                                    size={12}
                                                />
                                                <span>
                                                    {typeof staffCount ===
                                                    "number"
                                                        ? isVi
                                                            ? `${staffCount} nhân sự`
                                                            : `${staffCount} staff`
                                                        : isVi
                                                          ? "Đang tải"
                                                          : "Loading"}
                                                </span>
                                            </span>
                                        </div>
                                    </button>
                                );
                            })}
                        </div>
                    )}
                </aside>

                {/* D. Right Panel: Selected Lot Details, Shift Schedule & Assigned Staff Table */}
                <div className="admin-assign-main-column">
                    {/* Selected Lot Summary Banner */}
                    {activeLot ? (
                        <section className="admin-card admin-assign-selected-banner">
                            <div className="admin-assign-selected-info">
                                <div className="admin-assign-selected-title-row">
                                    <span className="admin-assign-selected-tag">
                                        {isVi
                                            ? "BÃI ĐANG CHỌN"
                                            : "SELECTED LOT"}
                                    </span>
                                    <h2>{activeLot.name}</h2>
                                    <StatusPill status={activeLot.status} />
                                </div>
                                <div className="admin-assign-selected-meta">
                                    <span>
                                        <AdminIcon name="pin" size={14} />
                                        {activeLot.address ||
                                            activeLot.city ||
                                            (isVi
                                                ? "Chưa cập nhật địa chỉ"
                                                : "Address not updated")}
                                    </span>
                                    <span>
                                        <AdminIcon name="users" size={14} />
                                        {t("lotOwnerHeader")}:{" "}
                                        <strong>
                                            {resolveLotOwnerName(activeLot)}
                                        </strong>
                                    </span>
                                    <span>
                                        <AdminIcon name="shield" size={14} />
                                        {isVi ? "Đang trực:" : "Assigned:"}{" "}
                                        <strong>
                                            {assignments.length}{" "}
                                            {isVi ? "nhân sự" : "members"}
                                        </strong>
                                    </span>
                                </div>
                            </div>
                            <div className="admin-assign-selected-actions">
                                <Link
                                    href={`/admin/parking-lots/${encodeURIComponent(activeLot.id)}`}
                                    className="admin-btn admin-btn-ghost admin-btn-sm"
                                >
                                    <AdminIcon name="eye" size={14} />
                                    <span>
                                        {isVi ? "Chi tiết bãi" : "Lot Details"}
                                    </span>
                                </Link>
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-lime admin-btn-sm"
                                    onClick={() => openCreateAssignModal()}
                                >
                                    <AdminIcon name="plus" size={14} />
                                    <span>
                                        {isVi
                                            ? "+ Phân công nhân viên"
                                            : "+ Assign Staff"}
                                    </span>
                                </button>
                            </div>
                        </section>
                    ) : (
                        !loadingLots && (
                            <section className="admin-card admin-assign-empty-box">
                                <AdminIcon name="lots" size={28} />
                                <strong>
                                    {isVi
                                        ? "Chưa chọn bãi đỗ xe"
                                        : "No parking lot selected"}
                                </strong>
                                <p>
                                    {isVi
                                        ? "Vui lòng chọn một bãi đỗ xe ở danh sách bên trái để xem và quản lý nhân sự trực bãi."
                                        : "Please select a parking lot from the left panel to view and manage its assigned staff."}
                                </p>
                            </section>
                        )
                    )}

                    {/* F. Shift Management Overview Strip for Selected Lot */}
                    {activeLot && (
                        <section
                            className="admin-card admin-assign-shifts-card"
                            aria-label={
                                isVi
                                    ? "Lịch phân ca trực tại bãi"
                                    : "Shift schedule at lot"
                            }
                        >
                            <div className="admin-assign-shifts-header">
                                <div>
                                    <h3>
                                        <AdminIcon name="clock" size={16} />
                                        <span>
                                            {isVi
                                                ? "Quản lý ca trực tại bãi"
                                                : "Shift Schedule Overview"}
                                        </span>
                                    </h3>
                                    <small>
                                        {isVi
                                            ? "Phân bổ nhân sự theo 4 khung giờ vận hành tiêu chuẩn"
                                            : "Staff distribution across 4 standard operational shifts"}
                                    </small>
                                </div>
                            </div>
                            <div className="admin-assign-shifts-grid">
                                {shiftBreakdown.map((slot) => (
                                    <div
                                        key={slot.key}
                                        className={`admin-assign-shift-box ${slot.members.length > 0 ? "has-members" : ""}`}
                                    >
                                        <div className="admin-assign-shift-top">
                                            <strong>{slot.shiftName}</strong>
                                            <span className="admin-assign-shift-count">
                                                {slot.members.length}
                                            </span>
                                        </div>
                                        <div className="admin-assign-shift-hours">
                                            {slot.hours}
                                        </div>
                                        <div className="admin-assign-shift-members">
                                            {slot.members.length > 0 ? (
                                                slot.members.map((m) => (
                                                    <span
                                                        key={m.accountId}
                                                        className="admin-assign-shift-member-chip"
                                                    >
                                                        {m.accountFullName}
                                                    </span>
                                                ))
                                            ) : (
                                                <span className="admin-cell-unupdated">
                                                    {isVi
                                                        ? "Chưa có người trực"
                                                        : "Unassigned"}
                                                </span>
                                            )}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </section>
                    )}

                    {/* D. Assigned Staff Table Card */}
                    <section className="admin-card admin-table-card admin-assign-table-section">
                        <div className="admin-assign-table-header">
                            <div>
                                <h2>
                                    {isVi
                                        ? "Nhân sự đang được phân công"
                                        : t("staffAtLotTitle")}
                                    {activeLot ? ` — ${activeLot.name}` : ""}
                                </h2>
                                <p>
                                    {isVi
                                        ? "Quản lý danh sách nhân sự được phân quyền kiểm soát cổng vào / cổng ra tại bãi này."
                                        : "Manage staff members authorized to operate entry and exit gates at this parking lot."}
                                </p>
                            </div>

                            {/* Table Search & Role Filter */}
                            <div className="admin-assign-table-controls">
                                <div className="admin-assign-member-search">
                                    <AdminIcon name="search" size={14} />
                                    <input
                                        type="text"
                                        value={memberSearch}
                                        onChange={(e) =>
                                            setMemberSearch(e.target.value)
                                        }
                                        placeholder={
                                            isVi
                                                ? "Tìm tên, email, SĐT, mã ID..."
                                                : "Search name, email, phone, ID..."
                                        }
                                        aria-label={
                                            isVi
                                                ? "Tìm nhân viên trong bãi"
                                                : "Search assigned staff"
                                        }
                                    />
                                    {memberSearch && (
                                        <button
                                            type="button"
                                            className="admin-search-clear-btn"
                                            onClick={() => setMemberSearch("")}
                                        >
                                            ✕
                                        </button>
                                    )}
                                </div>

                                <select
                                    className="admin-select admin-select-sm"
                                    value={memberRoleFilter}
                                    onChange={(e) =>
                                        setMemberRoleFilter(
                                            e.target.value as MemberRoleFilter,
                                        )
                                    }
                                    aria-label={
                                        isVi
                                            ? "Lọc theo vai trò"
                                            : "Filter by role"
                                    }
                                >
                                    <option value="All">
                                        {isVi ? "Tất cả vai trò" : "All roles"}
                                    </option>
                                    <option value="Staff">
                                        {formatRoleLabel("Staff")}
                                    </option>
                                    <option value="Owner">
                                        {formatRoleLabel("Owner")}
                                    </option>
                                </select>
                            </div>
                        </div>

                        <div className="admin-assign-table-wrap">
                            <table className="admin-table admin-assign-table">
                                <thead>
                                    <tr>
                                        <th>
                                            {isVi
                                                ? "Nhân viên"
                                                : t("assignmentsColStaff")}
                                        </th>
                                        <th>
                                            {isVi
                                                ? "Liên hệ"
                                                : t("assignmentsColContact")}
                                        </th>
                                        <th>
                                            {isVi
                                                ? "Vai trò & Trạng thái"
                                                : "Role & Status"}
                                        </th>
                                        <th>{t("assignmentsColShift")}</th>
                                        <th style={{ textAlign: "right" }}>
                                            {t("actions")}
                                        </th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {!loadingAssignments &&
                                        filteredAssignments.map((member) => (
                                            <tr key={member.accountId}>
                                                <td
                                                    data-label={
                                                        isVi
                                                            ? "Nhân viên"
                                                            : t(
                                                                  "assignmentsColStaff",
                                                              )
                                                    }
                                                >
                                                    <div className="admin-assign-staff-cell">
                                                        <span className="admin-assign-avatar">
                                                            {getInitials(
                                                                member.accountFullName,
                                                                member.accountEmail,
                                                            )}
                                                        </span>
                                                        <div className="admin-assign-staff-meta">
                                                            <strong>
                                                                {
                                                                    member.accountFullName
                                                                }
                                                            </strong>
                                                            <small className="admin-assign-id-chip">
                                                                ID:{" "}
                                                                {member.accountId.slice(
                                                                    0,
                                                                    8,
                                                                )}
                                                            </small>
                                                        </div>
                                                    </div>
                                                </td>
                                                <td
                                                    data-label={
                                                        isVi
                                                            ? "Liên hệ"
                                                            : t(
                                                                  "assignmentsColContact",
                                                              )
                                                    }
                                                >
                                                    <div className="admin-assign-contact-stack">
                                                        {member.accountEmail ? (
                                                            <span
                                                                className="admin-assign-contact-email"
                                                                title={
                                                                    member.accountEmail
                                                                }
                                                            >
                                                                {
                                                                    member.accountEmail
                                                                }
                                                            </span>
                                                        ) : (
                                                            <span className="admin-cell-unupdated">
                                                                {isVi
                                                                    ? "Chưa cập nhật email"
                                                                    : "No email"}
                                                            </span>
                                                        )}
                                                        {member.accountPhoneNumber ? (
                                                            <small className="admin-assign-contact-phone">
                                                                {
                                                                    member.accountPhoneNumber
                                                                }
                                                            </small>
                                                        ) : (
                                                            <small className="admin-cell-unupdated">
                                                                {isVi
                                                                    ? "Chưa cập nhật SĐT"
                                                                    : "No phone"}
                                                            </small>
                                                        )}
                                                    </div>
                                                </td>
                                                <td
                                                    data-label={
                                                        isVi
                                                            ? "Vai trò & Trạng thái"
                                                            : "Role & Status"
                                                    }
                                                >
                                                    <div className="admin-assign-role-status-stack">
                                                        <span
                                                            className={`admin-assign-role-badge ${member.role === "Owner" ? "is-owner" : "is-staff"}`}
                                                        >
                                                            {formatRoleLabel(
                                                                member.role,
                                                            )}
                                                        </span>
                                                        <div className="admin-assign-status-inline">
                                                            <span className="admin-assign-active-dot" />
                                                            <span>
                                                                {isVi
                                                                    ? "Đang hoạt động"
                                                                    : "Active"}
                                                            </span>
                                                            {member.assignedAtUtc && (
                                                                <small>
                                                                    ·{" "}
                                                                    {formatShortDate(
                                                                        member.assignedAtUtc,
                                                                        isVi,
                                                                    )}
                                                                </small>
                                                            )}
                                                        </div>
                                                    </div>
                                                </td>
                                                <td
                                                    data-label={t(
                                                        "assignmentsColShift",
                                                    )}
                                                >
                                                    <div className="admin-assign-shift-cell">
                                                        <span className="admin-assign-shift-badge">
                                                            {member.shiftName ||
                                                                "Ca 1 - Sáng"}
                                                        </span>
                                                        {member.hours && (
                                                            <small className="admin-assign-shift-time">
                                                                {member.hours}
                                                            </small>
                                                        )}
                                                    </div>
                                                </td>
                                                <td
                                                    data-label={t("actions")}
                                                    style={{
                                                        textAlign: "right",
                                                    }}
                                                >
                                                    <div className="admin-row-actions">
                                                        <button
                                                            type="button"
                                                            className="admin-btn admin-btn-xs admin-btn-secondary-view"
                                                            onClick={() =>
                                                                openEditAssignModal(
                                                                    member,
                                                                )
                                                            }
                                                            disabled={
                                                                actionBusy
                                                            }
                                                            title={
                                                                isVi
                                                                    ? "Chỉnh sửa vai trò & ca trực"
                                                                    : "Edit role & shift"
                                                            }
                                                        >
                                                            {isVi
                                                                ? "Chỉnh sửa"
                                                                : "Edit"}
                                                        </button>
                                                        <button
                                                            type="button"
                                                            className="admin-btn admin-btn-xs admin-btn-danger-ghost"
                                                            onClick={() =>
                                                                setMemberToRemove(
                                                                    {
                                                                        accountId:
                                                                            member.accountId,
                                                                        fullName:
                                                                            member.accountFullName ||
                                                                            "Staff",
                                                                        email:
                                                                            member.accountEmail ||
                                                                            "",
                                                                        role: member.role,
                                                                        shiftLabel: `${member.shiftName || ""} (${member.hours || ""})`,
                                                                    },
                                                                )
                                                            }
                                                            disabled={
                                                                actionBusy
                                                            }
                                                            title={t(
                                                                "removeStaffBtn",
                                                            )}
                                                        >
                                                            {t(
                                                                "removeStaffBtn",
                                                            )}
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ))}
                                </tbody>
                            </table>
                        </div>

                        {loadingAssignments && (
                            <div className="admin-assign-empty-box">
                                <p className="admin-empty">
                                    {t("assignmentsLoadingAssignments")}
                                </p>
                            </div>
                        )}

                        {!loadingAssignments && assignments.length === 0 && (
                            <div className="admin-assign-empty-box">
                                <AdminIcon name="users" size={26} />
                                <strong>{t("noStaffAssigned")}</strong>
                                <p>
                                    {isVi
                                        ? "Bãi đỗ xe này hiện chưa có nhân viên hoặc chủ bãi nào được phân công trực."
                                        : "No staff or owner accounts are currently assigned to this parking lot."}
                                </p>
                                {selectedLotId && (
                                    <button
                                        type="button"
                                        className="admin-btn admin-btn-lime admin-btn-sm"
                                        onClick={() => openCreateAssignModal()}
                                    >
                                        <AdminIcon name="plus" size={14} />
                                        <span>
                                            {isVi
                                                ? "+ Phân công nhân viên ngay"
                                                : "+ Assign Staff Now"}
                                        </span>
                                    </button>
                                )}
                            </div>
                        )}

                        {!loadingAssignments &&
                            assignments.length > 0 &&
                            filteredAssignments.length === 0 && (
                                <div className="admin-assign-empty-box">
                                    <AdminIcon name="search" size={24} />
                                    <strong>
                                        {isVi
                                            ? "Không tìm thấy nhân sự phù hợp"
                                            : "No matching staff found"}
                                    </strong>
                                    <p>
                                        {isVi
                                            ? "Không có nhân viên nào khớp với từ khóa hoặc bộ lọc vai trò hiện tại."
                                            : "No assigned members match the current search keyword or role filter."}
                                    </p>
                                    <button
                                        type="button"
                                        className="admin-btn admin-btn-xs admin-btn-ghost"
                                        onClick={() => {
                                            setMemberSearch("");
                                            setMemberRoleFilter("All");
                                        }}
                                    >
                                        {isVi ? "Xóa bộ lọc" : "Clear filters"}
                                    </button>
                                </div>
                            )}
                    </section>

                    {/* Quick Inline Assignment Card (keeps rapid workflow accessible + synced with Modal) */}
                    <section className="admin-card admin-assign-quick-card">
                        <div className="admin-assign-quick-head">
                            <div>
                                <h3>
                                    <AdminIcon name="plus" size={16} />
                                    <span>{t("assignStaffTitle")}</span>
                                </h3>
                                <small>
                                    {isVi
                                        ? "Chọn nhanh nhân sự sẵn sàng bên dưới hoặc mở biểu mẫu chi tiết để kiểm tra đầy đủ thông tin."
                                        : "Quickly assign an available staff member below or open the detailed assignment modal."}
                                </small>
                            </div>
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost admin-btn-sm"
                                onClick={() => openCreateAssignModal()}
                            >
                                {isVi
                                    ? "Mở cửa sổ phân công"
                                    : "Open Assignment Modal"}
                            </button>
                        </div>

                        <form
                            onSubmit={(e) => {
                                setModalLotId(selectedLotId);
                                void handleAssignMember(e);
                            }}
                            className="admin-assign-quick-form"
                        >
                            <label className="admin-field">
                                <span>{t("selectStaffLabel")}</span>
                                <select
                                    value={selectedAccountId}
                                    onChange={(e) =>
                                        setSelectedAccountId(e.target.value)
                                    }
                                    required
                                    className="admin-select"
                                >
                                    <option value="">
                                        {t("selectStaffDefault")}
                                    </option>
                                    {assignableStaff.map((staff) => (
                                        <option
                                            key={staff.accountId}
                                            value={staff.accountId}
                                        >
                                            {staff.fullName} ({staff.email}) —{" "}
                                            {isVi
                                                ? "Sẵn sàng phân công"
                                                : "Available"}
                                        </option>
                                    ))}
                                    {busyStaff.map(({ staff, assignedTo }) => (
                                        <option
                                            key={staff.accountId}
                                            value={staff.accountId}
                                            disabled
                                        >
                                            🔒 {staff.fullName} (
                                            {isVi
                                                ? `Đã trực tại: ${assignedTo.lotName} - 1 người/1 bãi`
                                                : `Assigned to: ${assignedTo.lotName}`}
                                            )
                                        </option>
                                    ))}
                                </select>
                            </label>

                            <label className="admin-field">
                                <span>{t("assignRoleLabel")}</span>
                                <select
                                    value={selectedRole}
                                    onChange={(e) =>
                                        setSelectedRole(
                                            e.target.value as "Staff" | "Owner",
                                        )
                                    }
                                    className="admin-select"
                                >
                                    <option value="Staff">
                                        {formatRoleLabel("Staff")}
                                    </option>
                                    <option value="Owner">
                                        {formatRoleLabel("Owner")}
                                    </option>
                                </select>
                            </label>

                            <label className="admin-field">
                                <span>{t("assignmentsSelectShift")}</span>
                                <select
                                    value={selectedShift}
                                    onChange={(e) =>
                                        setSelectedShift(
                                            e.target.value as ShiftType,
                                        )
                                    }
                                    className="admin-select"
                                >
                                    <option value="morning">
                                        {t("shiftMorning")}
                                    </option>
                                    <option value="afternoon">
                                        {t("shiftAfternoon")}
                                    </option>
                                    <option value="night">
                                        {t("shiftNight")}
                                    </option>
                                    <option value="allday">
                                        {t("shiftAllDay")}
                                    </option>
                                </select>
                            </label>

                            <button
                                type="submit"
                                className="admin-btn admin-btn-lime admin-assign-submit-btn"
                                disabled={
                                    actionBusy ||
                                    !selectedAccountId ||
                                    !selectedLotId
                                }
                            >
                                {actionBusy
                                    ? t("assignmentsAssigning")
                                    : t("assignBtn")}
                            </button>
                        </form>
                    </section>
                </div>
            </div>

            {/* E. Modal: Add / Edit Staff Assignment */}
            {isAssignModalOpen && (
                <div
                    className="admin-modal-backdrop"
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="assign-modal-title"
                    onClick={closeAssignModal}
                >
                    <div
                        className="admin-modal admin-assign-modal"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="admin-assign-modal-header">
                            <div>
                                <span className="admin-eyebrow">
                                    {editingMember
                                        ? isVi
                                            ? "CẬP NHẬT PHÂN CÔNG"
                                            : "UPDATE ASSIGNMENT"
                                        : isVi
                                          ? "THÊM PHÂN CÔNG MỚI"
                                          : "NEW STAFF ASSIGNMENT"}
                                </span>
                                <h2 id="assign-modal-title">
                                    {editingMember
                                        ? isVi
                                            ? "Chỉnh sửa vai trò & ca trực"
                                            : "Edit Role & Shift Schedule"
                                        : isVi
                                          ? "Phân công nhân viên trực bãi"
                                          : "Assign Staff to Parking Lot"}
                                </h2>
                            </div>
                            <button
                                type="button"
                                className="admin-btn admin-btn-xs admin-btn-ghost"
                                onClick={closeAssignModal}
                                disabled={actionBusy}
                                aria-label={isVi ? "Đóng" : "Close"}
                            >
                                ✕
                            </button>
                        </div>

                        <form
                            onSubmit={handleAssignMember}
                            className="admin-assign-modal-body"
                        >
                            {modalError && (
                                <div
                                    className="admin-error-box"
                                    role="alert"
                                    style={{ marginBottom: 0 }}
                                >
                                    <div className="admin-error-box-main">
                                        <AdminIcon name="alert" size={16} />
                                        <p>{modalError}</p>
                                    </div>
                                </div>
                            )}

                            {/* 1. Chọn bãi đỗ xe */}
                            <label className="admin-field">
                                <span>
                                    1.{" "}
                                    {isVi
                                        ? "Chọn bãi đỗ xe"
                                        : "Select Parking Lot"}
                                </span>
                                <select
                                    className="admin-select"
                                    value={modalLotId || selectedLotId}
                                    onChange={(e) =>
                                        setModalLotId(e.target.value)
                                    }
                                    disabled={Boolean(editingMember)}
                                    required
                                >
                                    {lots.map((lot) => (
                                        <option key={lot.id} value={lot.id}>
                                            {lot.name} —{" "}
                                            {lot.address ||
                                                lot.city ||
                                                (isVi
                                                    ? "Chưa cập nhật địa chỉ"
                                                    : "No address")}
                                        </option>
                                    ))}
                                </select>
                            </label>

                            {/* 2. Chọn nhân viên */}
                            <label className="admin-field">
                                <span>2. {t("selectStaffLabel")}</span>
                                <select
                                    className="admin-select"
                                    value={selectedAccountId}
                                    onChange={(e) =>
                                        setSelectedAccountId(e.target.value)
                                    }
                                    disabled={Boolean(editingMember)}
                                    required
                                >
                                    <option value="">
                                        {t("selectStaffDefault")}
                                    </option>
                                    {editingMember && (
                                        <option value={editingMember.accountId}>
                                            {editingMember.accountFullName} (
                                            {editingMember.accountEmail ||
                                                editingMember.accountId}
                                            )
                                        </option>
                                    )}
                                    {assignableStaff.map((staff) => (
                                        <option
                                            key={staff.accountId}
                                            value={staff.accountId}
                                        >
                                            {staff.fullName} ({staff.email}) —{" "}
                                            {isVi
                                                ? "Sẵn sàng phân công"
                                                : "Available"}
                                        </option>
                                    ))}
                                    {!editingMember &&
                                        busyStaff.map(
                                            ({ staff, assignedTo }) => (
                                                <option
                                                    key={staff.accountId}
                                                    value={staff.accountId}
                                                    disabled
                                                >
                                                    🔒 {staff.fullName} (
                                                    {isVi
                                                        ? `Đã trực tại: ${assignedTo.lotName}`
                                                        : `Assigned to: ${assignedTo.lotName}`}
                                                    )
                                                </option>
                                            ),
                                        )}
                                </select>
                            </label>

                            {/* 3. Chọn vai trò & 4. Chọn ca trực */}
                            <div className="admin-assign-modal-grid-2">
                                <label className="admin-field">
                                    <span>3. {t("assignRoleLabel")}</span>
                                    <select
                                        className="admin-select"
                                        value={selectedRole}
                                        onChange={(e) =>
                                            setSelectedRole(
                                                e.target.value as
                                                    "Staff" | "Owner",
                                            )
                                        }
                                    >
                                        <option value="Staff">
                                            {formatRoleLabel("Staff")}
                                        </option>
                                        <option value="Owner">
                                            {formatRoleLabel("Owner")}
                                        </option>
                                    </select>
                                </label>

                                <label className="admin-field">
                                    <span>
                                        4. {t("assignmentsSelectShift")}
                                    </span>
                                    <select
                                        className="admin-select"
                                        value={selectedShift}
                                        onChange={(e) =>
                                            setSelectedShift(
                                                e.target.value as ShiftType,
                                            )
                                        }
                                    >
                                        <option value="morning">
                                            {t("shiftMorning")}
                                        </option>
                                        <option value="afternoon">
                                            {t("shiftAfternoon")}
                                        </option>
                                        <option value="night">
                                            {t("shiftNight")}
                                        </option>
                                        <option value="allday">
                                            {t("shiftAllDay")}
                                        </option>
                                    </select>
                                </label>
                            </div>

                            {/* 5. Hiển thị thông tin nhân viên đã chọn */}
                            <div className="admin-assign-staff-preview">
                                <span className="admin-assign-preview-title">
                                    {isVi
                                        ? "Thông tin nhân sự được chọn"
                                        : "Selected Staff Summary"}
                                </span>
                                {selectedStaffPreview || editingMember ? (
                                    <div className="admin-assign-preview-card">
                                        <span className="admin-assign-avatar">
                                            {getInitials(
                                                selectedStaffPreview?.fullName ||
                                                    editingMember?.accountFullName,
                                                selectedStaffPreview?.email ||
                                                    editingMember?.accountEmail,
                                            )}
                                        </span>
                                        <div className="admin-assign-preview-details">
                                            <strong>
                                                {selectedStaffPreview?.fullName ||
                                                    editingMember?.accountFullName}
                                            </strong>
                                            <span>
                                                {selectedStaffPreview?.email ||
                                                    editingMember?.accountEmail ||
                                                    (isVi
                                                        ? "Chưa cập nhật email"
                                                        : "Email not updated")}
                                            </span>
                                            <small>
                                                {SHIFT_DETAILS[selectedShift]
                                                    ?.shiftName || ""}{" "}
                                                (
                                                {SHIFT_DETAILS[selectedShift]
                                                    ?.hours || ""}
                                                ) ·{" "}
                                                {formatRoleLabel(selectedRole)}
                                            </small>
                                        </div>
                                    </div>
                                ) : (
                                    <p className="admin-cell-unupdated">
                                        {isVi
                                            ? "Vui lòng chọn một nhân viên từ danh sách để xem trước thông tin phân công."
                                            : "Select a staff account above to preview assignment details."}
                                    </p>
                                )}
                            </div>

                            <div className="admin-assign-modal-footer">
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-ghost"
                                    onClick={closeAssignModal}
                                    disabled={actionBusy}
                                >
                                    {isVi ? "Hủy" : "Cancel"}
                                </button>
                                <button
                                    type="submit"
                                    className="admin-btn admin-btn-lime"
                                    disabled={
                                        actionBusy ||
                                        !selectedAccountId ||
                                        !(modalLotId || selectedLotId)
                                    }
                                >
                                    {actionBusy
                                        ? t("assignmentsAssigning")
                                        : isVi
                                          ? "Xác nhận phân công"
                                          : "Confirm Assignment"}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Dialog xác nhận gỡ nhân viên */}
            {memberToRemove && (
                <div
                    className="admin-modal-backdrop"
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="remove-member-modal-title"
                    onClick={() => !actionBusy && setMemberToRemove(null)}
                >
                    <div
                        className="admin-modal admin-owner-reject-modal"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="admin-owner-reject-top">
                            <div className="admin-modal-icon danger">
                                <AdminIcon name="alert" size={20} />
                            </div>
                            <button
                                type="button"
                                className="admin-btn admin-btn-xs admin-btn-ghost"
                                onClick={() => setMemberToRemove(null)}
                                disabled={actionBusy}
                            >
                                ✕
                            </button>
                        </div>

                        <h2 id="remove-member-modal-title">
                            {isVi
                                ? "Xác nhận gỡ nhân sự khỏi bãi"
                                : "Confirm Staff Removal"}
                        </h2>
                        <p className="admin-owner-reject-desc">
                            {t("assignmentsRemoveConfirm").replace(
                                "{name}",
                                memberToRemove.fullName,
                            )}
                        </p>

                        <div className="admin-owner-reject-summary">
                            <div>
                                <span>{isVi ? "Nhân sự" : "Staff Member"}</span>
                                <strong>{memberToRemove.fullName}</strong>
                            </div>
                            <div>
                                <span>
                                    {isVi ? "Bãi đỗ xe" : "Parking Lot"}
                                </span>
                                <strong>{activeLot?.name || "—"}</strong>
                            </div>
                            <div>
                                <span>{isVi ? "Vai trò" : "Role"}</span>
                                <strong>
                                    {formatRoleLabel(memberToRemove.role)}
                                </strong>
                            </div>
                            <div>
                                <span>{isVi ? "Ca trực" : "Shift"}</span>
                                <strong>{memberToRemove.shiftLabel}</strong>
                            </div>
                        </div>

                        <div
                            className="admin-modal-actions"
                            style={{ marginTop: "18px" }}
                        >
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost"
                                onClick={() => setMemberToRemove(null)}
                                disabled={actionBusy}
                            >
                                {isVi ? "Hủy" : "Cancel"}
                            </button>
                            <button
                                type="button"
                                className="admin-btn admin-btn-danger"
                                onClick={() =>
                                    executeRemoveMember(
                                        memberToRemove.accountId,
                                        memberToRemove.fullName,
                                    )
                                }
                                disabled={actionBusy}
                            >
                                {actionBusy
                                    ? isVi
                                        ? "Đang gỡ..."
                                        : "Removing..."
                                    : isVi
                                      ? "Xác nhận gỡ khỏi bãi"
                                      : "Confirm Removal"}
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
        </div>
    );
}
