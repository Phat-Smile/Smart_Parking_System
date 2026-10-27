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
    },
};
