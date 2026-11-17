import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { CustomerMap } from "./CustomerMap";

vi.mock("next/dynamic", () => ({
    default: () => () => <div data-testid="live-map" />,
}));
vi.mock("@/shared/config/env", () => ({
    getPublicMapboxToken: () => "test-token",
}));

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});

it("waits for results and viewport visibility before mounting the live map", () => {
    const observe = vi.fn();
    const disconnect = vi.fn();
    let notify!: (entries: Array<{ isIntersecting: boolean }>) => void;
    vi.stubGlobal(
        "IntersectionObserver",
        class {
            observe = observe;
            disconnect = disconnect;
            constructor(callback: typeof notify) {
                notify = callback;
            }
        },
    );
    const props = { selectedId: "", onSelect: vi.fn(), lots: [] };
    const view = render(<CustomerMap {...props} loadEnabled={false} />);
    expect(observe).not.toHaveBeenCalled();
    expect(screen.queryByTestId("live-map")).not.toBeInTheDocument();
    view.rerender(<CustomerMap {...props} loadEnabled />);
    expect(observe).toHaveBeenCalledTimes(1);
    act(() => notify([{ isIntersecting: false }]));
    expect(screen.queryByTestId("live-map")).not.toBeInTheDocument();
    act(() => notify([{ isIntersecting: true }]));
    expect(screen.getByTestId("live-map")).toBeVisible();
    expect(disconnect).toHaveBeenCalled();
});
