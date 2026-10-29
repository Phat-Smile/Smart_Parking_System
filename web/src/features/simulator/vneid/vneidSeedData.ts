export interface VneidCitizenProfile {
    cccd: string;
    fullName: string;
    dob: string; // YYYY-MM-DD
    dobDisplay: string; // DD/MM/YYYY
    gender: "M" | "F";
    genderDisplay: "Nam" | "Nữ";
    nationality: string;
    hometown: string;
    residence: string;
    expiryDate: string; // DD/MM/YYYY
    issueDate: string; // DD/MM/YYYY
    category:
        "owner_individual" | "owner_business" | "customer" | "admin" | "staff";
    label: string;
    demoAccount?: string;
    description: string;
}

export const VNEID_SEED_PROFILES: VneidCitizenProfile[] = [
    {
        cccd: "079201234567",
        fullName: "NGUYỄN VĂN AN",
        dob: "2001-03-15",
        dobDisplay: "15/03/2001",
        gender: "M",
        genderDisplay: "Nam",
        nationality: "Việt Nam",
        hometown: "Bến Tre",
        residence:
            "Số 123 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
        expiryDate: "15/03/2041",
        issueDate: "10/04/2021",
        category: "owner_individual",
        label: "Chủ bãi cá nhân (Nguyễn Văn An)",
        demoAccount: "owner.flow@demo.local",
        description: "Hồ sơ chủ bãi cá nhân chờ duyệt (Flow A Master Sheet)",
    },
    {
        cccd: "001199123456",
        fullName: "TRẦN THỊ BÍCH",
        dob: "1991-07-22",
        dobDisplay: "22/07/1991",
        gender: "F",
        genderDisplay: "Nữ",
        nationality: "Việt Nam",
        hometown: "Hà Nội",
        residence:
            "Số 45 Tràng Tiền, Phường Tràng Tiền, Quận Hoàn Kiếm, Hà Nội",
        expiryDate: "22/07/2031",
        issueDate: "15/08/2021",
        category: "owner_business",
        label: "Chủ bãi hộ kinh doanh (Trần Thị Bích)",
        demoAccount: "owner.biz@demo.local",
        description: "Chủ bãi hộ kinh doanh có MST cá thể",
    },
    {
        cccd: "038085123456",
        fullName: "LÊ VĂN CƯỜNG",
        dob: "1985-11-30",
        dobDisplay: "30/11/1985",
        gender: "M",
        genderDisplay: "Nam",
        nationality: "Việt Nam",
        hometown: "Thanh Hóa",
        residence: "Số 78 Trần Phú, Phường Điện Biên, TP. Thanh Hóa, Thanh Hóa",
        expiryDate: "30/11/2045",
        issueDate: "12/01/2022",
        category: "owner_business",
        label: "Đại diện Doanh nghiệp (Lê Văn Cường)",
        demoAccount: "owner.corp@demo.local",
        description: "Người đại diện pháp luật doanh nghiệp bãi đỗ xe",
    },
    {
        cccd: "000000000001",
        fullName: "TEST USER ONE",
        dob: "2000-01-01",
        dobDisplay: "01/01/2000",
        gender: "M",
        genderDisplay: "Nam",
        nationality: "Việt Nam",
        hometown: "TP. Hồ Chí Minh",
        residence:
            "Khu dân cư An Phú, Phường An Phú, TP. Thủ Đức, TP. Hồ Chí Minh",
        expiryDate: "01/01/2040",
        issueDate: "05/05/2021",
        category: "customer",
        label: "Khách hàng mẫu 1 (Test User One)",
        demoAccount: "customer@demo.local",
        description:
            "Khách hàng đặt chỗ & gửi xe (Biển số: 59X1-234.56, Flow D)",
    },
    {
        cccd: "000000000002",
        fullName: "TEST USER TWO",
        dob: "2001-06-15",
        dobDisplay: "15/06/2001",
        gender: "F",
        genderDisplay: "Nữ",
        nationality: "Việt Nam",
        hometown: "TP. Hồ Chí Minh",
        residence:
            "25 Đường số 8, Phường Linh Chiểu, TP. Thủ Đức, TP. Hồ Chí Minh",
        expiryDate: "15/06/2041",
        issueDate: "20/07/2021",
        category: "owner_individual",
        label: "Chủ bãi đã duyệt (Test User Two)",
        demoAccount: "owner@demo.local",
        description: "Tài khoản Chủ bãi đã có quyền OWNER quản lý các bãi đỗ",
    },
    {
        cccd: "000000000003",
        fullName: "TEST ADMIN USER",
        dob: "1995-08-20",
        dobDisplay: "20/08/1995",
        gender: "M",
        genderDisplay: "Nam",
        nationality: "Việt Nam",
        hometown: "Hà Nội",
        residence: "Tòa nhà Bộ Công An, 47 Phạm Văn Đồng, Cầu Giấy, Hà Nội",
        expiryDate: "20/08/2035",
        issueDate: "01/09/2021",
        category: "admin",
        label: "Quản trị viên (Test Admin User)",
        demoAccount: "admin@demo.local",
        description: "Tài khoản Quản trị hệ thống xét duyệt hồ sơ & bãi đỗ",
    },
    {
        cccd: "000000000004",
        fullName: "TEST STAFF USER",
        dob: "1998-12-10",
        dobDisplay: "10/12/1998",
        gender: "F",
        genderDisplay: "Nữ",
        nationality: "Việt Nam",
        hometown: "TP. Hồ Chí Minh",
        residence:
            "12 Điện Biên Phủ, Phường 15, Quận Bình Thạnh, TP. Hồ Chí Minh",
        expiryDate: "10/12/2038",
        issueDate: "10/10/2021",
        category: "staff",
        label: "Nhân viên bãi (Test Staff User)",
        demoAccount: "staff@demo.local",
        description: "Nhân viên trực cổng check-in / check-out bãi xe",
    },
    {
        cccd: "031190234567",
        fullName: "PHẠM THỊ DIỆU",
        dob: "1990-05-18",
        dobDisplay: "18/05/1990",
        gender: "F",
        genderDisplay: "Nữ",
        nationality: "Việt Nam",
        hometown: "Hải Phòng",
        residence: "42 Lạch Tray, Quận Ngô Quyền, TP. Hải Phòng",
        expiryDate: "18/05/2030",
        issueDate: "02/06/2021",
        category: "customer",
        label: "Khách hàng (Phạm Thị Diệu)",
        description: "Khách vãng lai đăng ký tài khoản mới",
    },
];

export function findProfileByCccd(
    cccd: string,
): VneidCitizenProfile | undefined {
    const clean = cccd.replace(/\s+/g, "");
    return VNEID_SEED_PROFILES.find((p) => p.cccd === clean);
}

export function generateVneidQrString(profile: VneidCitizenProfile): string {
    // Format standard QR code on CCCD gắn chip:
    // <Số CCCD>|<Số CMND cũ 9 số nếu có>|<Họ tên>|<Ngày sinh ddmmyyyy>|<Giới tính Nam/Nữ>|<Nơi thường trú>|<Ngày cấp ddmmyyyy>
    const dobCompact = profile.dob.split("-").reverse().join("");
    const issueCompact = profile.issueDate.split("/").join("");
    return `${profile.cccd}||${profile.fullName}|${dobCompact}|${profile.genderDisplay}|${profile.residence}|${issueCompact}`;
}
