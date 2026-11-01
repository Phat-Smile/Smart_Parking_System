import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import {
    getAuthSessionSnapshot,
    clearAuthSession,
} from "@/features/auth/authSession";
import { loginParkWiseAccount } from "@/features/auth/loginParkWiseAccount";
import {
    applyForOwnerApplication,
    deleteOwnerDocument,
    getMyOwnerApplication,
    getMyOwnerDocuments,
    getOwnerDocumentRequirements,
    resubmitOwnerApplication,
    uploadOwnerDocument,
} from "./ownerLegalApi";

// Demo 1 (D01.3–D01.7): what the owner sees must be what Identity stored. A failed call is an error on
// screen, never a document or a submission that exists only in this browser.
function problem(status: number, errorCode: string) {
    return new Response(JSON.stringify({ status, title: "error", errorCode }), {
        status,
        headers: { "Content-Type": "application/problem+json" },
    });
}

function stubFetch(response: Response | Error) {
    const fetchMock =
        response instanceof Error
            ? vi.fn().mockRejectedValue(response)
            : vi.fn().mockResolvedValue(response);
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
}

beforeEach(() => {
    window.localStorage.clear();
});

afterEach(() => {
    vi.unstubAllGlobals();
    clearAuthSession();
});

describe("owner application calls surface backend errors", () => {
    it("a rejected upload is an error and nothing is kept in the browser", async () => {
        stubFetch(problem(401, "identity.unauthenticated"));
        const file = new File(["%PDF"], "giay-to.pdf", {
            type: "application/pdf",
        });

        await expect(
            uploadOwnerDocument("PremisesRightProof", file),
        ).rejects.toBeInstanceOf(ApiError);
        expect(window.localStorage.length).toBe(0);
    });

    it("a refused submission is an error, not a success message", async () => {
        stubFetch(problem(409, "identity.owner_application_incomplete"));

        await expect(
            applyForOwnerApplication({
                ownerType: "Individual",
                fullName: "A",
                idCardNumber: "079088001234",
            }),
        ).rejects.toBeInstanceOf(ApiError);
        // The citizen id number must not be stored in the browser.
        expect(window.localStorage.length).toBe(0);
    });

    it("a refused resubmission is an error", async () => {
        stubFetch(problem(409, "identity.owner_application_not_rejected"));
        await expect(resubmitOwnerApplication({})).rejects.toBeInstanceOf(
            ApiError,
        );
    });

    it("document list, checklist and delete failures are errors", async () => {
        stubFetch(problem(500, "identity.unexpected"));
        await expect(getMyOwnerDocuments()).rejects.toBeInstanceOf(ApiError);

        stubFetch(problem(500, "identity.unexpected"));
        await expect(
            getOwnerDocumentRequirements("Individual"),
        ).rejects.toBeInstanceOf(ApiError);

        stubFetch(problem(404, "identity.owner_document_not_found"));
        await expect(
            deleteOwnerDocument("PremisesRightProof"),
        ).rejects.toBeInstanceOf(ApiError);
    });

    it("no application yet is the empty state; no sign-in is an error", async () => {
        stubFetch(problem(404, "identity.owner_application_not_found"));
        const empty = await getMyOwnerApplication();
        expect(empty.status).toBe("None");
        expect(empty.accountId).not.toBe("demo-owner-id");

        stubFetch(problem(401, "identity.unauthenticated"));
        await expect(getMyOwnerApplication()).rejects.toBeInstanceOf(ApiError);
    });
});

describe("sign-in never invents a session", () => {
    it("an unreachable gateway is an error and no session is created", async () => {
        stubFetch(new TypeError("Failed to fetch"));

        await expect(
            loginParkWiseAccount("owner@parking.test", "Secret123!"),
        ).rejects.toBeInstanceOf(TypeError);
        expect(getAuthSessionSnapshot()).toBeNull();
    });
});

describe("owner application errors are explained in Vietnamese", () => {
    it("maps the backend codes the wizard can hit", async () => {
        const { describeOwnerApplicationError } =
            await import("./ownerLegalApi");
        const err = (status: number, code: string) =>
            new ApiError(status, {
                status,
                title: "t",
                errorCode: code,
            } as never);

        expect(
            describeOwnerApplicationError(
                err(400, "identity.owner_type_required"),
            ),
        ).toMatch(/loại chủ thể/);
        expect(
            describeOwnerApplicationError(
                err(409, "identity.owner_documents_incomplete"),
            ),
        ).toMatch(/thiếu giấy tờ/i);
        expect(
            describeOwnerApplicationError(err(401, "identity.unauthenticated")),
        ).toMatch(/đăng nhập/);
        expect(
            describeOwnerApplicationError(new TypeError("Failed to fetch")),
        ).toMatch(/máy chủ/);
    });
});
