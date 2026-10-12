export interface ParkingLotDetail {
    id: string;
    name: string;
    businessName: string;
    taxCode: string;
    licenseNumber: string;
    owner: {
        name: string;
        email: string;
        phone: string;
        nationalId: string;
        submittedAt: string;
    };
    category: "Underground Basement" | "Multi-Story Garage" | "Surface Open Lot";
    description: string;
    photos: {
        title: string;
        url: string;
        type: string;
    }[];
    legalDocuments: {
        name: string;
        fileSize: string;
        uploadedDate: string;
        status: "Verified" | "Pending" | "Attention";
        type: string;
    }[];
    location: {
        address: string;
        ward: string;
        district: string;
        city: string;
        latitude: number;
        longitude: number;
        accessNotes: string;
        mapEmbedUrl?: string;
    };
    operatingHours: {
        is24x7: boolean;
        regularDays: string;
        openTime: string;
        closeTime: string;
        peakHours: string[];
        holidayPolicy: string;
        overnightAllowed: boolean;
    };
    vehicleTypes: {
        type: string;
        iconName: string;
        allowed: boolean;
        maxDimensions: string;
        maxHeight: string;
        notes: string;
    }[];
    capacity: {
        total: number;
        carStandard: number;
        carElectric: number;
        motorcycles: number;
        accessible: number;
        currentOccupiedSample?: number;
    };
    zones: {
        id: string;
        name: string;
        floor: string;
        slotsCount: number;
        supportedTypes: string[];
        features: string[];
        status: "Active" | "Maintenance" | "Ready";
    }[];
    pricing: {
        currency: string;
        firstTwoHours: number;
        additionalHour: number;
        overnightFlatRate: number;
        fullDayPass: number;
        monthlySubscription?: number;
        evChargingKwh?: number;
        weekendSurchargePercent: number;
        lostTicketFee: number;
    };
    deposit: {
        required: boolean;
        holdAmount: number;
        holdDurationMinutes: number;
        autoReleaseMinutes: number;
        refundGraceMinutes: number;
        refundTerms: string;
    };
    reservationPolicy: {
        minAdvanceNoticeMinutes: number;
        maxAdvanceDays: number;
        gracePeriodMinutes: number;
        cancellationPolicy: string;
        overstayPenaltyRate: string;
        noShowPenalty: string;
    };
    approvalStatus: "Pending Review" | "Under Review" | "Action Required" | "Approved" | "Rejected";
}

export const PARKING_LOTS_DATA: Record<string, ParkingLotDetail> = {
    "PL-001": {
        id: "PL-001",
        name: "Downtown Central Parking",
        businessName: "Saigon Urban Mobility JSC",
        taxCode: "0314892110",
        licenseNumber: "GP-GTVT-2026/088",
        owner: {
            name: "Nguyen Van A",
            email: "vana.nguyen@example.com",
            phone: "+84 987 654 321",
            nationalId: "079089012345",
            submittedAt: "Oct 10, 2026, 14:30 GMT+7",
        },
        category: "Underground Basement",
        description: "Modern 2-level basement parking facility under the Financial Tower with automated license plate recognition (ALPR), 60kW DC ultra-fast EV charging hubs, and 24/7 CCTV surveillance.",
        photos: [
            {
                title: "Main Entrance & LPR Barrier",
                url: "https://images.unsplash.com/photo-1590674899484-d5640e854abe?auto=format&fit=crop&w=800&q=80",
                type: "Entrance"
            },
            {
                title: "Basement B1 Driving Lane & Stalls",
                url: "https://images.unsplash.com/photo-1506521781263-d8422e82f27a?auto=format&fit=crop&w=800&q=80",
                type: "Interior"
            },
            {
                title: "EV Fast Charging Station Hub",
                url: "https://images.unsplash.com/photo-1563986768609-322da13575f3?auto=format&fit=crop&w=800&q=80",
                type: "Facilities"
            },
            {
                title: "Exit Ramp & Automated Pay Kiosk",
                url: "https://images.unsplash.com/photo-1573348722427-f1d6819fdf98?auto=format&fit=crop&w=800&q=80",
                type: "Exit"
            }
        ],
        legalDocuments: [
            {
                name: "Business_Registration_Certificate_2026.pdf",
                fileSize: "2.4 MB",
                uploadedDate: "10/10/2026",
                status: "Verified",
                type: "Enterprise Legal"
            },
            {
                name: "Fire_Safety_PCCC_Approval_Certificate.pdf",
                fileSize: "4.8 MB",
                uploadedDate: "10/10/2026",
                status: "Verified",
                type: "Fire Safety"
            },
            {
                name: "Land_Use_Lease_Agreement_Certified.pdf",
                fileSize: "6.1 MB",
                uploadedDate: "09/10/2026",
                status: "Verified",
                type: "Land Property"
            },
            {
                name: "Environmental_Impact_Assessment.pdf",
                fileSize: "1.9 MB",
                uploadedDate: "09/10/2026",
                status: "Pending",
                type: "Compliance"
            }
        ],
        location: {
            address: "68 Nguyen Hue Boulevard, Ben Nghe Ward",
            ward: "Ben Nghe Ward",
            district: "District 1",
            city: "Ho Chi Minh City",
            latitude: 10.774421,
            longitude: 106.703215,
            accessNotes: "Direct entrance from Nguyen Hue pedestrian street side alley or Ton Duc Thang ramp. Two-way ramp with 15% slope.",
        },
        operatingHours: {
            is24x7: true,
            regularDays: "Monday - Sunday (7 days/week)",
            openTime: "00:00",
            closeTime: "23:59",
            peakHours: ["07:30 - 09:30", "11:45 - 13:30", "17:00 - 19:30"],
            holidayPolicy: "Open normally throughout public holidays with standard holiday surcharge.",
            overnightAllowed: true,
        },
        vehicleTypes: [
            {
                type: "Sedan / Hatchback (4-5 seats)",
                iconName: "car",
                allowed: true,
                maxDimensions: "4.9m L x 1.9m W",
                maxHeight: "2.1m",
                notes: "Standard basement stalls"
            },
            {
                type: "SUV / Crossover (7 seats)",
                iconName: "suv",
                allowed: true,
                maxDimensions: "5.2m L x 2.0m W",
                maxHeight: "2.1m",
                notes: "Recommended in Zone B & C wide stalls"
            },
            {
                type: "Electric Vehicle (EV)",
                iconName: "ev",
                allowed: true,
                maxDimensions: "5.0m L x 2.0m W",
                maxHeight: "2.1m",
                notes: "Designated charging ports in Zone A (CCS2 & Type 2)"
            },
            {
                type: "Motorcycle / Scooter",
                iconName: "bike",
                allowed: true,
                maxDimensions: "2.2m L x 0.9m W",
                maxHeight: "N/A",
                notes: "Dedicated ramp entrance on right side"
            },
            {
                type: "Commercial Van / Box Truck (>2.2m)",
                iconName: "truck",
                allowed: false,
                maxDimensions: "Exceeds clearance",
                maxHeight: "> 2.2m",
                notes: "Prohibited due to low ceiling pipes"
            }
        ],
        capacity: {
            total: 350,
            carStandard: 180,
            carElectric: 30,
            motorcycles: 130,
            accessible: 10,
            currentOccupiedSample: 214,
        },
        zones: [
            {
                id: "ZA",
                name: "Zone A - VIP & EV Rapid Charge",
                floor: "Basement B1 (Near Elevator Hall)",
                slotsCount: 50,
                supportedTypes: ["Electric Vehicle", "VIP Cars", "Accessible"],
                features: ["60kW DC Charger", "CCTV direct zoom", "Wide 2.7m bays"],
                status: "Ready"
            },
            {
                id: "ZB",
                name: "Zone B - Standard Car Deck",
                floor: "Basement B1 (Central Core)",
                slotsCount: 110,
                supportedTypes: ["Sedan", "SUV", "Crossover"],
                features: ["Overhead ultrasonic sensors", "LED bay availability guidance"],
                status: "Ready"
            },
            {
                id: "ZC",
                name: "Zone C - Long-Term & Overflow Cars",
                floor: "Basement B2 (East Wing)",
                slotsCount: 60,
                supportedTypes: ["Standard Sedan", "SUV"],
                features: ["Reserved long-term bays", "Low-traffic area"],
                status: "Ready"
            },
            {
                id: "ZD",
                name: "Zone D - Smart Motorcycle Vault",
                floor: "Basement B2 (West Wing)",
                slotsCount: 130,
                supportedTypes: ["Motorcycles", "Electric Scooters"],
                features: ["RFID barrier turnstile", "Helmet locker station"],
                status: "Ready"
            }
        ],
        pricing: {
            currency: "VND",
            firstTwoHours: 30000,
            additionalHour: 20000,
            overnightFlatRate: 120000,
            fullDayPass: 200000,
            monthlySubscription: 2500000,
            evChargingKwh: 3850,
            weekendSurchargePercent: 15,
            lostTicketFee: 250000,
        },
        deposit: {
            required: true,
            holdAmount: 25000,
            holdDurationMinutes: 30,
            autoReleaseMinutes: 30,
            refundGraceMinutes: 15,
            refundTerms: "100% deposit refunded if cancelled at least 15 minutes prior to scheduled reservation time. In case of no-show after 30 minutes grace holding, deposit is forfeited to compensate the facility.",
        },
        reservationPolicy: {
            minAdvanceNoticeMinutes: 15,
            maxAdvanceDays: 7,
            gracePeriodMinutes: 20,
            cancellationPolicy: "Free cancellation up to 15 mins before booking start. 50% cancellation fee between 15 mins to start time.",
            overstayPenaltyRate: "1.5x regular hourly tariff applied automatically for any unannounced overstay past booked slot checkout.",
            noShowPenalty: "Reservation canceled automatically after 30 mins late; hold deposit forfeited without refund.",
        },
        approvalStatus: "Pending Review",
    },
    "PL-002": {
        id: "PL-002",
        name: "Vincom Mega Mall Basement",
        businessName: "V-Retail Asset Management",
        taxCode: "0102983774",
        licenseNumber: "GP-BXD-2025/112",
        owner: {
            name: "Tran Thi B",
            email: "thib.tran@example.com",
            phone: "+84 912 345 678",
            nationalId: "001192004812",
            submittedAt: "Oct 09, 2026, 09:15 GMT+7",
        },
        category: "Underground Basement",
        description: "Commercial multi-zone basement serving shopping center visitors with high capacity, automated toll gates, and integrated mall guidance screens.",
        photos: [
            {
                title: "Mall Basement Ramp Entrance",
                url: "https://images.unsplash.com/photo-1590674899484-d5640e854abe?auto=format&fit=crop&w=800&q=80",
                type: "Entrance"
            },
            {
                title: "Basement Area Overview",
                url: "https://images.unsplash.com/photo-1506521781263-d8422e82f27a?auto=format&fit=crop&w=800&q=80",
                type: "Interior"
            }
        ],
        legalDocuments: [
            {
                name: "Commercial_Mall_Fire_Permit.pdf",
                fileSize: "3.5 MB",
                uploadedDate: "09/10/2026",
                status: "Verified",
                type: "Safety"
            },
            {
                name: "Business_License_VRetail.pdf",
                fileSize: "1.2 MB",
                uploadedDate: "09/10/2026",
                status: "Verified",
                type: "Legal"
            }
        ],
        location: {
            address: "159 Hanoi Highway, Thao Dien Ward",
            ward: "Thao Dien Ward",
            district: "Thu Duc City",
            city: "Ho Chi Minh City",
            latitude: 10.802114,
            longitude: 106.738924,
            accessNotes: "Direct ramp from frontage highway. Height clearance 2.3m.",
        },
        operatingHours: {
            is24x7: false,
            regularDays: "Monday - Sunday",
            openTime: "06:00",
            closeTime: "23:00",
            peakHours: ["11:00 - 13:00", "18:00 - 21:30"],
            holidayPolicy: "Open until 24:00 on festive occasions.",
            overnightAllowed: false,
        },
        vehicleTypes: [
            {
                type: "Sedan / Hatchback (4-5 seats)",
                iconName: "car",
                allowed: true,
                maxDimensions: "5.0m L x 2.0m W",
                maxHeight: "2.3m",
                notes: "All mall zones"
            },
            {
                type: "Motorcycle / Scooter",
                iconName: "bike",
                allowed: true,
                maxDimensions: "2.2m L x 0.9m W",
                maxHeight: "N/A",
                notes: "Zone M level"
            }
        ],
        capacity: {
            total: 600,
            carStandard: 350,
            carElectric: 50,
            motorcycles: 190,
            accessible: 10,
            currentOccupiedSample: 410,
        },
        zones: [
            {
                id: "ZA",
                name: "Zone A - Supermarket Direct",
                floor: "Basement B1",
                slotsCount: 150,
                supportedTypes: ["Cars", "Electric"],
                features: ["Elevator to WinMart", "EV Stalls"],
                status: "Active"
            },
            {
                id: "ZB",
                name: "Zone B - Cinema & Dining Access",
                floor: "Basement B2",
                slotsCount: 250,
                supportedTypes: ["Cars"],
                features: ["Direct Cinema lift"],
                status: "Ready"
            }
        ],
        pricing: {
            currency: "VND",
            firstTwoHours: 25000,
            additionalHour: 15000,
            overnightFlatRate: 100000,
            fullDayPass: 180000,
            weekendSurchargePercent: 20,
            lostTicketFee: 200000,
        },
        deposit: {
            required: true,
            holdAmount: 20000,
            holdDurationMinutes: 30,
            autoReleaseMinutes: 30,
            refundGraceMinutes: 15,
            refundTerms: "Refundable up to 15m prior to schedule.",
        },
        reservationPolicy: {
            minAdvanceNoticeMinutes: 20,
            maxAdvanceDays: 3,
            gracePeriodMinutes: 15,
            cancellationPolicy: "Standard cancellation policy applied.",
            overstayPenaltyRate: "1.5x regular hourly tariff.",
            noShowPenalty: "Hold deposit retained.",
        },
        approvalStatus: "Under Review",
    },
    "PL-003": {
        id: "PL-003",
        name: "Tech Hub Secure Parking",
        businessName: "Silicon Park Real Estate Ltd",
        taxCode: "0316554902",
        licenseNumber: "GP-SHTP-2026/019",
        owner: {
            name: "Le Minh C",
            email: "minhc.le@example.com",
            phone: "+84 905 111 222",
            nationalId: "048092003184",
            submittedAt: "Oct 08, 2026, 16:45 GMT+7",
        },
        category: "Multi-Story Garage",
        description: "5-level smart robotic parking building situated in High-Tech Innovation Park with solar canopy rooftop and automated vehicle elevators.",
        photos: [
            {
                title: "Exterior 5-Story Steel Structure",
                url: "https://images.unsplash.com/photo-1590674899484-d5640e854abe?auto=format&fit=crop&w=800&q=80",
                type: "Exterior"
            }
        ],
        legalDocuments: [
            {
                name: "HighTech_Park_Operation_Permit.pdf",
                fileSize: "2.1 MB",
                uploadedDate: "08/10/2026",
                status: "Verified",
                type: "Permit"
            }
        ],
        location: {
            address: "D1 Street, Saigon Hi-Tech Park, Tang Nhon Phu B",
            ward: "Tang Nhon Phu B",
            district: "Thu Duc City",
            city: "Ho Chi Minh City",
            latitude: 10.852319,
            longitude: 106.786521,
            accessNotes: "Wide turning loop for automated lift entrance.",
        },
        operatingHours: {
            is24x7: true,
            regularDays: "Monday - Sunday",
            openTime: "00:00",
            closeTime: "23:59",
            peakHours: ["08:00 - 09:30", "17:30 - 19:00"],
            holidayPolicy: "Open 24/7 all holidays.",
            overnightAllowed: true,
        },
        vehicleTypes: [
            {
                type: "Sedan / Electric Vehicle",
                iconName: "car",
                allowed: true,
                maxDimensions: "5.1m L x 2.0m W",
                maxHeight: "2.0m",
                notes: "Automated lift dimension limits"
            }
        ],
        capacity: {
            total: 220,
            carStandard: 160,
            carElectric: 40,
            motorcycles: 20,
            accessible: 0,
        },
        zones: [
            {
                id: "ZA",
                name: "Level 1 to Level 5 Automated Stacks",
                floor: "Tower 1",
                slotsCount: 220,
                supportedTypes: ["Standard", "EV"],
                features: ["Robotic pallet transporter"],
                status: "Ready"
            }
        ],
        pricing: {
            currency: "VND",
            firstTwoHours: 20000,
            additionalHour: 15000,
            overnightFlatRate: 80000,
            fullDayPass: 150000,
            weekendSurchargePercent: 0,
            lostTicketFee: 200000,
        },
        deposit: {
            required: true,
            holdAmount: 20000,
            holdDurationMinutes: 45,
            autoReleaseMinutes: 45,
            refundGraceMinutes: 20,
            refundTerms: "Free cancellation prior to 20 minutes.",
        },
        reservationPolicy: {
            minAdvanceNoticeMinutes: 10,
            maxAdvanceDays: 14,
            gracePeriodMinutes: 20,
            cancellationPolicy: "Flexible cancellation for tech park tenants.",
            overstayPenaltyRate: "1.2x regular tariff.",
            noShowPenalty: "Deposit forfeit.",
        },
        approvalStatus: "Pending Review",
    },
    "PL-004": {
        id: "PL-004",
        name: "District 1 Open Lot",
        businessName: "Saigon Land Prime Co.",
        taxCode: "0311223344",
        licenseNumber: "GP-TNMT-2026/041",
        owner: {
            name: "Pham Van D",
            email: "vand.pham@example.com",
            phone: "+84 933 444 555",
            nationalId: "079078009988",
            submittedAt: "Oct 08, 2026, 11:20 GMT+7",
        },
        category: "Surface Open Lot",
        description: "Gravel and asphalt surface open lot near Ben Thanh Market pending permanent high-rise construction, requesting temporary permit.",
        photos: [
            {
                title: "Surface Lot Front Gate",
                url: "https://images.unsplash.com/photo-1590674899484-d5640e854abe?auto=format&fit=crop&w=800&q=80",
                type: "Exterior"
            }
        ],
        legalDocuments: [
            {
                name: "Temporary_Land_Use_Approval.pdf",
                fileSize: "1.5 MB",
                uploadedDate: "08/10/2026",
                status: "Attention",
                type: "Temporary Permit"
            }
        ],
        location: {
            address: "45 Le Thi Hong Gam, Nguyen Thai Binh Ward",
            ward: "Nguyen Thai Binh Ward",
            district: "District 1",
            city: "Ho Chi Minh City",
            latitude: 10.769912,
            longitude: 106.697854,
            accessNotes: "Direct curb cut entrance.",
        },
        operatingHours: {
            is24x7: false,
            regularDays: "Monday - Sunday",
            openTime: "06:00",
            closeTime: "22:00",
            peakHours: ["08:00 - 10:00", "16:00 - 18:30"],
            holidayPolicy: "Open daytime only.",
            overnightAllowed: false,
        },
        vehicleTypes: [
            {
                type: "Sedan / SUV / Small Truck",
                iconName: "car",
                allowed: true,
                maxDimensions: "Open sky",
                maxHeight: "No height limit",
                notes: "Open air lot"
            }
        ],
        capacity: {
            total: 90,
            carStandard: 70,
            carElectric: 0,
            motorcycles: 20,
            accessible: 0,
        },
        zones: [
            {
                id: "ZA",
                name: "Open Yard Bay 1-70",
                floor: "Ground Surface",
                slotsCount: 90,
                supportedTypes: ["Cars", "Motorbikes"],
                features: ["Manual guard post"],
                status: "Maintenance"
            }
        ],
        pricing: {
            currency: "VND",
            firstTwoHours: 40000,
            additionalHour: 25000,
            overnightFlatRate: 0,
            fullDayPass: 250000,
            weekendSurchargePercent: 25,
            lostTicketFee: 300000,
        },
        deposit: {
            required: false,
            holdAmount: 0,
            holdDurationMinutes: 0,
            autoReleaseMinutes: 0,
            refundGraceMinutes: 0,
            refundTerms: "No reservation deposit supported for surface lot.",
        },
        reservationPolicy: {
            minAdvanceNoticeMinutes: 30,
            maxAdvanceDays: 1,
            gracePeriodMinutes: 10,
            cancellationPolicy: "Strict cancellation.",
            overstayPenaltyRate: "2.0x regular tariff.",
            noShowPenalty: "Account flagged.",
        },
        approvalStatus: "Action Required",
    }
};

export function getParkingLotById(id: string): ParkingLotDetail {
    return PARKING_LOTS_DATA[id] || {
        ...PARKING_LOTS_DATA["PL-001"],
        id: id,
        name: `Parking Facility #${id}`,
    };
}
