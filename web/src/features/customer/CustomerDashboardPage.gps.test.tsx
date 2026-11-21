import {
    act,
    cleanup,
    fireEvent,
    render,
    screen,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerDashboardPage } from "./CustomerDashboardPage";

// Keep this regression scoped to the desktop dashboard's GPS behavior.
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: vi.fn() }),
}));
vi.mock("next/image", () => ({
    default: ({ alt }: { alt: string }) => <span aria-label={alt} />,
}));
vi.mock("next/link", () => ({
    default: ({
        href,
        children,
    }: {
        href: string;
        children: React.ReactNode;
    }) => <a href={href}>{children}</a>,
}));
vi.mock("@/features/auth/authSession", () => ({
    getAuthSessionSnapshot: () => null,
    subscribeAuthSession: () => () => {},
}));
vi.mock("./CustomerShell", () => ({
    CustomerHeader: () => null,
    CustomerFooter: () => null,
    CustomerIcon: () => null,
}));
vi.mock("./CustomerSessions", () => ({ ActiveSessionCard: () => null }));
vi.mock("./CustomerSearchDropdown", () => ({
    CustomerSearchDropdown: () => null,
}));
vi.mock("./mobile/useCustomerMobile", () => ({
    useCustomerMobile: () => false,
}));
vi.mock("./mobile/CustomerMobileHome", () => ({
    CustomerMobileHome: () => null,
}));
vi.mock("./booking/bookingApi", () => ({
    getMyReservations: vi.fn().mockResolvedValue([]),
}));
vi.mock("./i18n/customerLanguage", () => ({
    useCustomerLanguage: () => ({
        language: "en",
        copy: (english: string) => english,
    }),
}));

const originalGeolocation = Object.getOwnPropertyDescriptor(
    navigator,
    "geolocation",
);

function setGeolocation(value: unknown) {
    Object.defineProperty(navigator, "geolocation", {
        configurable: true,
        value,
    });
}

beforeEach(() => {
    vi.clearAllMocks();
});
afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    if (originalGeolocation) {
        Object.defineProperty(navigator, "geolocation", originalGeolocation);
    } else {
        Reflect.deleteProperty(navigator, "geolocation");
    }
});

describe("US-PRIV-001 dashboard GPS consent", () => {
    it("does not request browser GPS or weather on page mount", async () => {
        const getCurrentPosition = vi.fn();
        const weatherFetch = vi.fn();
        setGeolocation({ getCurrentPosition });
        vi.stubGlobal("fetch", weatherFetch);

        render(<CustomerDashboardPage fontClassName="" />);
        expect(
            screen.getByRole("button", { name: /Get local weather and time/i }),
        ).toBeVisible();
        await act(async () => Promise.resolve());

        expect(getCurrentPosition).not.toHaveBeenCalled();
        expect(weatherFetch).not.toHaveBeenCalled();
    });

    it("asks for GPS only when the user explicitly selects a location action", () => {
        const getCurrentPosition = vi.fn();
        setGeolocation({ getCurrentPosition });
        render(<CustomerDashboardPage fontClassName="" />);

        fireEvent.click(
            screen.getByRole("button", { name: /Get local weather and time/i }),
        );
        expect(getCurrentPosition).toHaveBeenCalledTimes(1);
    });

    it("keeps manual address search available when the browser denies GPS", () => {
        const getCurrentPosition = vi.fn(
            (_success: unknown, error: (err: { code: number }) => void) => {
                error({ code: 1 });
            },
        );
        setGeolocation({ getCurrentPosition });
        render(<CustomerDashboardPage fontClassName="" />);

        fireEvent.click(
            screen.getByRole("button", { name: /Use my current location/i }),
        );
        expect(getCurrentPosition).toHaveBeenCalledTimes(1);
        expect(screen.getByRole("status")).toHaveTextContent(
            /enter an address instead/i,
        );
        expect(
            screen.getByPlaceholderText("Enter an address or landmark"),
        ).toBeEnabled();
    });

    it("preserves address search if geolocation is unsupported", () => {
        setGeolocation(undefined);
        render(<CustomerDashboardPage fontClassName="" />);
        fireEvent.click(
            screen.getByRole("button", { name: /Use my current location/i }),
        );
        expect(screen.getByRole("status")).toHaveTextContent(
            /enter an address instead/i,
        );
        expect(
            screen.getByPlaceholderText("Enter an address or landmark"),
        ).toBeEnabled();
    });
});
