import {
    act,
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerFindParkingPage } from "./CustomerFindParkingPage";
import {
    getApprovedParkingLot,
    getApprovedParkingLots,
    searchCustomerParkingLots,
} from "./parkingLotApi";
import type { CustomerLotSearchItem } from "./parkingLotApi";

vi.mock("./CustomerShell", () => ({
    CustomerHeader: () => null,
    CustomerFooter: () => null,
    CustomerIcon: () => null,
}));
vi.mock("./CustomerMap", () => ({ CustomerMap: () => null }));
vi.mock("./components/AiRecommendationPanel", () => ({
    AiRecommendationPanel: () => null,
}));
vi.mock("./components/ArrivalTimeScrubber", () => ({
    ArrivalTimeScrubber: () => null,
}));
vi.mock("./arrivalForecast", () => ({
    initialArrivalHour: () => 10,
    useArrivalForecast: () => ({ slots: [], loading: false }),
}));
vi.mock("@/shared/config/env", () => ({ getPublicMapboxToken: () => "" }));
vi.mock("./parkingLotApi", () => ({
    getApprovedParkingLots: vi.fn(),
    getApprovedParkingLot: vi.fn(),
    searchCustomerParkingLots: vi.fn(),
    searchNearbyParkingLots: vi.fn(),
}));

const lot = {
    id: "lot-1",
    name: "Catalog lot",
    address: "Test address",
    latitude: 10.77,
    longitude: 106.7,
    type: "Outdoor",
};
const metrics: CustomerLotSearchItem = {
    ...lot,
    startingPrice: 25000,
    availableSpaces: 12,
    distanceKm: null,
    drivingTimeMinutes: null,
    billingMode: "PerEntry",
    prebookable: true,
    compatibility: [],
    amenities: [],
};
const initialSearch = {
    destination: "",
    date: "2026-11-17",
    time: "10:00",
    vehicle: "any",
};

function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (reason: Error) => void;
    const promise = new Promise<T>((res, rej) => {
        resolve = res;
        reject = rej;
    });
    return { promise, resolve, reject };
}

beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getApprovedParkingLots).mockResolvedValue([lot]);
});
afterEach(cleanup);

describe("search results loading", () => {
    it("shows the catalog before slow metrics and uses one batch instead of per-lot details", async () => {
        const pending = deferred<CustomerLotSearchItem[]>();
        vi.mocked(searchCustomerParkingLots).mockReturnValue(pending.promise);
        render(
            <CustomerFindParkingPage
                fontClassName=""
                initialSearch={initialSearch}
            />,
        );
        expect(
            await screen.findByRole("heading", { name: lot.name }),
        ).toBeVisible();
        expect(screen.getAllByText("Loading...")).toHaveLength(2);
        expect(searchCustomerParkingLots).toHaveBeenCalledTimes(1);
        expect(getApprovedParkingLot).not.toHaveBeenCalled();
        await act(async () => pending.resolve([metrics]));
        expect(screen.getByText(/25.000/)).toBeVisible();
        expect(screen.getByText("12")).toBeVisible();
    });

    it("retains the catalog when optional metrics fail", async () => {
        const pending = deferred<CustomerLotSearchItem[]>();
        vi.mocked(searchCustomerParkingLots).mockReturnValue(pending.promise);
        render(
            <CustomerFindParkingPage
                fontClassName=""
                initialSearch={initialSearch}
            />,
        );
        await screen.findByRole("heading", { name: lot.name });
        await act(async () => pending.reject(new Error("Metrics unavailable")));
        expect(screen.getByRole("heading", { name: lot.name })).toBeVisible();
        expect(screen.queryByText("Loading...")).not.toBeInTheDocument();
        expect(screen.getAllByText("Unavailable")).toHaveLength(2);
    });

    it("does not overwrite a new EV search with a stale metrics response", async () => {
        const pending = deferred<CustomerLotSearchItem[]>();
        vi.mocked(searchCustomerParkingLots)
            .mockReturnValueOnce(pending.promise)
            .mockResolvedValue([
                { ...metrics, id: "ev-lot", name: "EV-only lot" },
            ]);
        render(
            <CustomerFindParkingPage
                fontClassName=""
                initialSearch={initialSearch}
            />,
        );
        await screen.findByRole("heading", { name: lot.name });
        const signal = vi.mocked(searchCustomerParkingLots).mock.calls[0][1];
        fireEvent.click(screen.getByLabelText(/EV charging/));
        await screen.findByRole("heading", { name: "EV-only lot" });
        expect(signal?.aborted).toBe(true);
        await act(async () => pending.resolve([metrics]));
        await waitFor(() =>
            expect(
                screen.getByRole("heading", { name: "EV-only lot" }),
            ).toBeVisible(),
        );
        expect(
            screen.queryByRole("heading", { name: lot.name }),
        ).not.toBeInTheDocument();
    });
});
