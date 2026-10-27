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
        parkingLotsPendingTab: "🅿️ Bãi xe chờ duyệt",
        parkingLotsOwnersTab: "👤 Duyệt hồ sơ Chủ bãi",
        parkingLotsAssignmentsTab: "👥 Phân công nhân viên",
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
        assignLot: "👥 Phân công",
        viewLot: "Xem",

        // Admin Owner Applications
        ownerAppsTitle: "Duyệt hồ sơ Chủ bãi",
        ownerAppsEyebrow: "Xác thực đối tác",
        ownerAppsBusinessName: "Doanh nghiệp / MST",
        ownerAppsTaxCode: "Mã số thuế",
        ownerAppsDocuments: "Giấy tờ pháp lý",
        ownerAppsInspectDocs: "📑 Xem giấy tờ",
        ownerAppsVerifyAndApprove: "Kiểm tra & Duyệt",
        ownerAppsReject: "Từ chối",
        ownerAppsChecklistTitle: "📋 Checklist kiểm tra điều kiện pháp lý (Yêu cầu đủ 4 mục):",
        ownerAppsChecklistRule1: "1. Giấy phép ĐKKD & Mã số thuế hợp lệ trên cổng quốc gia",
        ownerAppsChecklistRule2: "2. Căn cước công dân người đại diện pháp luật hợp lệ, rõ nét",
        ownerAppsChecklistRule3: "3. Giấy chứng nhận thẩm duyệt thiết kế / đủ điều kiện PCCC",
        ownerAppsChecklistRule4: "4. Hợp đồng thuê mặt bằng hoặc quyền sử dụng bãi xe hợp pháp",
        ownerAppsChecklistWarning: "⚠️ Chỉ có thể duyệt khi kiểm tra và đánh dấu đủ cả 4 mục giấy tờ trên.",
        ownerAppsConfirmApprove: "✓ Xác nhận đủ giấy tờ & Duyệt",
        ownerAppsRejectReasonLabel: "Lý do từ chối (bắt buộc):",
        ownerAppsRejectReasonPlaceholder: "Ví dụ: Hồ sơ chưa đủ điều kiện: Thiếu giấy phép PCCC và Hợp đồng bãi đỗ.",
        ownerAppsEmpty: "Không có hồ sơ chủ bãi nào phù hợp.",

        // Admin Staff Assignments
        assignmentsTitle: "Phân công Nhân viên trực bãi",
        assignmentsEyebrow: "Quản trị vận hành",
        selectLotTitle: "Chọn bãi đỗ xe",
        staffAtLotTitle: "Nhân viên trực tại bãi",
        staffAtLotSubtitle: "Quản lý danh sách nhân sự được phân quyền kiểm soát cổng vào / cổng ra (Staff Portal).",
        assignStaffTitle: "Phân công thêm nhân sự vào bãi",
        selectStaffLabel: "Chọn nhân viên:",
        selectStaffDefault: "-- Chọn nhân viên từ hệ thống --",
        assignRoleLabel: "Vai trò tại bãi:",
        roleStaff: "Nhân viên trực (Staff)",
        roleOwner: "Quản lý bãi (Owner)",
        assignBtn: "Gán vào bãi",
        removeStaffBtn: "Gỡ khỏi bãi",
        noStaffAssigned: "Chưa có nhân viên nào được phân công vào bãi đỗ xe này.",

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
        sessionsTitle: "Quản lý Phiên đỗ xe Khách & Đang gửi",
        sessionsSubtitle: "Kiểm tra các phương tiện đang đỗ trong bãi, xem vé QR của khách và hủy phiên xe nếu cần.",
        sessionsSearchPlaceholder: "Tìm kiếm biển số xe, vị trí đỗ, hoặc tên tài xế...",
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
        sessionsViewQr: "🎟️ Xem vé QR",
        sessionsCancelEntry: "Hủy phiên vào",
        sessionsCancelTitle: "Hủy phiên gửi xe",
        sessionsCancelConfirmText: "Bạn có chắc muốn hủy phiên xe cho biển số",
        sessionsCancelReasonLabel: "Lý do hủy phiên:",
        sessionsCancelReasonPlaceholder: "Nhập lý do hủy (ví dụ: Xe quay đầu không vào bãi)...",
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
        dashSubtitle: "Theo dõi lưu lượng xe tại cổng, công suất chỗ đỗ thời gian thực và các cảnh báo.",
        dashProcessEntry: "➕ Tiếp nhận xe vào cổng",
        dashProcessExit: "⚡ Xử lý xe ra cổng",
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
        qrTicketDesc: "Cung cấp mã QR này cho tài xế khách để xác thực cổng ra và thanh toán tiền gửi xe.",
        qrCopy: "Sao chép mã",
        qrCopied: "Đã sao chép!",
        qrDone: "Hoàn tất / In vé",

        // Gate & Barrier
        barrierStatus: "TRẠNG THÁI BARRIER",
        gateIn: "Cổng Vào",
        gateOut: "Cổng Ra",
        gateOnline: "Hoạt động",
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
        parkingLotsPendingTab: "🅿️ Pending Lots",
        parkingLotsOwnersTab: "👤 Owner Applications",
        parkingLotsAssignmentsTab: "👥 Staff Assignments",
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
        assignLot: "👥 Assign",
        viewLot: "View",

        // Admin Owner Applications
        ownerAppsTitle: "Owner Applications",
        ownerAppsEyebrow: "Partner Verification",
        ownerAppsBusinessName: "Business / Tax ID",
        ownerAppsTaxCode: "Tax Code",
        ownerAppsDocuments: "Legal Documents",
        ownerAppsInspectDocs: "📑 View Documents",
        ownerAppsVerifyAndApprove: "Inspect & Approve",
        ownerAppsReject: "Reject",
        ownerAppsChecklistTitle: "📋 Legal Verification Checklist (All 4 required):",
        ownerAppsChecklistRule1: "1. Valid Business Registration & Tax Code on national registry",
        ownerAppsChecklistRule2: "2. Clear and valid Citizen ID card of legal representative",
        ownerAppsChecklistRule3: "3. Fire safety design approval / certificate",
        ownerAppsChecklistRule4: "4. Valid premises lease agreement or land ownership title",
        ownerAppsChecklistWarning: "⚠️ Approval is only allowed after verifying all 4 legal criteria above.",
        ownerAppsConfirmApprove: "✓ Verified All Docs & Approve",
        ownerAppsRejectReasonLabel: "Rejection Reason (required):",
        ownerAppsRejectReasonPlaceholder: "Example: Missing fire safety documentation or invalid premises lease.",
        ownerAppsEmpty: "No owner applications found.",

        // Admin Staff Assignments
        assignmentsTitle: "Staff Lot Assignments",
        assignmentsEyebrow: "Operations Management",
        selectLotTitle: "Select Parking Lot",
        staffAtLotTitle: "Assigned Staff at Lot",
        staffAtLotSubtitle: "Manage staff accounts authorized for gate check-in/check-out at this parking lot.",
        assignStaffTitle: "Assign New Member to Lot",
        selectStaffLabel: "Select Staff Account:",
        selectStaffDefault: "-- Choose staff member --",
        assignRoleLabel: "Role at Lot:",
        roleStaff: "On-site Staff",
        roleOwner: "Lot Manager (Owner)",
        assignBtn: "Assign to Lot",
        removeStaffBtn: "Remove",
        noStaffAssigned: "No staff assigned to this parking lot yet.",

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
        staffCheckinSubtitle: "Record incoming vehicles and issue digital QR tickets",
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
        sessionsTitle: "Guest & Active Session Management",
        sessionsSubtitle: "Inspect active parked vehicles, view guest QR tickets, and handle cancellations.",
        sessionsSearchPlaceholder: "Search license plate, slot, or driver name...",
        sessionsAllTab: "All Sessions",
        sessionsGuestTab: "Guest Vehicles",
        sessionsRegisteredTab: "Registered App Users",
        sessionsLicensePlate: "License Plate",
        sessionsCategory: "Category",
        sessionsVehicle: "Vehicle",
        sessionsSpot: "Spot",
        sessionsEntryTime: "Entry Time",
        sessionsStatus: "Session Status",
        sessionsPaymentStatus: "Payment Status",
        sessionsQr: "Session QR",
        sessionsActions: "Actions",
        sessionsEmpty: "No parking sessions match your search.",
        sessionsGuest: "Guest",
        sessionsRegistered: "Registered",
        sessionsDue: "Due",
        sessionsViewQr: "🎟️ View QR",
        sessionsCancelEntry: "Cancel Entry",
        sessionsCancelTitle: "Cancel Parking Session",
        sessionsCancelConfirmText: "Are you sure you want to cancel the session for",
        sessionsCancelReasonLabel: "Reason for cancellation",
        sessionsCancelReasonPlaceholder: "Enter reason (e.g. driver decided to leave)...",
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
        dashSubtitle: "Real-time gate traffic, slot capacity, and pending verifications.",
        dashProcessEntry: "➕ Process Vehicle Entry",
        dashProcessExit: "⚡ Process Vehicle Exit",
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
        qrTicketDesc: "Provide this QR code to the guest driver for exit gate verification and fee payment.",
        qrCopy: "Copy Token",
        qrCopied: "Copied Token!",
        qrDone: "Done / Print Ticket",

        // Gate & Barrier
        barrierStatus: "BARRIER GATE STATUS",
        gateIn: "Gate In",
        gateOut: "Gate Out",
        gateOnline: "Online",
    },
};
