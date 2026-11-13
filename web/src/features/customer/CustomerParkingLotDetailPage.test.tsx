import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setCustomerLanguage } from "./i18n/customerLanguage";

// The page reads the arrival forecast through react-query.
function renderPage(ui: React.ReactElement) {
    return render(
        <QueryClientProvider client={new QueryClient()}>
            {ui}
        </QueryClientProvider>,
    );
}

const { mockGetApprovedParkingLot, mockGetMyVehicles, mockAuthSession } =
    vi.hoisted(() => ({
        mockGetApprovedParkingLot: vi.fn(),
        mockGetMyVehicles: vi.fn(),
        mockAuthSession: { value: null as null | { accessToken: string } },
    }));

vi.mock("next/image", () => ({
    default: ({
        priority: _priority,
        fill: _fill,
        ...props
    }: React.ImgHTMLAttributes<HTMLImageElement> & {
        priority?: boolean;
        fill?: boolean;
    }) => (
        // eslint-disable-next-line @next/next/no-img-element
        <img {...props} alt={props.alt ?? ""} />
    ),
}));

vi.mock("next/link", () => ({
    default: ({
        href,
        children,
        ...rest
    }: {
        href: string;
        children: React.ReactNode;
    }) => (
        <a href={href} {...rest}>
            {children}
        </a>
    ),
}));

vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("./CustomerShell", () => ({
    CustomerHeader: () => <div data-testid="customer-header" />,
    CustomerFooter: () => <div data-testid="customer-footer" />,
}));

vi.mock("./ParkingLayoutView", () => ({
    ParkingLayoutView: () => <div data-testid="parking-layout-view" />,
}));

vi.mock("@/features/parking/useParkingRealtime", () => ({
    useParkingRealtime: () => ({ isConnected: false }),
}));

vi.mock("@/features/auth/authSession", () => ({
    getAuthSessionSnapshot: () => mockAuthSession.value,
    subscribeAuthSession: () => () => {},
}));

vi.mock("./myVehiclesApi", () => ({
    getMyVehicles: mockGetMyVehicles,
}));

vi.mock("./booking/walletBalanceApi", () => ({
    getCustomerWalletBalance: vi.fn().mockResolvedValue(200000),
}));

vi.mock("./parkingLotApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./parkingLotApi")>();
    return {
        ...actual,
        getApprovedParkingLot: mockGetApprovedParkingLot,
    };
});

import { CustomerParkingLotDetailPage } from "./CustomerParkingLotDetailPage";

const LOT_ID = "11111111-2222-3333-4444-555555555555";

describe("CustomerParkingLotDetailPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        setCustomerLanguage("en");
        mockAuthSession.value = null;
        mockGetApprovedParkingLot.mockResolvedValue({
            id: LOT_ID,
            name: "Bãi Bitexco",
            address: "2 Hải Triều, Quận 1",
            latitude: 10.77,
            longitude: 106.7,
            type: "MultiStorey",
            openingHours: null,
            pricing: [
                {
                    vehicleType: "CAR",
                    billingMode: "Block",
                    blockMinutes: 60,
                    price: 30000,
                    overnightFee: 50000,
                },
                {
                    vehicleType: "MOTORBIKE",
                    billingMode: "PerEntry",
                    blockMinutes: null,
                    price: 5000,
                    overnightFee: 0,
                },
            ],
            availability: {
                car: { available: 7 },
                motorbike: { capacity: 40, available: 30 },
            },
        });
    });

    afterEach(() => {
        setCustomerLanguage("en");
    });

    it("shows hourly rate and estimated total from GET /api/parking/lots/{id} pricing in Book this parking card instead of Unavailable", async () => {
        renderPage(
            <CustomerParkingLotDetailPage
                id={LOT_ID}
                fontClassName="test-font"
            />,
        );

        await waitFor(() => {
            expect(
                screen.getByText(/Car 30\.000 ₫\/60 min/),
            ).toBeInTheDocument();
        });

        expect(screen.queryByText("Unavailable")).not.toBeInTheDocument();
        expect(screen.getByText("30.000 ₫")).toBeInTheDocument();
        expect(screen.getByText("60.000 ₫")).toBeInTheDocument();
    });

    it("shows estimated parking fee in Step 1 booking modal instead of Pricing unavailable when lot has pricing", async () => {
        mockAuthSession.value = { accessToken: "fake-token" };
        mockGetMyVehicles.mockResolvedValue({
            vehicles: [
                {
                    vehicleId: "veh-car-1",
                    licensePlate: "51F-123.45",
                    vehicleType: "Car",
                    brand: "Toyota",
                    model: "Vios",
                    color: "White",
                    isDefault: true,
                },
            ],
        });

        renderPage(
            <CustomerParkingLotDetailPage
                id={LOT_ID}
                fontClassName="test-font"
            />,
        );

        await waitFor(() => {
            expect(
                screen.getByText(/Car 30\.000 ₫\/60 min/),
            ).toBeInTheDocument();
        });
        await waitFor(() => {
            expect(screen.getByText(/51F-123\.45/)).toBeInTheDocument();
        });

        fireEvent.click(screen.getByRole("button", { name: /Book Now/i }));

        await waitFor(() => {
            expect(
                screen.getByText("Review booking details"),
            ).toBeInTheDocument();
        });

        expect(
            screen.queryByText("Pricing unavailable"),
        ).not.toBeInTheDocument();
        expect(screen.getByText("60.000 đ")).toBeInTheDocument();
    });
});
