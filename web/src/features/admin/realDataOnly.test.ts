import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import {
    changeAccountRole,
    describeAccountError,
    searchAccounts,
    suspendAccount,
} from "./adminAccounts";
import {
    assignLotMember,
    getAvailableStaffAccounts,
    getLotAssignments,
    removeLotMember,
} from "./lotAssignmentsApi";
import {
    downloadOwnerApplicationDocument,
    getOwnerApplications,
} from "./ownerApplicationsApi";
import {
    getAdminParkingLotDetail,
    getAdminParkingLotLayout,
    getAdminParkingLotPricing,
    getAdminParkingLots,
} from "./reviewParkingLot";

// Demo 1: an admin screen must show what the backend answered. A failed call is an error on screen,
// never sample data or a success message.
const LOT = "10000000-0000-0000-0000-000000000001";
const ACCOUNT = "20000000-0000-0000-0000-000000000001";

function problem(status: number, errorCode: string) {
    return new Response(JSON.stringify({ status, title: "error", errorCode }), {
        status,
        headers: { "Content-Type": "application/problem+json" },
    });
}

function json(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
    });
}

function stubFetch(...responses: Response[]) {
    const fetchMock = vi.fn();
    for (const response of responses) fetchMock.mockResolvedValueOnce(response);
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
}

afterEach(() => {
    vi.unstubAllGlobals();
});

describe("admin calls surface backend errors", () => {
    it("owner application list rejects instead of returning samples", async () => {
        stubFetch(problem(500, "identity.unexpected"));
        await expect(
            getOwnerApplications({ status: "Pending" }),
        ).rejects.toBeInstanceOf(ApiError);
    });

    it("account search rejects instead of returning samples", async () => {
        stubFetch(problem(500, "identity.unexpected"));
        await expect(searchAccounts({ search: "a" })).rejects.toBeInstanceOf(
            ApiError,
        );
    });

    it("suspending an account rejects when the backend refuses", async () => {
        stubFetch(problem(403, "identity.cannot_change_admin_status"));
        await expect(suspendAccount(ACCOUNT)).rejects.toBeInstanceOf(ApiError);
    });

    it("a blocked role change is an error with a clear message, not a success", async () => {
        stubFetch(problem(409, "identity.owner_role_requires_application"));
        const error = await changeAccountRole(ACCOUNT, "ParkingLotOwner").catch(
            (e: unknown) => e,
        );
        expect(error).toBeInstanceOf(ApiError);
        expect(describeAccountError(error, "vi")).toMatch(/hồ sơ chủ bãi/i);
    });

    it("assigning staff rejects when the backend refuses", async () => {
        stubFetch(problem(404, "parking.lot_not_found"));
        await expect(
            assignLotMember(LOT, ACCOUNT, "Staff"),
        ).rejects.toBeInstanceOf(ApiError);
    });

    it("removing staff rejects when the backend refuses", async () => {
        stubFetch(problem(404, "parking.lot_not_found"));
        await expect(removeLotMember(LOT, ACCOUNT)).rejects.toBeInstanceOf(
            ApiError,
        );
    });

    it("the lot list rejects instead of returning samples", async () => {
        stubFetch(problem(500, "parking.unexpected"));
        await expect(getAdminParkingLots("All")).rejects.toBeInstanceOf(
            ApiError,
        );
    });
});

describe("admin calls read the real backend shapes", () => {
    it("assignments are the backend rows, even when there are none", async () => {
        stubFetch(json([]));
        await expect(getLotAssignments(LOT)).resolves.toEqual([]);

        stubFetch(
            json([
                {
                    accountId: ACCOUNT,
                    role: "Staff",
                    assignedAtUtc: "2026-10-29T01:00:00Z",
                },
            ]),
        );
        await expect(getLotAssignments(LOT)).resolves.toEqual([
            {
                lotId: LOT,
                accountId: ACCOUNT,
                role: "Staff",
                assignedAtUtc: "2026-10-29T01:00:00Z",
            },
        ]);
    });

    it("staff and owner accounts come from the admin account search pages", async () => {
        const fetchMock = stubFetch(
            json({
                items: [
                    {
                        id: ACCOUNT,
                        email: "staff@x.vn",
                        fullName: "Nhân viên A",
                        role: "Staff",
                    },
                ],
                page: 1,
                pageSize: 100,
                totalCount: 1,
            }),
            json({
                items: [
                    {
                        id: "30000000-0000-0000-0000-000000000001",
                        email: "owner@x.vn",
                        fullName: null,
                        role: "ParkingLotOwner",
                    },
                ],
                page: 1,
                pageSize: 100,
                totalCount: 1,
            }),
        );

        const accounts = await getAvailableStaffAccounts();

        expect(String(fetchMock.mock.calls[0][0])).toContain(
            "/api/identity/admin/accounts?role=Staff&page=1&pageSize=100",
        );
        expect(String(fetchMock.mock.calls[1][0])).toContain(
            "role=ParkingLotOwner",
        );
        expect(accounts).toEqual([
            {
                accountId: ACCOUNT,
                fullName: "Nhân viên A",
                email: "staff@x.vn",
                role: "Staff",
            },
            {
                accountId: "30000000-0000-0000-0000-000000000001",
                fullName: "owner@x.vn",
                email: "owner@x.vn",
                role: "Owner",
            },
        ]);
    });

    it("the lot list keeps only what the backend sent", async () => {
        stubFetch(
            json({
                items: [
                    {
                        id: LOT,
                        ownerUserId: ACCOUNT,
                        name: "Bãi A",
                        address: "1 Lê Lợi",
                        latitude: 10.7,
                        longitude: 106.7,
                        type: "Outdoor",
                        approvalStatus: "Pending",
                        rejectionReason: null,
                    },
                ],
                totalCount: 1,
                page: 1,
                pageSize: 20,
            }),
        );

        const [lot] = await getAdminParkingLots("Pending");

        expect(lot).toEqual({
            id: LOT,
            name: "Bãi A",
            address: "1 Lê Lợi",
            type: "Outdoor",
            ownerUserId: ACCOUNT,
            status: "Pending",
            rejectionReason: null,
            latitude: 10.7,
            longitude: 106.7,
        });
    });

    it("detail, layout and prices use the #128 admin routes; a missing price is null", async () => {
        const fetchMock = stubFetch(
            json({
                id: LOT,
                ownerUserId: ACCOUNT,
                name: "Bãi A",
                address: "1 Lê Lợi",
                latitude: 10.7,
                longitude: 106.7,
                type: "Outdoor",
                approvalStatus: "Pending",
                rejectionReason: null,
                openingHours: null,
            }),
            json({ lotId: LOT, version: 1, floors: [] }),
            problem(404, "parking.pricing_policy_not_found"),
        );

        const detail = await getAdminParkingLotDetail(LOT);
        const layout = await getAdminParkingLotLayout(LOT);
        const price = await getAdminParkingLotPricing(LOT, "Car");

        expect(detail.name).toBe("Bãi A");
        expect(layout.floors).toEqual([]);
        expect(price).toBeNull();
        expect(String(fetchMock.mock.calls[0][0])).toContain(
            `/api/parking/admin/lots/${LOT}`,
        );
        expect(String(fetchMock.mock.calls[1][0])).toContain(
            `/api/parking/admin/lots/${LOT}/layout`,
        );
        expect(String(fetchMock.mock.calls[2][0])).toContain(
            `/api/parking/admin/lots/${LOT}/pricing/Car`,
        );
    });

    it("an owner document downloads as a blob from the admin document route", async () => {
        const fetchMock = stubFetch(
            new Response(new Blob(["%PDF-1.7"], { type: "application/pdf" }), {
                status: 200,
                headers: { "Content-Type": "application/pdf" },
            }),
        );

        const blob = await downloadOwnerApplicationDocument(
            ACCOUNT,
            "PremisesRightProof",
        );

        expect(blob.type).toBe("application/pdf");
        expect(String(fetchMock.mock.calls[0][0])).toBe(
            `http://localhost:5000/api/identity/admin/owner-applications/${ACCOUNT}/documents/PremisesRightProof`,
        );
    });
});
