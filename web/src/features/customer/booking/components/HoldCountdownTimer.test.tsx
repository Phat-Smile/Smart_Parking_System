import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { HoldCountdownTimer } from "./HoldCountdownTimer";

describe("HoldCountdownTimer", () => {
    it("renders remaining minutes and seconds when active", () => {
        // 10 minutes in the future
        const expiresAt = new Date(
            Date.now() + 10 * 60 * 1000 + 30 * 1000,
        ).toISOString();

        render(<HoldCountdownTimer expiresAt={expiresAt} />);

        const timer = screen.getByRole("timer");
        expect(timer).toBeInTheDocument();
        expect(timer.textContent).toContain("Spot held for:");
        expect(timer.textContent).toMatch(/10:[23]\d/);
    });

    it("renders urgent warning style when under 3 minutes", () => {
        // 2 minutes in the future
        const expiresAt = new Date(Date.now() + 2 * 60 * 1000).toISOString();

        render(<HoldCountdownTimer expiresAt={expiresAt} />);

        const timer = screen.getByRole("timer");
        expect(timer.className).toContain("text-amber-400");
    });

    it("renders expired message when past expiresAt", () => {
        // 5 minutes in the past
        const expiresAt = new Date(Date.now() - 5 * 60 * 1000).toISOString();
        const onExpire = vi.fn();

        render(
            <HoldCountdownTimer expiresAt={expiresAt} onExpire={onExpire} />,
        );

        const status = screen.getByRole("status");
        expect(status.textContent).toContain("Your hold has expired");
    });
});
