import { Page, Route } from "@playwright/test";

export const BASE_URL = process.env.BASE_URL || "http://localhost:3000";

export const DEMO_PASSWORD = process.env.DEMO_PASSWORD || "Demo@12345";

export const DEMO_ACCOUNTS = {
    admin: {
        email: "admin@demo.local",
        password: DEMO_PASSWORD,
        role: "SystemAdmin",
        fullName: "Quan Tri Vien Demo",
        id: "11111111-1111-1111-1111-111111111111",
    },
    owner: {
        email: "owner@demo.local",
        password: DEMO_PASSWORD,
        role: "ParkingLotOwner",
        fullName: "Chu Bai Demo",
        id: "22222222-2222-2222-2222-222222222222",
    },
    ownerPending: {
        email: "owner.pending@demo.local",
        password: DEMO_PASSWORD,
        role: "Customer",
        fullName: "Chu Bai Cho Duyet",
        id: "22222222-2222-2222-2222-222222222223",
    },
    customer: {
        email: "customer@demo.local",
        password: DEMO_PASSWORD,
        role: "Customer",
        fullName: "Khach Hang Demo",
        id: "33333333-3333-3333-3333-333333333333",
        vehicle: "51A-123.45",
        vehicleMoto: "59X1-234.56",
    },
    staff: {
        email: "staff@demo.local",
        password: DEMO_PASSWORD,
        role: "Staff",
        fullName: "Nhan Vien Demo",
        id: "44444444-4444-4444-4444-444444444444",
    },
};

export const DEMO_LOTS = {
    lotA: {
        id: "10000000-0000-0000-0000-000000000001",
        name: "Bai do Trung tam A",
        address: "01 Duong Mau, TP. Ho Chi Minh",
        latitude: 10.77653,
        longitude: 106.700981,
        type: "MultiStorey",
        status: "Approved",
        capacity: 120,
        availableSpaces: 45,
        walkInSpaces: 20,
        carPrice: 15000,
        motoPrice: 5000,
        hours: "24h",
    },
    lotPending: {
        id: "10000000-0000-0000-0000-000000000004",
        name: "Bai cho duyet",
        address: "04 Duong Mau, TP. Ho Chi Minh",
        latitude: 10.775,
        longitude: 106.71,
        type: "Outdoor",
        status: "Pending",
        capacity: 50,
        carPrice: 0,
        motoPrice: 0,
    },
};

export interface MockOptions {
    walkInFull?: boolean;
    plateMismatch?: boolean;
    unpaidFee?: boolean;
    exitWindowExpired?: boolean;
    mfaRequired?: boolean;
}

/**
 * Sets up mock API route handlers for isolated E2E demonstration tests when explicitly enabled.
 * Mặc định chạy với API thật (chỉ mock khi process.env.USE_MOCK_API === "true").
 * Đảm bảo test không giả tạo kết quả PASS khi backend không chạy (Tin 270).
 */
export async function setupDemoMockRoutes(
    page: Page,
    options: MockOptions = {},
) {
    if (process.env.USE_MOCK_API !== "true") {
        return;
    }

    // Auth Login
    await page.route("**/api/identity/accounts/login", async (route: Route) => {
        const body = route.request().postDataJSON() || {};
        const email = (body.email || "").toLowerCase();

        if (
            email === DEMO_ACCOUNTS.admin.email &&
            options.mfaRequired !== false
        ) {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    mfaRequired: true,
                    mfaToken: "mock-mfa-token-admin-12345",
                }),
            });
            return;
        }

        const matched = Object.values(DEMO_ACCOUNTS).find(
            (a) => a.email === email,
        );
        const role = matched ? matched.role : "Customer";
        const accountId = matched ? matched.id : "user-000";

        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                accessToken: `mock-jwt-token-for-${role}`,
                tokenType: "Bearer",
                expiresIn: 3600,
                refreshToken: "mock-refresh-token",
                mfaRequired: false,
                account: {
                    id: accountId,
                    email,
                    role,
                    status: "EmailVerified",
                    fullName: matched?.fullName || "User Demo",
                },
            }),
        });
    });

    // MFA Verification
    await page.route(
        "**/api/identity/accounts/login/mfa",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    accessToken: "mock-admin-mfa-token-systemadmin",
                    tokenType: "Bearer",
                    expiresIn: 3600,
                    refreshToken: "mock-refresh-token",
                    account: {
                        id: DEMO_ACCOUNTS.admin.id,
                        email: DEMO_ACCOUNTS.admin.email,
                        role: "SystemAdmin",
                        status: "EmailVerified",
                        fullName: DEMO_ACCOUNTS.admin.fullName,
                    },
                }),
            });
        },
    );

    // Profile & Me
    await page.route("**/api/identity/accounts/me", async (route: Route) => {
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                id: DEMO_ACCOUNTS.customer.id,
                email: DEMO_ACCOUNTS.customer.email,
                role: DEMO_ACCOUNTS.customer.role,
                status: "EmailVerified",
                fullName: DEMO_ACCOUNTS.customer.fullName,
            }),
        });
    });

    // Vehicles
    await page.route(
        "**/api/identity/accounts/me/vehicles*",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify([
                    {
                        id: "veh-001",
                        licensePlate: "51A-123.45",
                        vehicleType: "Car",
                        nickName: "Demo car",
                        isDefault: true,
                    },
                    {
                        id: "veh-002",
                        licensePlate: "59X1-234.56",
                        vehicleType: "Motorbike",
                        nickName: "Demo motorbike",
                        isDefault: false,
                    },
                ]),
            });
        },
    );

    // Wallet Balance
    await page.route("**/api/payment/wallets/me", async (route: Route) => {
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                walletId: "wal-001",
                balance: 200000,
                currency: "VND",
                status: "Active",
                gatewayMode: "Sandbox",
                limits: { minTopUp: 10000, maxTopUp: 10000000 },
                pendingTopUps: [],
            }),
        });
    });

    // Wallet Top-Up
    await page.route("**/api/payment/wallets/top-up", async (route: Route) => {
        const body = route.request().postDataJSON() || {};
        const amount = body.amount || 100000;
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                topupId: "topup-uuid-999",
                amount,
                paymentUrl: `${BASE_URL}/payment/vnpay-return?vnp_ResponseCode=00&vnp_TxnRef=topup-uuid-999&vnp_Amount=${amount * 100}`,
                status: "Pending",
            }),
        });
    });

    // Search Nearby Lots
    await page.route("**/api/parking/lots/nearby*", async (route: Route) => {
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify([
                {
                    id: DEMO_LOTS.lotA.id,
                    name: DEMO_LOTS.lotA.name,
                    address: DEMO_LOTS.lotA.address,
                    latitude: DEMO_LOTS.lotA.latitude,
                    longitude: DEMO_LOTS.lotA.longitude,
                    type: DEMO_LOTS.lotA.type,
                    distanceKm: 0.45,
                    pricePerHour: 30000,
                    availableSpaces: 15,
                },
            ]),
        });
    });

    // AI Recommendations
    await page.route(
        "**/api/parking/lots/recommendations*",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    recommendedLots: [
                        {
                            lotId: DEMO_LOTS.lotA.id,
                            lotName: DEMO_LOTS.lotA.name,
                            address: DEMO_LOTS.lotA.address,
                            latitude: DEMO_LOTS.lotA.latitude,
                            longitude: DEMO_LOTS.lotA.longitude,
                            distanceKm: 0.45,
                            estimatedCostVnd: 30000,
                            availabilityProbability: 0.95,
                            matchScore: 98.5,
                            recommendationReason:
                                "Gần nhất (0.45km), giá tối ưu 15.000đ/block và 95% còn chỗ lúc tới.",
                        },
                    ],
                }),
            });
        },
    );

    // Public Lot Details
    await page.route(
        `**/api/parking/lots/${DEMO_LOTS.lotA.id}`,
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    id: DEMO_LOTS.lotA.id,
                    name: DEMO_LOTS.lotA.name,
                    address: DEMO_LOTS.lotA.address,
                    latitude: DEMO_LOTS.lotA.latitude,
                    longitude: DEMO_LOTS.lotA.longitude,
                    type: DEMO_LOTS.lotA.type,
                    pricing: {
                        carPrice: 15000,
                        motorbikePrice: 5000,
                        carBillingMode: "Block",
                        blockMinutes: 30,
                        overnightFee: 50000,
                    },
                    availability: {
                        availableCarSpaces: 2,
                        totalCarCapacity: 2,
                        availableMotorbikeSpaces: 20,
                        totalMotorbikeCapacity: 20,
                        walkInCarSpaces: 1,
                    },
                    openingHours: {
                        is24Hours: true,
                        days: [0, 1, 2, 3, 4, 5, 6].map((d) => ({
                            dayOfWeek: d,
                            isClosed: false,
                            is24Hours: true,
                        })),
                    },
                    compatibility: ["Car", "Motorbike"],
                    amenities: ["CCTV", "Covered", "Staffed", "Security247"],
                }),
            });
        },
    );

    // Booking Holds
    await page.route("**/api/booking/holds", async (route: Route) => {
        const body = route.request().postDataJSON() || {};
        await route.fulfill({
            status: 201,
            contentType: "application/json",
            body: JSON.stringify({
                reservationId: "res-hold-12345678-0000",
                lotId: body.lotId || DEMO_LOTS.lotA.id,
                vehicleId: body.vehicleId || "veh-001",
                plateNumber: body.plateNumber || "51A-123.45",
                vehicleGroup: "Car",
                reservationType: "Scheduled",
                status: "PendingPayment",
                appointmentTime:
                    body.appointmentTime ||
                    new Date(Date.now() + 3600000).toISOString(),
                durationMinutes: body.durationMinutes || 120,
                tempHoldExpiresAt: new Date(Date.now() + 600000).toISOString(),
                windowStartAt: new Date(Date.now() + 3600000).toISOString(),
                windowEndAt: new Date(Date.now() + 10800000).toISOString(),
                prepaidAmount: 50000,
                priceSnapshot: "15000/30m",
                createdAt: new Date().toISOString(),
            }),
        });
    });

    // Booking Confirm
    await page.route(
        "**/api/booking/reservations/*/confirm",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    reservationId: "res-hold-12345678-0000",
                    status: "Confirmed",
                    lotId: DEMO_LOTS.lotA.id,
                    plateNumber: "51A-123.45",
                    bookingCode: "PW-BKG-2026",
                    qrToken: "QR-BOOKING-ACCESS-51A12345",
                    confirmedAt: new Date().toISOString(),
                }),
            });
        },
    );

    // Staff Assigned Lot
    await page.route(
        "**/api/parking/staff/assigned-lot",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    lotId: DEMO_LOTS.lotA.id,
                    lotName: DEMO_LOTS.lotA.name,
                    role: "Staff",
                    assignedAt: "2026-11-20T00:00:00Z",
                    status: "Active",
                }),
            });
        },
    );

    // ALPR Recognize Plate
    await page.route(
        "**/api/parking/sessions/recognize-plate",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    detectedPlate: "51A-123.45",
                    normalizedPlate: "51A12345",
                    confidence: 0.98,
                    vehicleType: "Car",
                    matchedBooking: true,
                    suggestedAction: "Admit",
                }),
            });
        },
    );

    // Open Parking Session
    await page.route("**/api/parking/sessions", async (route: Route) => {
        if (route.request().method() !== "POST") {
            await route.continue();
            return;
        }

        const body = route.request().postDataJSON() || {};
        if (options.walkInFull && body.plateNumber?.includes("51B")) {
            await route.fulfill({
                status: 409,
                contentType: "application/problem+json",
                body: JSON.stringify({
                    status: 409,
                    title: "Hết chỗ cho xe không đặt trước.",
                    errorCode: "parking.walk_in.full",
                    detail: "Hết chỗ cho xe không đặt trước.",
                }),
            });
            return;
        }

        await route.fulfill({
            status: 201,
            contentType: "application/json",
            body: JSON.stringify({
                sessionId: "sess-8888-0001",
                lotId: body.lotId || DEMO_LOTS.lotA.id,
                plateNumber: body.plateNumber || "51A-123.45",
                vehicleType: body.vehicleType || "Car",
                customerType:
                    body.plateNumber === "51A-123.45"
                        ? "Booking"
                        : "UnregisteredWalkIn",
                status: "Active",
                entryTime: new Date().toISOString(),
                isAccountLinked: true,
                qrToken: "QR-PARKING-SESSION-999888",
                slotCode: "A-01",
                zoneCode: "CAR-A",
                floorName: "Tầng 1",
            }),
        });
    });

    // Customer Active Sessions
    await page.route("**/api/parking/sessions/me", async (route: Route) => {
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify([
                {
                    sessionId: "sess-8888-0001",
                    lotId: DEMO_LOTS.lotA.id,
                    lotName: DEMO_LOTS.lotA.name,
                    plateNumber: "51A-123.45",
                    vehicleType: "Car",
                    entryTime: new Date(Date.now() - 3600000).toISOString(),
                    status: options.unpaidFee ? "Active" : "Paid",
                    slotCode: "A-01",
                    zoneCode: "CAR-A",
                    floorName: "Tầng 1",
                    qrToken: "QR-PARKING-SESSION-999888",
                    totalFeeVnd: 30000,
                    prepaidVnd: 0,
                    amountDueVnd: options.unpaidFee ? 30000 : 0,
                },
            ]),
        });
    });

    // Session By QR
    await page.route(
        "**/api/parking/sessions/by-qr/*",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    sessionId: "sess-8888-0001",
                    lotId: DEMO_LOTS.lotA.id,
                    plateNumber: "51A-123.45",
                    vehicleType: "Car",
                    entryTime: new Date(Date.now() - 3600000).toISOString(),
                    status: options.unpaidFee ? "Active" : "Paid",
                    paymentStatus: options.unpaidFee ? "Pending" : "Paid",
                    slotCode: "A-01",
                    qrToken: "QR-PARKING-SESSION-999888",
                    totalFeeVnd: 30000,
                    prepaidVnd: 0,
                    amountDueVnd: options.unpaidFee ? 30000 : 0,
                }),
            });
        },
    );

    // Session Fee
    await page.route(
        "**/api/payment/parking-sessions/*/fee",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    sessionId: "sess-8888-0001",
                    totalFee: 30000,
                    prepaidAmount: 0,
                    amountDue: options.unpaidFee ? 30000 : 0,
                    calculatedAtUtc: new Date().toISOString(),
                    customerType: "RegisteredWalkIn",
                    isPaid: !options.unpaidFee,
                }),
            });
        },
    );

    // Complete Session
    await page.route(
        "**/api/parking/sessions/*/complete",
        async (route: Route) => {
            const body = route.request().postDataJSON() || {};
            if (
                body.plateNumber &&
                !body.plateNumber.includes("51A-123.45") &&
                !body.plateNumber.includes("51A12345")
            ) {
                await route.fulfill({
                    status: 409,
                    contentType: "application/problem+json",
                    body: JSON.stringify({
                        status: 409,
                        title: "The plate or the QR does not match this parking session.",
                        errorCode: "parking.session.exit_not_verified",
                    }),
                });
                return;
            }

            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    sessionId: "sess-8888-0001",
                    status: "Completed",
                    completedAt: new Date().toISOString(),
                }),
            });
        },
    );

    // Reissue QR
    await page.route(
        "**/api/parking/sessions/*/reissue-qr",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    sessionId: "sess-8888-0001",
                    newQrToken: "QR-PARKING-SESSION-REISSUED-777",
                    reissuedAt: new Date().toISOString(),
                }),
            });
        },
    );

    // Owner Layout
    await page.route(
        "**/api/parking/owner/lots/*/layout",
        async (route: Route) => {
            if (route.request().method() === "PUT") {
                const body = route.request().postDataJSON() || {};
                await route.fulfill({
                    status: 200,
                    contentType: "application/json",
                    body: JSON.stringify({
                        version: (body.expectedVersion || 1) + 1,
                        floors: body.floors || [],
                    }),
                });
                return;
            }

            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    version: 1,
                    floors: [
                        {
                            name: "Tầng 1",
                            zones: [
                                {
                                    code: "CAR-A",
                                    vehicleGroup: "Car",
                                    capacity: 2,
                                    slots: [
                                        {
                                            slotCode: "A-01",
                                            effectiveStatus: "Available",
                                        },
                                        {
                                            slotCode: "A-02",
                                            effectiveStatus: "Available",
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                }),
            });
        },
    );

    // Owner Slot Maintenance (US-OWNER-003/008 / Tin 270)
    await page.route(
        "**/api/parking/owner/lots/*/slots/*/maintenance",
        async (route: Route) => {
            if (route.request().method() === "PUT") {
                const body = route.request().postDataJSON() || {};
                const url = route.request().url();
                const match = url.match(
                    /\/lots\/([^/]+)\/slots\/([^/]+)\/maintenance/,
                );
                const lotId = match ? match[1] : DEMO_LOTS.lotA.id;
                const slotId = match
                    ? match[2]
                    : "40000000-0000-0000-0000-000000000002";
                await route.fulfill({
                    status: 200,
                    contentType: "application/json",
                    body: JSON.stringify({
                        lotId,
                        slotId,
                        slotCode: "A-02",
                        effectiveStatus: body.underMaintenance
                            ? "UnderMaintenance"
                            : "Available",
                        overrideState: body.underMaintenance
                            ? "Maintenance"
                            : null,
                        updatedAtUtc: new Date().toISOString(),
                    }),
                });
                return;
            }
            await route.continue();
        },
    );

    // Owner Pricing
    await page.route(
        "**/api/parking/lots/*/pricing/*",
        async (route: Route) => {
            if (route.request().method() === "PUT") {
                await route.fulfill({
                    status: 200,
                    contentType: "application/json",
                    body: JSON.stringify({
                        message: "Cập nhật bảng giá thành công.",
                    }),
                });
                return;
            }

            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    price: 15000,
                    billingMode: "Block",
                    blockMinutes: 30,
                    overnightFee: 50000,
                }),
            });
        },
    );

    // Admin Owner Applications
    await page.route(
        "**/api/identity/admin/owner-applications",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify([
                    {
                        accountId: DEMO_ACCOUNTS.ownerPending.id,
                        email: DEMO_ACCOUNTS.ownerPending.email,
                        fullName: DEMO_ACCOUNTS.ownerPending.fullName,
                        ownerType: "Individual",
                        taxCode: "0900000002",
                        status: "Pending",
                        submittedAt: new Date().toISOString(),
                    },
                ]),
            });
        },
    );

    // Admin Owner Application Approve
    await page.route(
        "**/api/identity/admin/owner-applications/*/approve",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    accountId: DEMO_ACCOUNTS.ownerPending.id,
                    status: "Approved",
                    roleAssigned: "ParkingLotOwner",
                    decidedAt: new Date().toISOString(),
                }),
            });
        },
    );

    // Admin Pending Lots
    await page.route(
        "**/api/parking/admin/lots/pending*",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify([
                    {
                        id: DEMO_LOTS.lotPending.id,
                        name: DEMO_LOTS.lotPending.name,
                        address: DEMO_LOTS.lotPending.address,
                        approvalStatus: "Pending",
                    },
                ]),
            });
        },
    );

    // Admin Approve Lot
    await page.route(
        "**/api/parking/admin/lots/*/approve",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    lotId: DEMO_LOTS.lotPending.id,
                    approvalStatus: "Approved",
                    decidedAt: new Date().toISOString(),
                }),
            });
        },
    );

    // Admin Audit Logs
    await page.route(
        "**/api/payment/admin/audit-logs*",
        async (route: Route) => {
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({
                    page: 1,
                    pageSize: 20,
                    totalCount: 2,
                    items: [
                        {
                            id: "audit-002",
                            actionType: "ParkingLotApproved",
                            actorId: DEMO_ACCOUNTS.admin.id,
                            actorEmail: DEMO_ACCOUNTS.admin.email,
                            details:
                                "Approved parking lot 'Bai cho duyet' (10000000-0000-0000-0000-000000000004)",
                            createdAtUtc: new Date().toISOString(),
                        },
                        {
                            id: "audit-001",
                            actionType: "OwnerApplicationApproved",
                            actorId: DEMO_ACCOUNTS.admin.id,
                            actorEmail: DEMO_ACCOUNTS.admin.email,
                            details:
                                "Approved owner application for 'Chu Bai Cho Duyet'",
                            createdAtUtc: new Date(
                                Date.now() - 60000,
                            ).toISOString(),
                        },
                    ],
                }),
            });
        },
    );
}
