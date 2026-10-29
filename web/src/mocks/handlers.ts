import { http, HttpResponse } from "msw";
import { MOCK_LOTS, MOCK_SLOTS } from "./seed";

export const handlers = [
    http.get("/api/owner/lots", () => {
        return HttpResponse.json(MOCK_LOTS);
    }),
    http.get("/api/owner/lots/:id", ({ params }) => {
        const lot = MOCK_LOTS.find((l) => String(l.id) === String(params.id));
        if (!lot) return new HttpResponse(null, { status: 404 });
        return HttpResponse.json(lot);
    }),
    http.get("/api/owner/lots/:id/slots", () => {
        // In a real scenario, this would filter by lotId.
        // For now, MOCK_SLOTS simulates one lot.
        return HttpResponse.json(MOCK_SLOTS);
    }),
    // Owner Legal & Onboarding endpoints
    http.get("/api/identity/owner-document-requirements", ({ request }) => {
        const url = new URL(request.url);
        const ownerType = url.searchParams.get("ownerType") || "Enterprise";
        const docs =
            ownerType === "Individual"
                ? [
                      {
                          documentType: "PremisesRightProof",
                          name: "Giấy tờ chứng minh quyền sử dụng hoặc thuê mặt bằng bãi xe",
                          required: true,
                          acceptedContentTypes: [
                              "application/pdf",
                              "image/png",
                              "image/jpeg",
                          ],
                          maxSizeBytes: 10485760,
                      },
                      {
                          documentType: "AuthorizationLetter",
                          name: "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
                          required: false,
                          acceptedContentTypes: [
                              "application/pdf",
                              "image/png",
                              "image/jpeg",
                          ],
                          maxSizeBytes: 10485760,
                      },
                  ]
                : ownerType === "HouseholdBusiness"
                  ? [
                        {
                            documentType: "HouseholdBusinessRegistration",
                            name: "Giấy chứng nhận đăng ký hộ kinh doanh",
                            required: true,
                            acceptedContentTypes: [
                                "application/pdf",
                                "image/png",
                                "image/jpeg",
                            ],
                            maxSizeBytes: 10485760,
                        },
                        {
                            documentType: "PremisesRightProof",
                            name: "Giấy tờ chứng minh quyền sử dụng hoặc thuê mặt bằng bãi xe",
                            required: true,
                            acceptedContentTypes: [
                                "application/pdf",
                                "image/png",
                                "image/jpeg",
                            ],
                            maxSizeBytes: 10485760,
                        },
                        {
                            documentType: "AuthorizationLetter",
                            name: "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
                            required: false,
                            acceptedContentTypes: [
                                "application/pdf",
                                "image/png",
                                "image/jpeg",
                            ],
                            maxSizeBytes: 10485760,
                        },
                    ]
                  : [
                        {
                            documentType: "EnterpriseRegistration",
                            name: "Giấy chứng nhận đăng ký doanh nghiệp (GPĐKKD)",
                            required: true,
                            acceptedContentTypes: [
                                "application/pdf",
                                "image/png",
                                "image/jpeg",
                            ],
                            maxSizeBytes: 10485760,
                        },
                        {
                            documentType: "PremisesRightProof",
                            name: "Giấy tờ chứng minh quyền sử dụng hoặc thuê mặt bằng bãi xe",
                            required: true,
                            acceptedContentTypes: [
                                "application/pdf",
                                "image/png",
                                "image/jpeg",
                            ],
                            maxSizeBytes: 10485760,
                        },
                        {
                            documentType: "AuthorizationLetter",
                            name: "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
                            required: false,
                            acceptedContentTypes: [
                                "application/pdf",
                                "image/png",
                                "image/jpeg",
                            ],
                            maxSizeBytes: 10485760,
                        },
                    ];
        return HttpResponse.json({ ownerType, documents: docs });
    }),
    http.get("/api/identity/accounts/me/owner-application", () => {
        return HttpResponse.json({
            accountId: "demo-owner-id",
            email: "partner@parkwise.vn",
            status: "None",
            taxCode: null,
            businessName: null,
            rejectionReason: null,
            reviewedAtUtc: null,
            ownerType: "Enterprise",
        });
    }),
    http.post(
        "/api/identity/accounts/me/owner-application/apply",
        async ({ request }) => {
            const body = (await request.json().catch(() => ({}))) as Record<
                string,
                unknown
            >;
            return HttpResponse.json({
                accountId: "demo-owner-id",
                status: "Pending",
                message:
                    "Yêu cầu đăng ký xét duyệt chủ bãi đã được gửi thành công. Vui lòng chờ quản trị viên phê duyệt.",
            });
        },
    ),
];
