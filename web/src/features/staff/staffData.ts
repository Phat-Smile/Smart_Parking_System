"use client";

import { useSyncExternalStore } from "react";
import type {
    AssignedLotResponse,
    LotParkingSessionItem,
    OccupancyStats,
    SessionStatus,
    StaffSession,
    VehicleType,
    VerificationIssue,
} from "./types";
import { toStaffCustomerType } from "./types";
import { getAssignedLot, listLotSessions } from "./staffApi";

export interface GateActivity {
    id: string;
    type: "entry" | "exit";
    plateNumber: string;
    vehicleType: string;
    customerType?: string;
    timestamp: string;
    slotCode?: string | null;
    amount?: number;
}

/*
 * What the staff screens show, read from Parking: the assigned lot, its free spaces and its active
 * sessions. Activities are the actions taken in this browser tab. Gate plate readings (#136, #137) are
 * not wired yet, so there are no verification issues.
 */
let assignedLotState: AssignedLotResponse | null = null;
let sessionsState: StaffSession[] = [];
const issuesState: VerificationIssue[] = [];
let activitiesState: GateActivity[] = [];
let loadErrorState: unknown = null;
let loaded = false;
let refreshing: Promise<AssignedLotResponse | null> | null = null;
const listeners = new Set<() => void>();

function notify() {
    listeners.forEach((l) => l());
}

function toStaffSession(
    item: LotParkingSessionItem,
    lotId: string,
): StaffSession {
    return {
        id: item.sessionId,
        lotId,
        plateNumber: item.plateNumber,
        vehicleType: item.vehicleType as VehicleType,
        customerType: toStaffCustomerType(item.customerType),
        entryTime: item.entryTime,
        status: item.status as SessionStatus,
        slotCode: item.slotCode,
        // Session payment does not exist before Sprint 3.
        paymentStatus: "Pending",
    };
}

function refresh(): Promise<AssignedLotResponse | null> {
    refreshing ??= (async () => {
        try {
            const lot = await getAssignedLot();
            const page = lot
                ? await listLotSessions(lot.lotId, "Active", 1, 100)
                : null;
            assignedLotState = lot;
            sessionsState = page
                ? page.items.map((item) => toStaffSession(item, lot!.lotId))
                : [];
            loadErrorState = null;
        } catch (error) {
            loadErrorState = error;
        } finally {
            loaded = true;
            refreshing = null;
            notify();
        }
        return assignedLotState;
    })();
    return refreshing;
}

function addActivity(activity: GateActivity) {
    activitiesState = [activity, ...activitiesState].slice(0, 20);
}

export const staffStore = {
    getSessions: () => sessionsState,
    getIssues: () => issuesState,
    getActivities: () => activitiesState,
    getAssignedLot: () => assignedLotState,
    getLoadError: () => loadErrorState,

    setAssignedLot: (lot: AssignedLotResponse) => {
        assignedLotState = lot;
        notify();
    },

    fetchAssignedLot: refresh,
    refresh,

    /** Called after Parking opened the session; counts and the list are read again from Parking. */
    addSession: (session: StaffSession) => {
        addActivity({
            id: `act-${session.id}`,
            type: "entry",
            plateNumber: session.plateNumber,
            vehicleType: session.vehicleType,
            customerType: session.customerType,
            timestamp: session.entryTime,
            slotCode: session.slotCode,
        });
        notify();
        void refresh();
    },

    /** Called after Parking cancelled the session. */
    cancelSession: (sessionId?: string) => {
        void sessionId;
        void refresh();
    },

    /** Called after Parking completed the exit (not available before Sprint 3). */
    completeExit: (sessionId: string, paymentMethod?: string) => {
        void paymentMethod;
        const found = sessionsState.find((s) => s.id === sessionId);
        if (found) {
            addActivity({
                id: `act-exit-${sessionId}`,
                type: "exit",
                plateNumber: found.plateNumber,
                vehicleType: found.vehicleType,
                customerType: found.customerType,
                timestamp: new Date().toISOString(),
                amount: found.amountDueVnd,
            });
        }
        void refresh();
    },

    resolveIssue: (issueId?: string) => {
        // No gate plate readings yet.
        void issueId;
    },

    subscribe: (listener: () => void) => {
        listeners.add(listener);
        if (!loaded) void refresh();
        return () => listeners.delete(listener);
    },
};

const EMPTY_SESSIONS: StaffSession[] = [];
const EMPTY_ISSUES: VerificationIssue[] = [];
const EMPTY_ACTIVITIES: GateActivity[] = [];

export function useStaffData() {
    const sessions = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getSessions,
        () => EMPTY_SESSIONS,
    );
    const issues = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getIssues,
        () => EMPTY_ISSUES,
    );
    const activities = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getActivities,
        () => EMPTY_ACTIVITIES,
    );
    const assignedLot = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getAssignedLot,
        () => null,
    );
    const loadError = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getLoadError,
        () => null,
    );

    const activeSessions = sessions.filter((s) => s.status === "Active");
    const pendingIssues = issues.filter((i) => i.status === "Pending");

    const occupancy: OccupancyStats = {
        totalCapacity: assignedLot?.motorbikeCapacity ?? null,
        occupied: activeSessions.length,
        carAvailable: assignedLot?.availableCarSlots ?? null,
        motorbikeAvailable: assignedLot?.availableMotorbikeSlots ?? null,
        activeSessionsCount: activeSessions.length,
        needsVerificationCount: pendingIssues.length,
    };

    return {
        sessions,
        activeSessions,
        issues: pendingIssues,
        activities,
        occupancy,
        assignedLot,
        loadError,
        fetchAssignedLot: staffStore.fetchAssignedLot,
        setAssignedLot: staffStore.setAssignedLot,
        addSession: staffStore.addSession,
        cancelSession: staffStore.cancelSession,
        completeExit: staffStore.completeExit,
        resolveIssue: staffStore.resolveIssue,
    };
}
