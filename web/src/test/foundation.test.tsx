import { render, screen } from "@testing-library/react";
import { useQueryClient } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";
import Home from "@/app/page";
import GuestRoute from "@/app/guest/page";
import NotFound from "@/app/not-found";
import { Providers } from "@/app/providers";
import { redirect } from "next/navigation";

vi.mock("next/navigation", () => ({ redirect: vi.fn() }));
vi.mock("next/font/google", () => ({
    Manrope: () => ({ className: "manrope" }),
}));

function QueryProbe() {
    const queryClient = useQueryClient();
    return <span>{queryClient ? "Query client ready" : "Unavailable"}</span>;
}

describe("application shell", () => {
    it("renders the ParkWise landing directly at the home route", () => {
        const page = Home();
        expect(page.type).toHaveProperty("name", "ParkWiseLanding");
    });

    it("redirects the legacy guest route to the new landing", () => {
        GuestRoute();
        expect(redirect).toHaveBeenCalledWith("/");
    });

    it("shows a safe not-found page", () => {
        render(<NotFound />);
        expect(
            screen.getByRole("heading", { name: "Page not found" }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: "Return home" }),
        ).toHaveAttribute("href", "/");
    });

    it("provides a TanStack Query client", () => {
        render(
            <Providers>
                <QueryProbe />
            </Providers>,
        );
        expect(screen.getByText("Query client ready")).toBeInTheDocument();
    });
});
