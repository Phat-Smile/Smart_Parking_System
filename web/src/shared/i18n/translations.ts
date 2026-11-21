export type SupportedLanguage = "vi" | "en";

export interface TranslationDictionary {
    // General
    appName: string;
    switchLanguage: string;
    vietnamese: string;
    english: string;
    refresh: string;
    search: string;
    all: string;
    pending: string;
    approved: string;
    rejected: string;
    cancel: string;
    confirm: string;
    close: string;
    view: string;
    details: string;
    save: string;
    saving: string;
    loading: string;
    notSignedIn: string;
    signOut: string;
    status: string;
    actions: string;
    name: string;
    email: string;
    phone: string;
    createdAt: string;

    // Admin Sidebar & Roles
    adminPortal: string;
    adminDashboard: string;
    adminPendingLots: string;
    adminParkingLots: string;
    adminLotAssignments: string;
    adminOwnerApplications: string;
    adminUsers: string;
    systemAdmin: string;

    // Admin Parking Lots Page
    parkingLotsTitle: string;
    parkingLotsEyebrow: string;
    parkingLotsPendingTab: string;
    parkingLotsOwnersTab: string;
    parkingLotsAssignmentsTab: string;
    searchLotsPlaceholder: string;
    lookupLotId: string;
    lookupBtn: string;
    noLotsFound: string;
    lotNameHeader: string;
    lotOwnerHeader: string;
    lotCityHeader: string;
    lotVehiclesHeader: string;
    lotCapacityHeader: string;
    lotStatusHeader: string;
    approveLot: string;
    rejectLot: string;
    assignLot: string;
    viewLot: string;

    // Admin Dashboard
    adminDashEyebrow: string;
    adminDashTitle: string;
    adminDashReviewQueueBtn: string;
    adminDashWaitingReview: string;
    adminDashActiveLots: string;
    adminDashRejectedLots: string;
    adminDashApprovedCapacity: string;
    adminDashSlotsOpen: string;
    adminDashOldestPending: string;
    adminDashSeeAll: string;
    adminDashNothingToReview: string;
    adminDashRecentDecisions: string;
    adminDashNoDecisions: string;
    adminDashSubmittedAt: string;
    adminDashReviewBtn: string;

    // Admin Owner Applications Page
    ownerAppsTitle: string;
    ownerAppsEyebrow: string;
    ownerAppsBusinessName: string;
    ownerAppsTaxCode: string;
    ownerAppsDocuments: string;
    ownerAppsInspectDocs: string;
    ownerAppsVerifyAndApprove: string;
    ownerAppsReject: string;
    ownerAppsChecklistTitle: string;
    ownerAppsChecklistRule1: string;
    ownerAppsChecklistRule2: string;
    ownerAppsChecklistRule3: string;
    ownerAppsChecklistRule4: string;
    ownerAppsChecklistWarning: string;
    ownerAppsConfirmApprove: string;
    ownerAppsRejectReasonLabel: string;
    ownerAppsRejectReasonPlaceholder: string;
    ownerAppsEmpty: string;
    ownerAppsApplicant: string;
    ownerAppsBusiness: string;
    ownerAppsLegalDocs: string;
    ownerAppsSubmittedAt: string;
    ownerAppsSearchPlaceholder: string;
    ownerAppsInspectDocsModalTitle: string;
    ownerAppsApproveModalTitle: string;
    ownerAppsRepresentative: string;
    ownerAppsIdCardNumber: string;
    ownerAppsRegisteredAddress: string;
    ownerAppsAttachedDocs: string;
    ownerAppsNoDocsWarning: string;
    ownerAppsSelectAllChecklist: string;
    ownerAppsDeselectAllChecklist: string;
    ownerAppsRejectMissingDocsBtn: string;
    ownerAppsRejectConfirmTitle: string;
    ownerAppsRejectConfirmDesc: string;
    ownerAppsConfirmRejectBtn: string;
    ownerAppsOpenNewTab: string;
    ownerAppsPendingEmpty: string;
    ownerAppsFilterEmpty: string;
    ownerAppsLoading: string;
    ownerAppsNotUpdated: string;
    ownerAppsMissingDocsAlert: string;

    // Admin Staff Assignments Page
    assignmentsTitle: string;
    assignmentsEyebrow: string;
    selectLotTitle: string;
    staffAtLotTitle: string;
    staffAtLotSubtitle: string;
    assignStaffTitle: string;
    selectStaffLabel: string;
    selectStaffDefault: string;
    assignRoleLabel: string;
    roleStaff: string;
    roleOwner: string;
    assignBtn: string;
    removeStaffBtn: string;
    noStaffAssigned: string;
    assignmentsColStaff: string;
    assignmentsColContact: string;
    assignmentsColRole: string;
    assignmentsColShift: string;
    assignmentsSelectShift: string;
    shiftMorning: string;
    shiftAfternoon: string;
    shiftNight: string;
    shiftAllDay: string;
    assignmentsLoadingLots: string;
    assignmentsLoadingAssignments: string;
    assignmentsAssigning: string;
    assignmentsRemoveConfirm: string;
    assignmentsBackToLots: string;
    assignmentsOwnerAppsLink: string;
    assignmentsDefaultStaffName: string;

    // Admin Users Page
    adminUsersEyebrow: string;
    adminUsersTitle: string;
    adminUsersSearchPlaceholder: string;
    adminUsersAllRoles: string;
    adminUsersAllStatuses: string;
    adminUsersColAccount: string;
    adminUsersColRole: string;
    adminUsersColStatus: string;
    adminUsersColCreated: string;
    adminUsersColActions: string;
    adminUsersSuspend: string;
    adminUsersActivate: string;
    adminUsersUnlock: string;
    adminUsersChangeRole: string;
    adminUsersEmpty: string;
    adminUsersPrevious: string;
    adminUsersNext: string;
    adminUsersPageOf: string;

    // Staff Portal & Sidebar
    staffStation: string;
    staffOperations: string;
    staffDashboard: string;
    staffEntryGate: string;
    staffExitGate: string;
    staffSessions: string;
    staffShift: string;
    staffOperator: string;
    staffAssignedLot: string;
    staffLiveCapacity: string;
    staffCarSlots: string;
    staffMotorbikeSlots: string;
    staffCheckinTitle: string;
    staffCheckinSubtitle: string;
    staffPlateNumber: string;
    staffVehicleType: string;
    staffCar: string;
    staffMotorbike: string;
    staffCreateSessionBtn: string;
    staffCheckoutTitle: string;
    staffScanQr: string;
    staffCalculateFee: string;
    staffPaymentQr: string;
    staffCompleteSessionBtn: string;

    // Staff Sessions Management
    sessionsTitle: string;
    sessionsSubtitle: string;
    sessionsSearchPlaceholder: string;
    sessionsAllTab: string;
    sessionsGuestTab: string;
    sessionsRegisteredTab: string;
    sessionsLicensePlate: string;
    sessionsCategory: string;
    sessionsVehicle: string;
    sessionsSpot: string;
    sessionsEntryTime: string;
    sessionsStatus: string;
    sessionsPaymentStatus: string;
    sessionsQr: string;
    sessionsActions: string;
    sessionsEmpty: string;
    sessionsGuest: string;
    sessionsRegistered: string;
    sessionsDue: string;
    sessionsViewQr: string;
    sessionsCancelEntry: string;
    sessionsCancelTitle: string;
    sessionsCancelConfirmText: string;
    sessionsCancelReasonLabel: string;
    sessionsCancelReasonPlaceholder: string;
    sessionsGoBack: string;
    sessionsConfirmCancel: string;
    sessionsCancelling: string;

    // Status translations
    statusActive: string;
    statusPaid: string;
    statusPending: string;
    statusCompleted: string;
    statusCancelled: string;

    // Staff Dashboard
    dashTitle: string;
    dashSubtitle: string;
    dashProcessEntry: string;
    dashProcessExit: string;
    dashOccupancy: string;
    dashFull: string;
    dashSlots: string;
    dashCarAvailable: string;
    dashMotorbikeAvailable: string;
    dashActiveSessions: string;
    dashActiveHint: string;
    dashViewSessions: string;
    dashNeedsVerif: string;
    dashNeedsVerifHint: string;
    dashResolveNow: string;
    dashGateHealth: string;
    dashGateSynced: string;
    dashLatency: string;
    dashEntryActivity: string;
    dashExitActivity: string;
    dashLiveFeed: string;
    dashNoEntry: string;
    dashNoExit: string;

    // Staff QR & UI
    qrTicketTitle: string;
    qrTicketDesc: string;
    qrCopy: string;
    qrCopied: string;
    qrDone: string;

    // Gate & Barrier
    barrierStatus: string;
    gateIn: string;
    gateOut: string;
    gateOnline: string;

    // Staff Check-in (Entry)
    entryPageTitle: string;
    entryPageSubtitle: string;
    entryClearBtn: string;
    entrySimulateLabel: string;
    entryPresetRegCar: string;
    entryPresetGuestBike: string;
    entryPresetLowConf: string;
    entryCameraTitle: string;
    entryPlateVerifTitle: string;
    entryConfidence: string;
    entryLowConfWarning: string;
    entryHighConfHint: string;
    entryPlateLabel: string;
    entryConfirmPlateBtn: string;
    entryVehicleTypeLabel: string;
    entryCustCategoryLabel: string;
    entryRegOption: string;
    entryGuestOption: string;
    entryFlowRegTitle: string;
    entryFlowGuestTitle: string;
    entryStep1: string;
    entryStep2: string;
    entryStep3Reg: string;
    entryStep3Guest: string;
    entryStep4: string;
    entryStep5: string;
    entrySummaryPlate: string;
    entrySummaryType: string;
    entrySummaryCap: string;
    entrySummaryAccount: string;
    entryAccountLinked: string;
    entryAccountGuest: string;
    entryQrTokenLabel: string;
    entryMotorbikeSlotsLeft: string;
    entryCarSlotsLeft: string;
    entryBarrierRaised: string;
    entryBarrierHint: string;
    entryProcessing: string;
    entryBtnGuest: string;
    entryBtnReg: string;
    entryViewQrBtn: string;
    entryConfirmParkedBtn: string;
    entryParkedConfirmed: string;
    entryNextVehicleBtn: string;

    // Staff Check-out (Exit)
    exitPageTitle: string;
    exitPageSubtitle: string;
    exitClearBtn: string;
    exitSimulateLabel: string;
    exitPresetGuestUnpaid: string;
    exitPresetRegPrepaid: string;
    exitPresetMismatch: string;
    exitCameraTitle: string;
    exitQrVerifTitle: string;
    exitScannerReady: string;
    exitScanQrLabel: string;
    exitVerifyQrBtn: string;
    exitVerifying: string;
    exitCameraPlateLabel: string;
    exitChecklistTitle: string;
    exitAuthorized: string;
    exitDenied: string;
    exitCheckQr: string;
    exitCheckQrScanned: string;
    exitCheckQrNone: string;
    exitCheckPlate: string;
    exitCheckPlateCam: string;
    exitCheckPlateSession: string;
    exitCheckSession: string;
    exitCheckSessionActive: string;
    exitCheckSessionClosed: string;
    exitCheckPayment: string;
    exitCheckPaymentSettled: string;
    exitCheckPaymentPending: string;
    exitStatusPassed: string;
    exitStatusFailed: string;
    exitStatusWarning: string;
    exitFeeTitle: string;
    exitTotalFee: string;
    exitPrepaidWallet: string;
    exitAmountDue: string;
    exitCollectWarning: string;
    exitCollectCash: string;
    exitVnPayQr: string;
    exitBarrierRaised: string;
    exitBarrierHint: string;
    exitAuthorizeBtn: string;
    exitBlockedBtn: string;
    exitDoneBtn: string;
    exitCashModalTitle: string;
    exitVnPayModalTitle: string;
    exitModalSub: string;
    exitPlateLabel: string;
    exitSpotLabel: string;
    exitAmountDueLabel: string;
    exitCashReceivedLabel: string;
    exitCashHint: string;
    exitChangeDue: string;
    exitExactCash: string;
    exitConfirmCashPayment: string;
    exitSimulateVnPaySuccess: string;
    exitClose: string;

    // Staff Verification Queue
    verifTitle: string;
    verifSubtitle: string;
    verifColSpot: string;
    verifColProblem: string;
    verifColDetectedPlate: string;
    verifColConf: string;
    verifColState: string;
    verifColReportedAt: string;
    verifColAction: string;
    verifEmpty: string;
    verifApproveBarrier: string;
    verifDismiss: string;
}

export const translations: Record<SupportedLanguage, TranslationDictionary> = {
    vi: {
        appName: "ParkWise",
        switchLanguage: "Đổi ngôn ngữ",
        vietnamese: "Tiếng Việt",
        english: "English",
        refresh: "Làm mới",
        search: "Tìm kiếm...",
        all: "Tất cả",
        pending: "Chờ duyệt",
        approved: "Đã duyệt",
        rejected: "Từ chối",
        cancel: "Hủy",
        confirm: "Xác nhận",
        close: "Đóng",
        view: "Xem",
        details: "Chi tiết",
        save: "Lưu",
        saving: "Đang lưu...",
        loading: "Đang tải...",
        notSignedIn: "Chưa đăng nhập",
        signOut: "Đăng xuất",
        status: "Trạng thái",
        actions: "Thao tác",
        name: "Họ và tên",
        email: "Email",
        phone: "Số điện thoại",
        createdAt: "Ngày tạo",

        // Admin
        adminPortal: "Hệ thống Quản trị",
        adminDashboard: "Bảng điều khiển",
        adminPendingLots: "Bãi xe chờ duyệt",
        adminParkingLots: "Danh sách bãi xe",
        adminLotAssignments: "Phân công nhân viên",
        adminOwnerApplications: "Duyệt hồ sơ Chủ bãi",
        adminUsers: "Người dùng & Đối tác",
        systemAdmin: "Quản trị viên hệ thống",

        // Admin Parking Lots
        parkingLotsTitle: "Quản trị bãi đỗ xe",
        parkingLotsEyebrow: "Mạng lưới bãi xe",
        parkingLotsPendingTab: "Bãi xe chờ duyệt",
        parkingLotsOwnersTab: "Duyệt hồ sơ Chủ bãi",
        parkingLotsAssignmentsTab: "Phân công nhân viên",
        searchLotsPlaceholder: "Tìm theo tên bãi, chủ bãi, địa chỉ...",
        lookupLotId: "Nhập mã bãi (GUID) để tra cứu...",
        lookupBtn: "Mở",
        noLotsFound: "Không tìm thấy bãi đỗ xe nào phù hợp.",
        lotNameHeader: "Tên bãi đỗ xe",
        lotOwnerHeader: "Chủ bãi",
        lotCityHeader: "Địa chỉ / Thành phố",
        lotVehiclesHeader: "Loại xe",
        lotCapacityHeader: "Sức chứa",
        lotStatusHeader: "Trạng thái",
        approveLot: "Duyệt",
        rejectLot: "Từ chối",
        assignLot: "Phân công",
        viewLot: "Xem",

        // Admin Dashboard
        adminDashEyebrow: "Tổng quan hệ thống",
        adminDashTitle: "Bảng điều khiển",
        adminDashReviewQueueBtn: "Hàng đợi duyệt",
        adminDashWaitingReview: "Đang chờ duyệt",
        adminDashActiveLots: "Bãi xe đang hoạt động",
        adminDashRejectedLots: "Hồ sơ bị từ chối",
        adminDashApprovedCapacity: "Sức chứa đã duyệt",
        adminDashSlotsOpen: "chỗ đỗ sẵn sàng phục vụ",
        adminDashOldestPending: "Chờ duyệt lâu nhất",
        adminDashSeeAll: "Xem tất cả",
        adminDashNothingToReview: "Không có hồ sơ nào cần duyệt. Tuyệt vời!",
        adminDashRecentDecisions: "Quyết định gần đây",
        adminDashNoDecisions: "Chưa có quyết định nào.",
        adminDashSubmittedAt: "gửi lúc",
        adminDashReviewBtn: "Duyệt",

        // Admin Owner Applications
        ownerAppsTitle: "Duyệt hồ sơ Chủ bãi",
        ownerAppsEyebrow: "Xác thực đối tác",
        ownerAppsBusinessName: "Doanh nghiệp / MST",
        ownerAppsTaxCode: "Mã số thuế",
        ownerAppsDocuments: "Giấy tờ pháp lý",
        ownerAppsInspectDocs: "Xem giấy tờ",
        ownerAppsVerifyAndApprove: "Kiểm tra & Duyệt",
        ownerAppsReject: "Từ chối",
        ownerAppsChecklistTitle:
            "Checklist kiểm tra điều kiện pháp lý (Yêu cầu đủ 4 mục):",
        ownerAppsChecklistRule1:
            "1. Giấy phép ĐKKD & Mã số thuế hợp lệ trên cổng quốc gia",
        ownerAppsChecklistRule2:
            "2. Căn cước công dân người đại diện pháp luật hợp lệ, rõ nét",
        ownerAppsChecklistRule3:
            "3. Giấy chứng nhận thẩm duyệt thiết kế / đủ điều kiện PCCC",
        ownerAppsChecklistRule4:
            "4. Hợp đồng thuê mặt bằng hoặc quyền sử dụng bãi xe hợp pháp",
        ownerAppsChecklistWarning:
            "Chỉ có thể duyệt khi kiểm tra và đánh dấu đủ cả 4 mục giấy tờ trên.",
        ownerAppsConfirmApprove: "Xác nhận đủ giấy tờ & Duyệt",
        ownerAppsRejectReasonLabel: "Lý do từ chối (bắt buộc):",
        ownerAppsRejectReasonPlaceholder:
            "Ví dụ: Hồ sơ chưa đủ điều kiện: Thiếu giấy phép PCCC và Hợp đồng bãi đỗ.",
        ownerAppsEmpty: "Không có hồ sơ chủ bãi nào phù hợp.",
        ownerAppsApplicant: "Họ và tên / Tài khoản",
        ownerAppsBusiness: "Doanh nghiệp / MST",
        ownerAppsLegalDocs: "Giấy tờ pháp lý",
        ownerAppsSubmittedAt: "Ngày nộp",
        ownerAppsSearchPlaceholder:
            "Tìm tên, email, MST, số điện thoại hoặc doanh nghiệp...",
        ownerAppsInspectDocsModalTitle: "Hồ sơ & Giấy tờ pháp lý Chủ bãi",
        ownerAppsApproveModalTitle:
            "Thẩm định giấy tờ & Phê duyệt hồ sơ Chủ bãi",
        ownerAppsRepresentative: "Người đại diện:",
        ownerAppsIdCardNumber: "Số CCCD:",
        ownerAppsRegisteredAddress: "Địa chỉ ĐKKD:",
        ownerAppsAttachedDocs: "Tài liệu đính kèm",
        ownerAppsNoDocsWarning: "Đối tác chưa tải lên tài liệu pháp lý nào.",
        ownerAppsSelectAllChecklist: "Chọn tất cả",
        ownerAppsDeselectAllChecklist: "Bỏ chọn tất cả",
        ownerAppsRejectMissingDocsBtn: "Hồ sơ thiếu giấy tờ ➔ Từ chối",
        ownerAppsRejectConfirmTitle: "Từ chối hồ sơ Chủ bãi?",
        ownerAppsRejectConfirmDesc:
            "Vui lòng nhập lý do từ chối để thông báo cho đối tác",
        ownerAppsConfirmRejectBtn: "Xác nhận từ chối",
        ownerAppsOpenNewTab: "Mở tab mới ↗",
        ownerAppsPendingEmpty: "Không có hồ sơ chủ bãi nào đang chờ duyệt.",
        ownerAppsFilterEmpty: "Không tìm thấy hồ sơ phù hợp với bộ lọc.",
        ownerAppsLoading: "Đang tải danh sách hồ sơ chủ bãi...",
        ownerAppsNotUpdated: "Chưa cập nhật",
        ownerAppsMissingDocsAlert:
            "Cần xác nhận đủ 4 mục giấy tờ pháp lý hợp lệ trước khi duyệt hồ sơ.",

        // Admin Staff Assignments
        assignmentsTitle: "Phân công Nhân viên trực bãi",
        assignmentsEyebrow: "Quản trị vận hành",
        selectLotTitle: "Chọn bãi đỗ xe",
        staffAtLotTitle: "Nhân viên trực tại",
        staffAtLotSubtitle:
            "Quản lý danh sách nhân sự được phân quyền kiểm soát cổng vào / cổng ra (Staff Portal) tại bãi này.",
        assignStaffTitle: "Phân công thêm nhân sự vào bãi",
        selectStaffLabel: "Chọn nhân viên:",
        selectStaffDefault: "-- Chọn nhân viên từ hệ thống --",
        assignRoleLabel: "Vai trò tại bãi:",
        roleStaff: "Nhân viên trực (Staff)",
        roleOwner: "Chủ bãi (Owner)",
        assignBtn: "Gán vào bãi",
        removeStaffBtn: "Gỡ khỏi bãi",
        noStaffAssigned:
            "Chưa có nhân viên nào được phân công vào bãi đỗ xe này. Hãy thêm nhân viên ở biểu mẫu bên dưới.",
        assignmentsColStaff: "Nhân viên / Họ tên",
        assignmentsColContact: "Email & Số điện thoại",
        assignmentsColRole: "Vai trò tại bãi",
        assignmentsColShift: "Ca trực",
        assignmentsSelectShift: "Ca trực phân công",
        shiftMorning: "Ca 1 - Sáng (06:00 - 14:00)",
        shiftAfternoon: "Ca 2 - Chiều (14:00 - 22:00)",
        shiftNight: "Ca 3 - Đêm (22:00 - 06:00)",
        shiftAllDay: "Cả ngày / 24/7 (00:00 - 23:59)",
        assignmentsLoadingLots: "Đang tải danh sách bãi...",
        assignmentsLoadingAssignments: "Đang tải danh sách phân công...",
        assignmentsAssigning: "Đang gán...",
        assignmentsRemoveConfirm:
            "Bạn có chắc muốn gỡ {name} khỏi bãi đỗ xe này?",
        assignmentsBackToLots: "← Danh sách bãi xe",
        assignmentsOwnerAppsLink: "Duyệt hồ sơ Chủ bãi",
        assignmentsDefaultStaffName: "Nhân viên trực bãi",

        // Admin Users Page
        adminUsersEyebrow: "Định danh & Tài khoản",
        adminUsersTitle: "Người dùng & Đối tác",
        adminUsersSearchPlaceholder: "Tìm theo email hoặc họ tên",
        adminUsersAllRoles: "Tất cả vai trò",
        adminUsersAllStatuses: "Tất cả trạng thái",
        adminUsersColAccount: "Tài khoản",
        adminUsersColRole: "Vai trò",
        adminUsersColStatus: "Trạng thái",
        adminUsersColCreated: "Ngày tạo",
        adminUsersColActions: "Thao tác",
        adminUsersSuspend: "Khóa",
        adminUsersActivate: "Kích hoạt",
        adminUsersUnlock: "Mở khóa",
        adminUsersChangeRole: "Đổi vai trò",
        adminUsersEmpty: "Không tìm thấy tài khoản nào.",
        adminUsersPrevious: "Trang trước",
        adminUsersNext: "Trang sau",
        adminUsersPageOf: "Trang",

        // Staff Portal
        staffStation: "TRẠM NHÂN VIÊN",
        staffOperations: "CHỨC NĂNG VẬN HÀNH",
        staffDashboard: "Tổng quan ca trực",
        staffEntryGate: "Cổng vào (Check-in)",
        staffExitGate: "Cổng ra (Check-out)",
        staffSessions: "Lịch sử phiên xe",
        staffShift: "Ca #2 · Sáng",
        staffOperator: "Nhân viên trực",
        staffAssignedLot: "Bãi đỗ đang trực",
        staffLiveCapacity: "Tình trạng chỗ trống",
        staffCarSlots: "Chỗ trống Ô tô",
        staffMotorbikeSlots: "Chỗ trống Xe máy",
        staffCheckinTitle: "Kiểm soát Cổng vào",
        staffCheckinSubtitle: "Ghi nhận xe vào bãi và cấp vé mã QR điện tử",
        staffPlateNumber: "Biển số xe",
        staffVehicleType: "Loại phương tiện",
        staffCar: "Ô tô",
        staffMotorbike: "Xe máy",
        staffCreateSessionBtn: "Tạo phiên & Cấp vé QR",
        staffCheckoutTitle: "Kiểm soát Cổng ra",
        staffScanQr: "Quét hoặc nhập mã vé QR",
        staffCalculateFee: "Tính tiền cước gửi xe",
        staffPaymentQr: "Mã QR thanh toán VietQR",
        staffCompleteSessionBtn: "Xác nhận hoàn tất & Cho xe ra",

        // Staff Sessions Management
        sessionsTitle: "Lịch sử phiên xe",
        sessionsSubtitle:
            "Theo dõi, tra cứu và quản lý toàn bộ phiên gửi xe tại bãi.",
        sessionsSearchPlaceholder: "Tìm biển số xe, mã phiên hoặc vị trí đỗ...",
        sessionsAllTab: "Tất cả phiên xe",
        sessionsGuestTab: "Khách vãng lai",
        sessionsRegisteredTab: "Khách đăng ký App",
        sessionsLicensePlate: "Biển số xe",
        sessionsCategory: "Phân loại",
        sessionsVehicle: "Loại xe",
        sessionsSpot: "Vị trí",
        sessionsEntryTime: "Giờ vào",
        sessionsStatus: "Trạng thái",
        sessionsPaymentStatus: "Thanh toán",
        sessionsQr: "Vé mã QR",
        sessionsActions: "Thao tác",
        sessionsEmpty: "Không có phiên đỗ xe nào phù hợp với tìm kiếm của bạn.",
        sessionsGuest: "Vãng lai",
        sessionsRegistered: "Đã đăng ký",
        sessionsDue: "Cần thu",
        sessionsViewQr: "Vé QR",
        sessionsCancelEntry: "Hủy phiên",
        sessionsCancelTitle: "Hủy phiên gửi xe",
        sessionsCancelConfirmText: "Bạn có chắc muốn hủy phiên xe cho biển số",
        sessionsCancelReasonLabel: "Lý do hủy phiên:",
        sessionsCancelReasonPlaceholder:
            "Nhập lý do hủy (ví dụ: Xe quay đầu không vào bãi)...",
        sessionsGoBack: "Quay lại",
        sessionsConfirmCancel: "Xác nhận hủy",
        sessionsCancelling: "Đang xử lý hủy...",

        // Status
        statusActive: "Đang đỗ",
        statusPaid: "Đã thanh toán",
        statusPending: "Chờ thanh toán",
        statusCompleted: "Đã hoàn tất",
        statusCancelled: "Đã hủy",

        // Staff Dashboard
        dashTitle: "Tổng quan ca trực",
        dashSubtitle:
            "Theo dõi lưu lượng xe tại cổng, công suất chỗ đỗ thời gian thực và các cảnh báo.",
        dashProcessEntry: "Tiếp nhận xe vào cổng",
        dashProcessExit: "Xử lý xe ra cổng",
        dashOccupancy: "Công suất bãi đỗ hiện tại",
        dashFull: "ĐẦY",
        dashSlots: "chỗ",
        dashCarAvailable: "Chỗ trống Ô tô:",
        dashMotorbikeAvailable: "Chỗ trống Xe máy:",
        dashActiveSessions: "Phiên xe đang gửi",
        dashActiveHint: "Phương tiện đang đỗ trong bãi",
        dashViewSessions: "Xem tất cả phiên xe →",
        dashNeedsVerif: "Cần xác thực OCR",
        dashNeedsVerifHint: "Cảnh báo OCR biển số & rào chắn",
        dashResolveNow: "Xử lý sự cố ngay →",
        dashGateHealth: "Tình trạng Barrier",
        dashGateSynced: "Cổng 01 & Cổng 02 đồng bộ",
        dashLatency: "Độ trễ",
        dashEntryActivity: "Hoạt động vào (Cổng In)",
        dashExitActivity: "Hoạt động ra (Cổng Out)",
        dashLiveFeed: "● Trực tiếp",
        dashNoEntry: "Chưa có lượt xe vào nào gần đây.",
        dashNoExit: "Chưa có lượt xe ra nào gần đây.",

        // Staff QR & UI
        qrTicketTitle: "Vé giữ xe điện tử (QR)",
        qrTicketDesc:
            "Cung cấp mã QR này cho tài xế khách để xác thực cổng ra và thanh toán tiền gửi xe.",
        qrCopy: "Sao chép mã",
        qrCopied: "Đã sao chép!",
        qrDone: "Hoàn tất / In vé",

        // Gate & Barrier
        barrierStatus: "TRẠNG THÁI BARRIER",
        gateIn: "Cổng Vào",
        gateOut: "Cổng Ra",
        gateOnline: "Hoạt động",

        // Staff Check-in (Entry)
        entryPageTitle: "Cổng Vào — Kiểm Soát Xe Vào Bãi",
        entryPageSubtitle:
            "Tự động nhận diện biển số (ANPR), hiệu chỉnh biển số và khởi tạo phiên gửi xe.",
        entryClearBtn: "Xóa / Tiếp nhận xe tiếp theo",
        entrySimulateLabel: "Mô phỏng xe vào cổng:",
        entryPresetRegCar: "Ô tô đăng ký (97% OCR)",
        entryPresetGuestBike: "Xe máy vãng lai (99% OCR)",
        entryPresetLowConf: "OCR độ tin cậy thấp (62%)",
        entryCameraTitle: "Cổng Vào 01 · Camera ANPR HD",
        entryPlateVerifTitle: "Xác Thực Biển Số Xe",
        entryConfidence: "Độ tin cậy:",
        entryLowConfWarning:
            "Độ tin cậy OCR của camera dưới ngưỡng (80%). Vui lòng kiểm tra và chỉnh sửa biển số trước khi mở rào chắn.",
        entryHighConfHint:
            "Nhận diện biển số khớp độ tin cậy cao. Nhân viên vẫn có thể sửa thủ công nếu cần.",
        entryPlateLabel: "Biển số xe",
        entryConfirmPlateBtn: "Xác nhận biển số",
        entryVehicleTypeLabel: "Loại phương tiện",
        entryCustCategoryLabel: "Phân loại khách hàng",
        entryRegOption: "Đã đăng ký (Tài khoản App)",
        entryGuestOption: "Khách vãng lai",
        entryFlowRegTitle: "Quy Trình Tiếp Nhận Xe Đã Đăng Ký",
        entryFlowGuestTitle: "Quy Trình Tiếp Nhận Xe Vãng Lai",
        entryStep1: "Camera nhận diện",
        entryStep2: "Biển số & Loại xe",
        entryStep3Reg: "Tài khoản / Đặt chỗ",
        entryStep3Guest: "Kiểm tra chỗ trống",
        entryStep4: "Tạo phiên xe",
        entryStep5: "Cho xe vào bãi",
        entrySummaryPlate: "Biển số xác nhận:",
        entrySummaryType: "Loại xe:",
        entrySummaryCap: "Chỗ trống bãi xe:",
        entrySummaryAccount: "Trạng thái tài khoản:",
        entryAccountLinked: "Đã liên kết tài khoản (ví khả dụng)",
        entryAccountGuest: "Khách vãng lai (Cấp vé mã QR)",
        entryQrTokenLabel: "Mã QR phiên gửi xe:",
        entryMotorbikeSlotsLeft: "Số chỗ xe máy còn trống:",
        entryCarSlotsLeft: "Số chỗ ô tô còn trống:",
        entryBarrierRaised: "RÀO CHẮN ĐÃ NÂNG — CHO XE VÀO BÃI",
        entryBarrierHint: "Phương tiện được phép tiến vào bãi và đỗ xe.",
        entryProcessing: "Đang xử lý vào bãi...",
        entryBtnGuest: "Tạo phiên & Cấp vé QR (Cho xe vào)",
        entryBtnReg: "Xác thực & Mở rào chắn",
        entryViewQrBtn: "Xem vé QR của khách",
        entryConfirmParkedBtn: "Xác nhận đã vào chỗ",
        entryParkedConfirmed: "Đã vào chỗ đỗ",
        entryNextVehicleBtn: "Tiếp nhận xe tiếp theo",

        // Staff Check-out (Exit)
        exitPageTitle: "Cổng Ra — Xác Thực & Thu Phí",
        exitPageSubtitle:
            "Quét QR, đối chiếu biển số, xử lý cước phí gửi xe và giải phóng vị trí đỗ.",
        exitClearBtn: "Xóa / Reset Cổng Ra",
        exitSimulateLabel: "Mô phỏng xe tại cổng ra:",
        exitPresetGuestUnpaid: "Ô tô vãng lai (Chưa trả: 60.000₫)",
        exitPresetRegPrepaid: "Ô tô đăng ký (Trả trước: 0₫)",
        exitPresetMismatch: "Thử nghiệm lệch biển số",
        exitCameraTitle: "Cổng Ra 02 · Camera ANPR",
        exitQrVerifTitle: "Xác Thực Vé QR / Vé Khách",
        exitScannerReady: "Máy quét sẵn sàng",
        exitScanQrLabel: "Quét mã QR hoặc nhập chuỗi ký tự vé QR",
        exitVerifyQrBtn: "Kiểm tra QR",
        exitVerifying: "Đang kiểm tra...",
        exitCameraPlateLabel: "Biển số camera cổng ra đọc được",
        exitChecklistTitle: "Danh Mục Kiểm Tra Xuất Bãi",
        exitAuthorized: "CHO PHÉP XUẤT BÃI",
        exitDenied: "TỪ CHỐI XUẤT BÃI",
        exitCheckQr: "Vé QR phiên xe hợp lệ",
        exitCheckQrScanned: "Đã quét:",
        exitCheckQrNone: "Chưa kiểm tra mã QR",
        exitCheckPlate: "Khớp biển số vào - ra",
        exitCheckPlateCam: "Camera:",
        exitCheckPlateSession: "Phiên xe:",
        exitCheckSession: "Trạng thái phiên xe",
        exitCheckSessionActive: "Phiên đang hoạt động",
        exitCheckSessionClosed: "Không hợp lệ / Đã đóng",
        exitCheckPayment: "Thanh toán đầy đủ",
        exitCheckPaymentSettled: "Đã thanh toán đủ cước phí",
        exitCheckPaymentPending: "Cước phí còn nợ:",
        exitStatusPassed: "Hợp lệ",
        exitStatusFailed: "Chưa đạt",
        exitStatusWarning: "Chờ thanh toán",
        exitFeeTitle: "Tính Tiền Cước Gửi Xe",
        exitTotalFee: "Tổng tiền cước gửi xe:",
        exitPrepaidWallet: "Đã trừ qua Ví App:",
        exitAmountDue: "Tiền cần thu tại cổng:",
        exitCollectWarning:
            "Còn tiền cước chưa trả. Thu phí trước khi cho xe ra.",
        exitCollectCash: "Thu tiền mặt",
        exitVnPayQr: "Quét mã VNPay QR",
        exitBarrierRaised: "ĐÃ ĐÓNG PHIÊN & NÂNG RÀO CHẮN",
        exitBarrierHint:
            "Vị trí đỗ đã được giải phóng. Cập nhật sức chứa thành công.",
        exitAuthorizeBtn: "Xác nhận ra & Mở rào chắn",
        exitBlockedBtn: "Chặn cổng (Chưa đủ điều kiện)",
        exitDoneBtn: "Hoàn tất / Tiếp nhận xe tiếp theo",
        exitCashModalTitle: "Thu Phí Tiền Mặt",
        exitVnPayModalTitle: "Thanh Toán VNPay QR",
        exitModalSub:
            "Xác nhận giao dịch thanh toán tại bốt trước khi mở barrier",
        exitPlateLabel: "Biển số phương tiện:",
        exitSpotLabel: "Vị trí đỗ:",
        exitAmountDueLabel: "Tổng cước còn lại:",
        exitCashReceivedLabel: "Tiền khách đưa (VND)",
        exitCashHint: "Nhập hoặc chọn nhanh",
        exitChangeDue: "Tiền thối lại cho khách:",
        exitExactCash: "Đủ tiền",
        exitConfirmCashPayment: "Xác nhận đã nhận tiền mặt",
        exitSimulateVnPaySuccess: "Mô phỏng khách thanh toán VNPay thành công",
        exitClose: "Đóng",

        // Staff Verification Queue
        verifTitle: "Hàng Đợi Xác Thực Sự Cố Cổng",
        verifSubtitle:
            "Các trường hợp bất thường tại cổng yêu cầu nhân viên can thiệp thủ công, xác minh OCR và xử lý rào chắn.",
        verifColSpot: "Vị trí / Cổng",
        verifColProblem: "Sự cố",
        verifColDetectedPlate: "Biển số camera",
        verifColConf: "Độ tin cậy",
        verifColState: "Trạng thái hiện tại",
        verifColReportedAt: "Thời gian báo",
        verifColAction: "Thao tác xử lý",
        verifEmpty: "Không có sự cố nào đang chờ nhân viên xác thực!",
        verifApproveBarrier: "Mở Barrier",
        verifDismiss: "Bỏ qua",
    },
    en: {
        appName: "ParkWise",
        switchLanguage: "Change Language",
        vietnamese: "Tiếng Việt",
        english: "English",
        refresh: "Refresh",
        search: "Search...",
        all: "All",
        pending: "Pending",
        approved: "Approved",
        rejected: "Rejected",
        cancel: "Cancel",
        confirm: "Confirm",
        close: "Close",
        view: "View",
        details: "Details",
        save: "Save",
        saving: "Saving...",
        loading: "Loading...",
        notSignedIn: "Not signed in",
        signOut: "Sign out",
        status: "Status",
        actions: "Actions",
        name: "Full Name",
        email: "Email",
        phone: "Phone Number",
        createdAt: "Created Date",

        // Admin
        adminPortal: "Admin Portal",
        adminDashboard: "Dashboard",
        adminPendingLots: "Pending Review",
        adminParkingLots: "Parking Lots",
        adminLotAssignments: "Staff Assignments",
        adminOwnerApplications: "Owner Applications",
        adminUsers: "Users & Owners",
        systemAdmin: "System Administrator",

        // Admin Parking Lots
        parkingLotsTitle: "Parking Lots Management",
        parkingLotsEyebrow: "Parking Network",
        parkingLotsPendingTab: "Pending Lots",
        parkingLotsOwnersTab: "Owner Applications",
        parkingLotsAssignmentsTab: "Staff Assignments",
        searchLotsPlaceholder: "Search by lot name, owner, city...",
        lookupLotId: "Enter Lot ID (GUID) to lookup...",
        lookupBtn: "Open",
        noLotsFound: "No parking lots found matching your filter.",
        lotNameHeader: "Parking Lot",
        lotOwnerHeader: "Owner",
        lotCityHeader: "Address / City",
        lotVehiclesHeader: "Vehicles",
        lotCapacityHeader: "Capacity",
        lotStatusHeader: "Status",
        approveLot: "Approve",
        rejectLot: "Reject",
        assignLot: "Assign",
        viewLot: "View",

        // Admin Dashboard
        adminDashEyebrow: "System overview",
        adminDashTitle: "Dashboard",
        adminDashReviewQueueBtn: "Review queue",
        adminDashWaitingReview: "Waiting for review",
        adminDashActiveLots: "Active parking lots",
        adminDashRejectedLots: "Rejected applications",
        adminDashApprovedCapacity: "Approved capacity",
        adminDashSlotsOpen: "slots open to drivers",
        adminDashOldestPending: "Oldest pending first",
        adminDashSeeAll: "See all",
        adminDashNothingToReview: "Nothing to review. Nice work.",
        adminDashRecentDecisions: "Recent decisions",
        adminDashNoDecisions: "No decisions yet.",
        adminDashSubmittedAt: "submitted",
        adminDashReviewBtn: "Review",

        // Admin Owner Applications
        ownerAppsTitle: "Owner Applications",
        ownerAppsEyebrow: "Partner Verification",
        ownerAppsBusinessName: "Business / Tax ID",
        ownerAppsTaxCode: "Tax Code",
        ownerAppsDocuments: "Legal Documents",
        ownerAppsInspectDocs: "View Documents",
        ownerAppsVerifyAndApprove: "Inspect & Approve",
        ownerAppsReject: "Reject",
        ownerAppsChecklistTitle:
            "Legal Verification Checklist (All 4 required):",
        ownerAppsChecklistRule1:
            "1. Valid Business Registration & Tax Code on national registry",
        ownerAppsChecklistRule2:
            "2. Clear and valid Citizen ID card of legal representative",
        ownerAppsChecklistRule3: "3. Fire safety design approval / certificate",
        ownerAppsChecklistRule4:
            "4. Valid premises lease agreement or land ownership title",
        ownerAppsChecklistWarning:
            "Approval is only allowed after verifying all 4 legal criteria above.",
        ownerAppsConfirmApprove: "Verified All Docs & Approve",
        ownerAppsRejectReasonLabel: "Rejection Reason (required):",
        ownerAppsRejectReasonPlaceholder:
            "Example: Missing fire safety documentation or invalid premises lease.",
        ownerAppsEmpty: "No owner applications found.",
        ownerAppsApplicant: "Applicant / Account",
        ownerAppsBusiness: "Business / Tax ID",
        ownerAppsLegalDocs: "Legal Documents",
        ownerAppsSubmittedAt: "Submitted Date",
        ownerAppsSearchPlaceholder:
            "Search by name, email, tax code, phone or business...",
        ownerAppsInspectDocsModalTitle: "Owner Application Legal Documents",
        ownerAppsApproveModalTitle: "Document Review & Application Approval",
        ownerAppsRepresentative: "Legal Representative:",
        ownerAppsIdCardNumber: "Citizen ID (CCCD):",
        ownerAppsRegisteredAddress: "Registered Address:",
        ownerAppsAttachedDocs: "Attached Documents",
        ownerAppsNoDocsWarning:
            "Applicant has not uploaded any legal documents.",
        ownerAppsSelectAllChecklist: "Select All",
        ownerAppsDeselectAllChecklist: "Deselect All",
        ownerAppsRejectMissingDocsBtn: "Missing Documents ➔ Reject",
        ownerAppsRejectConfirmTitle: "Reject Owner Application?",
        ownerAppsRejectConfirmDesc:
            "Please enter a reason for rejection to notify the applicant",
        ownerAppsConfirmRejectBtn: "Confirm Rejection",
        ownerAppsOpenNewTab: "Open in new tab ↗",
        ownerAppsPendingEmpty: "No pending owner applications.",
        ownerAppsFilterEmpty: "No owner applications match this filter.",
        ownerAppsLoading: "Loading owner applications...",
        ownerAppsNotUpdated: "Not updated",
        ownerAppsMissingDocsAlert:
            "All 4 legal requirements must be verified before approval.",

        // Admin Staff Assignments
        assignmentsTitle: "Staff Lot Assignments",
        assignmentsEyebrow: "Operations Management",
        selectLotTitle: "Select Parking Lot",
        staffAtLotTitle: "Assigned Staff at",
        staffAtLotSubtitle:
            "Manage staff accounts authorized for gate check-in/check-out at this parking lot.",
        assignStaffTitle: "Assign New Member to Lot",
        selectStaffLabel: "Select Staff Account:",
        selectStaffDefault: "-- Choose staff member --",
        assignRoleLabel: "Role at Lot:",
        roleStaff: "On-site Staff",
        roleOwner: "Lot Manager (Owner)",
        assignBtn: "Assign to Lot",
        removeStaffBtn: "Remove",
        noStaffAssigned:
            "No staff assigned to this parking lot yet. Use the form below to assign staff.",
        assignmentsColStaff: "Staff Member / Name",
        assignmentsColContact: "Email & Phone Number",
        assignmentsColRole: "Role at Lot",
        assignmentsColShift: "Work Shift",
        assignmentsSelectShift: "Assign Work Shift",
        shiftMorning: "Shift 1 - Morning (06:00 - 14:00)",
        shiftAfternoon: "Shift 2 - Afternoon (14:00 - 22:00)",
        shiftNight: "Shift 3 - Night (22:00 - 06:00)",
        shiftAllDay: "All Day / 24/7 (00:00 - 23:59)",
        assignmentsLoadingLots: "Loading parking lots...",
        assignmentsLoadingAssignments: "Loading staff assignments...",
        assignmentsAssigning: "Assigning...",
        assignmentsRemoveConfirm:
            "Are you sure you want to remove {name} from this parking lot?",
        assignmentsBackToLots: "← Parking Lots",
        assignmentsOwnerAppsLink: "Owner Applications",
        assignmentsDefaultStaffName: "On-site Staff",

        // Admin Users Page
        adminUsersEyebrow: "Identity & Accounts",
        adminUsersTitle: "Users & Owners",
        adminUsersSearchPlaceholder: "Search email or name",
        adminUsersAllRoles: "All roles",
        adminUsersAllStatuses: "All statuses",
        adminUsersColAccount: "Account",
        adminUsersColRole: "Role",
        adminUsersColStatus: "Status",
        adminUsersColCreated: "Created",
        adminUsersColActions: "Actions",
        adminUsersSuspend: "Suspend",
        adminUsersActivate: "Activate",
        adminUsersUnlock: "Unlock",
        adminUsersChangeRole: "Change role",
        adminUsersEmpty: "No accounts found.",
        adminUsersPrevious: "Previous",
        adminUsersNext: "Next",
        adminUsersPageOf: "Page",

        // Staff Portal
        staffStation: "STAFF STATION",
        staffOperations: "OPERATIONS",
        staffDashboard: "Shift Dashboard",
        staffEntryGate: "Entry Gate (Check-in)",
        staffExitGate: "Exit Gate (Check-out)",
        staffSessions: "Sessions History",
        staffShift: "Shift #2 · Morning",
        staffOperator: "Staff Operator",
        staffAssignedLot: "Assigned Parking Lot",
        staffLiveCapacity: "Realtime Capacity",
        staffCarSlots: "Car Available",
        staffMotorbikeSlots: "Motorbike Available",
        staffCheckinTitle: "Entry Gate Check-in",
        staffCheckinSubtitle:
            "Record incoming vehicles and issue digital QR tickets",
        staffPlateNumber: "License Plate",
        staffVehicleType: "Vehicle Type",
        staffCar: "Car",
        staffMotorbike: "Motorbike",
        staffCreateSessionBtn: "Create Session & Issue QR",
        staffCheckoutTitle: "Exit Gate Check-out",
        staffScanQr: "Scan or enter ticket QR token",
        staffCalculateFee: "Calculate Parking Fee",
        staffPaymentQr: "VietQR Payment Code",
        staffCompleteSessionBtn: "Complete Payment & Open Barrier",

        // Staff Sessions Management
        sessionsTitle: "Parking Session History",
        sessionsSubtitle:
            "Track, search, and manage all parking sessions in the lot.",
        sessionsSearchPlaceholder:
            "Search license plate, session code, or spot...",
        sessionsAllTab: "All Sessions",
        sessionsGuestTab: "Guest Vehicles",
        sessionsRegisteredTab: "Registered App Users",
        sessionsLicensePlate: "License Plate",
        sessionsCategory: "Category",
        sessionsVehicle: "Vehicle",
        sessionsSpot: "Spot",
        sessionsEntryTime: "Entry Time",
        sessionsStatus: "Status",
        sessionsPaymentStatus: "Payment",
        sessionsQr: "QR Ticket",
        sessionsActions: "Actions",
        sessionsEmpty: "No parking sessions match your search.",
        sessionsGuest: "Guest",
        sessionsRegistered: "Registered",
        sessionsDue: "Due",
        sessionsViewQr: "QR Ticket",
        sessionsCancelEntry: "Cancel",
        sessionsCancelTitle: "Cancel Parking Session",
        sessionsCancelConfirmText:
            "Are you sure you want to cancel the session for",
        sessionsCancelReasonLabel: "Reason for cancellation",
        sessionsCancelReasonPlaceholder:
            "Enter reason (e.g. driver decided to leave)...",
        sessionsGoBack: "Go back",
        sessionsConfirmCancel: "Confirm Cancellation",
        sessionsCancelling: "Cancelling...",

        // Status
        statusActive: "Active",
        statusPaid: "Paid",
        statusPending: "Pending",
        statusCompleted: "Completed",
        statusCancelled: "Cancelled",

        // Staff Dashboard
        dashTitle: "Staff Dashboard",
        dashSubtitle:
            "Real-time gate traffic, slot capacity, and pending verifications.",
        dashProcessEntry: "Process Vehicle Entry",
        dashProcessExit: "Process Vehicle Exit",
        dashOccupancy: "Current Occupancy",
        dashFull: "FULL",
        dashSlots: "Slots",
        dashCarAvailable: "Car Slots Available:",
        dashMotorbikeAvailable: "Motorbike Available:",
        dashActiveSessions: "Active Sessions",
        dashActiveHint: "Vehicles parked right now",
        dashViewSessions: "View all active sessions →",
        dashNeedsVerif: "Needs Verification",
        dashNeedsVerifHint: "Camera OCR flags & barrier issues",
        dashResolveNow: "Resolve issues now →",
        dashGateHealth: "Barrier Gate Health",
        dashGateSynced: "Gate 01 & Gate 02 Synced",
        dashLatency: "Latency",
        dashEntryActivity: "Entry Activity (Gate In)",
        dashExitActivity: "Exit Activity (Gate Out)",
        dashLiveFeed: "● Live feed",
        dashNoEntry: "No recent vehicle entries.",
        dashNoExit: "No recent vehicle exits.",

        // Staff QR & UI
        qrTicketTitle: "Guest Parking Ticket (QR)",
        qrTicketDesc:
            "Provide this QR code to the guest driver for exit gate verification and fee payment.",
        qrCopy: "Copy Token",
        qrCopied: "Copied Token!",
        qrDone: "Done / Print Ticket",

        // Gate & Barrier
        barrierStatus: "BARRIER GATE STATUS",
        gateIn: "Gate In",
        gateOut: "Gate Out",
        gateOnline: "Online",

        // Staff Check-in (Entry)
        entryPageTitle: "Gate In — Vehicle Entry Control",
        entryPageSubtitle:
            "Automated ANPR camera detection, plate correction, and session opening.",
        entryClearBtn: "Clear / Scan Next Vehicle",
        entrySimulateLabel: "Simulate incoming vehicle:",
        entryPresetRegCar: "Registered Car (97% OCR)",
        entryPresetGuestBike: "Guest Motorbike (99% OCR)",
        entryPresetLowConf: "Low Confidence OCR (62%)",
        entryCameraTitle: "Gate In 01 · HD ANPR Camera",
        entryPlateVerifTitle: "License Plate Verification",
        entryConfidence: "Confidence:",
        entryLowConfWarning:
            "Camera OCR confidence is below threshold (80%). Please review and correct the license plate before opening the gate.",
        entryHighConfHint:
            "High confidence match detected. Staff can still edit manually if needed.",
        entryPlateLabel: "License plate",
        entryConfirmPlateBtn: "Confirm Plate",
        entryVehicleTypeLabel: "Vehicle Type",
        entryCustCategoryLabel: "Customer Category",
        entryRegOption: "Registered (App account)",
        entryGuestOption: "Guest",
        entryFlowRegTitle: "Registered Vehicle Entry Flow",
        entryFlowGuestTitle: "Guest Vehicle Entry Flow",
        entryStep1: "Camera Detect",
        entryStep2: "Plate & Type",
        entryStep3Reg: "Account / Reservation",
        entryStep3Guest: "Capacity Check",
        entryStep4: "Open Session",
        entryStep5: "Allow Entry",
        entrySummaryPlate: "Plate to Authorize:",
        entrySummaryType: "Vehicle Type:",
        entrySummaryCap: "Lot Capacity Available:",
        entrySummaryAccount: "Account Status:",
        entryAccountLinked: "Linked account (wallet active)",
        entryAccountGuest: "Guest (No account needed · QR Issued)",
        entryQrTokenLabel: "Parking Session QR Token:",
        entryMotorbikeSlotsLeft: "Motorbike slots available:",
        entryCarSlotsLeft: "Car slots available:",
        entryBarrierRaised: "BARRIER RAISED — ALLOW ENTRY",
        entryBarrierHint: "Vehicle is clear to enter zone and park.",
        entryProcessing: "Processing Entry...",
        entryBtnGuest: "Create Session & Issue QR (Allow Entry)",
        entryBtnReg: "Verify & Allow Entry",
        entryViewQrBtn: "View Guest QR Ticket",
        entryConfirmParkedBtn: "Confirm Parked",
        entryParkedConfirmed: "Parked Confirmed",
        entryNextVehicleBtn: "Process Next Vehicle",

        // Staff Check-out (Exit)
        exitPageTitle: "Gate Out — Exit Verification & Payment",
        exitPageSubtitle:
            "Scan QR, cross-check license plate, process parking fee, and release spot.",
        exitClearBtn: "Clear Exit Gate",
        exitSimulateLabel: "Simulate vehicle at exit:",
        exitPresetGuestUnpaid: "Guest Car (Unpaid: 60,000₫)",
        exitPresetRegPrepaid: "Registered Car (Prepaid: 0₫ Due)",
        exitPresetMismatch: "Plate Mismatch Test",
        exitCameraTitle: "Gate Out 02 · Exit ANPR Camera",
        exitQrVerifTitle: "Guest QR / Ticket Verification",
        exitScannerReady: "Scanner Ready",
        exitScanQrLabel: "Scan QR Barcode or Enter QR Token",
        exitVerifyQrBtn: "Verify QR",
        exitVerifying: "Verifying...",
        exitCameraPlateLabel: "Exit Camera Detected Plate",
        exitChecklistTitle: "Exit Verification Checklist",
        exitAuthorized: "EXIT AUTHORIZED",
        exitDenied: "EXIT DENIED",
        exitCheckQr: "Valid Session QR",
        exitCheckQrScanned: "Scanned:",
        exitCheckQrNone: "No QR verified yet",
        exitCheckPlate: "License Plate Match",
        exitCheckPlateCam: "Camera:",
        exitCheckPlateSession: "Session:",
        exitCheckSession: "Session Status",
        exitCheckSessionActive: "Session is Active",
        exitCheckSessionClosed: "Invalid / already closed",
        exitCheckPayment: "Payment Completed",
        exitCheckPaymentSettled: "Payment settled in full",
        exitCheckPaymentPending: "Outstanding amount:",
        exitStatusPassed: "Passed",
        exitStatusFailed: "Unmet",
        exitStatusWarning: "Pending",
        exitFeeTitle: "Parking Fee Calculation",
        exitTotalFee: "Total Parking Fee:",
        exitPrepaidWallet: "Prepaid via App Wallet:",
        exitAmountDue: "Amount Due at Exit:",
        exitCollectWarning:
            "Amount Due > 0. Collect payment before allowing vehicle exit.",
        exitCollectCash: "Collect Cash",
        exitVnPayQr: "VNPay QR Payment",
        exitBarrierRaised: "SESSION CLOSED & BARRIER RAISED",
        exitBarrierHint: "Spot is now released. Capacity updated.",
        exitAuthorizeBtn: "Authorize Exit & Raise Barrier",
        exitBlockedBtn: "Exit Blocked (Requirements Unmet)",
        exitDoneBtn: "Done / Process Next Vehicle",
        exitCashModalTitle: "Cash Payment",
        exitVnPayModalTitle: "VNPay QR Payment",
        exitModalSub:
            "Confirm payment transaction at kiosk before raising barrier",
        exitPlateLabel: "License Plate:",
        exitSpotLabel: "Parking Spot:",
        exitAmountDueLabel: "Remaining Due:",
        exitCashReceivedLabel: "Cash Received (VND)",
        exitCashHint: "Type or select quickly",
        exitChangeDue: "Change Due to Customer:",
        exitExactCash: "Exact",
        exitConfirmCashPayment: "Confirm Cash Collected",
        exitSimulateVnPaySuccess: "Simulate VNPay Success",
        exitClose: "Close",

        // Staff Verification Queue
        verifTitle: "Needs Verification Queue",
        verifSubtitle:
            "Exceptional gate cases requiring manual operator confirmation, OCR overrides, and barrier interventions.",
        verifColSpot: "Spot / Gate",
        verifColProblem: "Problem",
        verifColDetectedPlate: "Detected Plate",
        verifColConf: "OCR Conf.",
        verifColState: "Current State",
        verifColReportedAt: "Reported At",
        verifColAction: "Verification Action",
        verifEmpty: "No issues currently pending operator verification!",
        verifApproveBarrier: "Approve Barrier",
        verifDismiss: "Dismiss",
    },
};
