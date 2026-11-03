import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { AdminOwnerApplicationsPage } from "./AdminOwnerApplicationsPage";
import * as api from "./ownerApplicationsApi";

const ACCOUNT = "20000000-0000-0000-0000-000000000001";

// Demo 1 step D01.9: the admin opens an application, sees the files Identity stored and opens one with the
// admin token (the file route needs Authorization, so a plain link cannot work).
describe("AdminOwnerApplicationsPage documents", () => {
    beforeEach(() => {
        window.localStorage.clear();
        vi.spyOn(api, "getOwnerApplications").mockResolvedValue({
            items: [
                {
                    accountId: ACCOUNT,
                    email: "owner@x.vn",
                    fullName: "Chủ bãi A",
                    status: "Pending",
                    createdAtUtc: "2026-10-29T01:00:00Z",
                },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 20,
        });
        vi.spyOn(api, "getOwnerApplicationDocuments").mockResolvedValue({
            accountId: ACCOUNT,
            status: "Pending",
            ownerType: "Individual",
            isComplete: false,
            missingDocumentTypes: ["AuthorizationLetter"],
            documents: [
                {
                    id: "d1",
                    documentType: "PremisesRightProof",
                    originalFileName: "so-do.pdf",
                    contentType: "application/pdf",
                    sizeBytes: 1000,
                    uploadedAtUtc: "2026-10-29T01:05:00Z",
                },
            ],
        });
        vi.spyOn(api, "downloadOwnerApplicationDocument").mockResolvedValue(
            new Blob(["%PDF"], { type: "application/pdf" }),
        );
        Object.assign(URL, {
            createObjectURL: vi.fn(() => "blob:doc-1"),
            revokeObjectURL: vi.fn(),
        });
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it("lists the stored files of the opened application and opens one through the token-protected route", async () => {
        const user = userEvent.setup();
        render(
            <LanguageProvider>
                <AdminOwnerApplicationsPage />
            </LanguageProvider>,
        );

        await user.click(
            await screen.findByRole("button", { name: /Xem giấy tờ/i }),
        );

        await waitFor(() =>
            expect(api.getOwnerApplicationDocuments).toHaveBeenCalledWith(
                ACCOUNT,
            ),
        );
        expect(await screen.findByText(/so-do\.pdf/)).toBeInTheDocument();
        expect(screen.getByText(/AuthorizationLetter/)).toBeInTheDocument();

        await user.click(screen.getByRole("button", { name: /^👁/ }));

        await waitFor(() =>
            expect(api.downloadOwnerApplicationDocument).toHaveBeenCalledWith(
                ACCOUNT,
                "PremisesRightProof",
            ),
        );
        expect(await screen.findByTitle("so-do.pdf")).toHaveAttribute(
            "src",
            "blob:doc-1",
        );
    });

    it("displays VNeID Level 2 verified badge when applicant CCCD matches national database", async () => {
        vi.spyOn(api, "getOwnerApplications").mockResolvedValue({
            items: [
                {
                    accountId: ACCOUNT,
                    email: "owner.flow@demo.local",
                    fullName: "NGUYỄN VĂN AN",
                    idCardNumber: "079201234567",
                    status: "Pending",
                    createdAtUtc: "2026-10-29T01:00:00Z",
                },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 20,
        });

        const user = userEvent.setup();
        render(
            <LanguageProvider>
                <AdminOwnerApplicationsPage />
            </LanguageProvider>,
        );

        await user.click(
            await screen.findByRole("button", { name: /Xem giấy tờ/i }),
        );

        expect(
            await screen.findByTestId("admin-vneid-badge"),
        ).toBeInTheDocument();
        expect(screen.getByText(/VNeID Mức 2/i)).toBeInTheDocument();
    });
});
