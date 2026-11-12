import { afterEach, describe, expect, it, vi } from "vitest";
import { searchCustomerParkingLots } from "./parkingLotApi";
import {
    getPublicParkingLayout,
    getPublicZoneUtilization,
} from "./parkingLayoutApi";
import {
    getParkingSessionFee,
    payParkingFeeByWallet,
    createVnPayParkingFeePayment,
} from "./parkingFeeApi";
import { getMyVehicle, updateMyVehicle } from "./myVehiclesApi";
import {
    deleteCustomerMedia,
    deleteMyProfileIdentity,
} from "./customerProfileApi";
import {
    getAccountSessions,
    revokeAccountSession,
    setupMfa,
    confirmMfa,
    disableMfa,
    regenerateMfaRecoveryCodes,
} from "../auth/customerSecurityApi";

afterEach(() => vi.unstubAllGlobals());

describe("Customer APIs already provided by backend", () => {
    it("sends supported search filters and sort to Parking", async () => {
        const fetchMock = vi
            .fn()
            .mockResolvedValue(new Response("[]", { status: 200 }));
        vi.stubGlobal("fetch", fetchMock);
        await searchCustomerParkingLots({
            latitude: 10.8,
            longitude: 106.7,
            radiusKm: 2,
            vehicleType: "Car",
            maxPrice: 30000,
            availableOnly: true,
            compatibility: ["car", "ev"],
            amenities: ["cctv", "covered"],
            sort: "price",
        });
        const url = new URL(String(fetchMock.mock.calls[0][0]));
        expect(url.pathname).toBe("/api/parking/lots/search");
        expect(Object.fromEntries(url.searchParams)).toMatchObject({
            latitude: "10.8",
            longitude: "106.7",
            radiusKm: "2",
            vehicleType: "Car",
            maxPrice: "30000",
            availableOnly: "true",
            compatibility: "car,ev",
            amenities: "cctv,covered",
            sort: "price",
        });
        expect(url.searchParams.has("arrivalAt")).toBe(false);
    });

    it("reads public layout and utilization without caching", async () => {
        const fetchMock = vi
            .fn()
            .mockImplementation(
                async () => new Response("{}", { status: 200 }),
            );
        vi.stubGlobal("fetch", fetchMock);
        await getPublicParkingLayout("lot-id");
        await getPublicZoneUtilization("lot-id");
        expect(new URL(String(fetchMock.mock.calls[0][0])).pathname).toBe(
            "/api/parking/lots/lot-id/layout",
        );
        expect(new URL(String(fetchMock.mock.calls[1][0])).pathname).toBe(
            "/api/parking/lots/lot-id/zone-utilization",
        );
        expect(fetchMock.mock.calls[0][1].cache).toBe("no-store");
    });

    it("uses the quoted amount and idempotency key for both payment methods", async () => {
        const fetchMock = vi
            .fn()
            .mockImplementation(
                async () => new Response("{}", { status: 200 }),
            );
        vi.stubGlobal("fetch", fetchMock);
        await getParkingSessionFee("session-id");
        await payParkingFeeByWallet(
            "session-id",
            15000,
            "11111111-1111-4111-8111-111111111111",
        );
        await createVnPayParkingFeePayment(
            "session-id",
            15000,
            "22222222-2222-4222-8222-222222222222",
        );
        const wallet = fetchMock.mock.calls[1];
        const vnpay = fetchMock.mock.calls[2];
        expect(new URL(String(wallet[0])).pathname).toContain("/pay/wallet");
        expect(new URL(String(vnpay[0])).pathname).toContain("/pay/vnpay");
        expect(JSON.parse(wallet[1].body)).toEqual({
            expectedAmountDue: 15000,
        });
        expect(JSON.parse(vnpay[1].body)).toEqual({ expectedAmountDue: 15000 });
        expect(new Headers(wallet[1].headers).get("X-Idempotency-Key")).toBe(
            "11111111-1111-4111-8111-111111111111",
        );
    });

    it("uses account scoped vehicle and privacy routes", async () => {
        const fetchMock = vi
            .fn()
            .mockImplementation(
                async () => new Response("{}", { status: 200 }),
            );
        vi.stubGlobal("fetch", fetchMock);
        await getMyVehicle("vehicle-id");
        await updateMyVehicle("vehicle-id", "Family car", {
            brand: null,
            model: null,
            motorbikeKind: null,
            seats: null,
            fuelType: null,
            color: null,
            manufacturingYear: null,
            size: null,
            photoUrl: null,
        });
        await deleteMyProfileIdentity();
        await deleteCustomerMedia("media-id");
        expect(
            fetchMock.mock.calls.map(
                (call) =>
                    `${call[1]?.method ?? "GET"} ${new URL(String(call[0])).pathname}`,
            ),
        ).toEqual([
            "GET /api/identity/accounts/me/vehicles/vehicle-id",
            "PUT /api/identity/accounts/me/vehicles/vehicle-id",
            "DELETE /api/identity/accounts/me/profile/identity",
            "DELETE /api/identity/accounts/me/media/media-id",
        ]);
        expect(JSON.parse(fetchMock.mock.calls[1][1].body).nickName).toBe(
            "Family car",
        );
    });

    it("uses current account sessions and MFA actions", async () => {
        const fetchMock = vi
            .fn()
            .mockImplementation(
                async () => new Response("{}", { status: 200 }),
            );
        vi.stubGlobal("fetch", fetchMock);
        await getAccountSessions();
        await revokeAccountSession("session-id");
        await setupMfa();
        await confirmMfa("123456");
        await disableMfa("password", "123456");
        await regenerateMfaRecoveryCodes("123456");
        expect(
            fetchMock.mock.calls.map(
                (call) => new URL(String(call[0])).pathname,
            ),
        ).toEqual([
            "/api/identity/accounts/sessions",
            "/api/identity/accounts/sessions/session-id",
            "/api/identity/accounts/mfa/setup",
            "/api/identity/accounts/mfa/confirm",
            "/api/identity/accounts/mfa/disable",
            "/api/identity/accounts/mfa/recovery-codes",
        ]);
        expect(JSON.parse(fetchMock.mock.calls[4][1].body)).toEqual({
            password: "password",
            code: "123456",
        });
    });
});
