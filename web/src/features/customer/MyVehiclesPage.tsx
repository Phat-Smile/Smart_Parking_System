"use client";

import {
    useEffect,
    useRef,
    useState,
    useSyncExternalStore,
    type FormEvent,
    type ReactNode,
} from "react";
import Image from "next/image";
import { ParkWiseNavbar } from "@/features/parkwise/ParkWiseNavbar";
import { CustomerLanguageSwitcher } from "./i18n/CustomerLanguageSwitcher";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";
import { CustomerAccountMenu, CustomerNotifications } from "./CustomerShell";
import { VehicleDropdown } from "./VehicleDropdown";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import {
    addMyVehicle,
    deleteMyVehicle,
    getMyVehicles,
    updateMyVehicle,
    type RegisteredVehicle,
    type VehicleDetails,
} from "./myVehiclesApi";
import { uploadCustomerMedia } from "./customerProfileApi";
import { formatVehiclePlate, validateVehiclePlate } from "./vehiclePlate";
import { vehicleAddErrorMessage } from "./vehicleAddError";
import {
    BRAND_OPTIONS,
    COLOR_OPTIONS,
    COLOR_SWATCHES,
    FUEL_OPTIONS,
    getModelOptions,
    getMotorbikeModelOptions,
    MOTORBIKE_BRAND_OPTIONS,
    MOTORBIKE_FUEL_OPTIONS,
    MOTORBIKE_TYPE_OPTIONS,
    SEAT_OPTIONS,
    VEHICLE_TYPE_OPTIONS,
    YEAR_OPTIONS,
} from "./vehicleOptions";

type Vehicle = {
    id: string;
    category: VehicleCategory;
    plate: string;
    brand: string;
    nickname?: string;
    model?: string;
    vehicleType?: string;
    seats?: string;
    fuelType?: string;
    color?: string;
    manufacturingYear?: string;
    size: "standard" | "large";
    photo?: string;
    details?: VehicleDetails | null;
    draft?: boolean;
    isDefault?: boolean;
};
type VehicleCategory =
    "motorbike" | "electric-motorbike" | "bicycle" | "car" | "electric-car";
const vehicleLabelsVi: Record<string, string> = {
    Motorbike: "Xe máy",
    "Electric motorbike": "Xe máy điện",
    Bicycle: "Xe đạp",
    Car: "Ô tô",
    "Electric car": "Ô tô điện",
    "Motorbike area": "Khu xe máy",
    "Electric motorbike area": "Khu xe máy điện",
    "Bicycle area": "Khu xe đạp",
    "Car area": "Khu ô tô",
    "Electric car area": "Khu ô tô điện",
    "Two-wheelers": "Xe hai bánh",
    "Motorbike or bicycle": "Xe máy hoặc xe đạp",
    "Fuel or electric": "Xăng hoặc điện",
    Nickname: "Tên gợi nhớ",
    "Vehicle Brand": "Hãng xe",
    "Vehicle Model": "Dòng xe",
    "Motorbike Type": "Loại xe máy",
    "Body Type": "Kiểu thân xe",
    Seats: "Số ghế",
    "Fuel type": "Loại nhiên liệu",
    Color: "Màu sắc",
    Year: "Năm sản xuất",
};
function vehicleLabelVi(label: string) {
    return vehicleLabelsVi[label] ?? label;
}
const VEHICLE_CATEGORIES: {
    value: VehicleCategory;
    label: string;
    description: string;
    icon: string;
}[] = [
    {
        value: "motorbike",
        label: "Motorbike",
        description: "Motorbike area",
        icon: "motorbike",
    },
    {
        value: "electric-motorbike",
        label: "Electric motorbike",
        description: "Electric motorbike area",
        icon: "motorbike",
    },
    {
        value: "bicycle",
        label: "Bicycle",
        description: "Bicycle area",
        icon: "bicycle",
    },
    { value: "car", label: "Car", description: "Car area", icon: "car" },
    {
        value: "electric-car",
        label: "Electric car",
        description: "Electric car area",
        icon: "car",
    },
];
const VEHICLE_FORM_CATEGORIES = VEHICLE_CATEGORIES.filter(
    (item) => item.value === "motorbike" || item.value === "car",
).map((item) => ({
    ...item,
    label: item.value === "motorbike" ? "Two-wheelers" : item.label,
    description:
        item.value === "motorbike"
            ? "Motorbike or bicycle"
            : "Fuel or electric",
}));
function categoryInfo(category: VehicleCategory) {
    return VEHICLE_CATEGORIES.find((item) => item.value === category)!;
}
function baseCategory(category: VehicleCategory): VehicleCategory {
    if (category === "electric-motorbike" || category === "bicycle")
        return "motorbike";
    if (category === "electric-car") return "car";
    return category;
}
function isMotorbikeCategory(category: VehicleCategory) {
    return category === "motorbike" || category === "electric-motorbike";
}
function isCarCategory(category: VehicleCategory) {
    return category === "car" || category === "electric-car";
}
function needsPlate(category: VehicleCategory) {
    return category !== "bicycle";
}
function plateGroup(category: VehicleCategory) {
    return category === "bicycle"
        ? ("Bicycle" as const)
        : isCarCategory(category)
          ? ("Car" as const)
          : ("Motorbike" as const);
}
function defaultVehicleImage(category: VehicleCategory) {
    if (category === "bicycle")
        return "/images/parkwise/vehicle-bicycle-cutout.png";
    if (category === "electric-motorbike")
        return "/images/parkwise/vehicle-electric-motorbike-cutout.png";
    if (category === "motorbike")
        return "/images/parkwise/vehicle-motorbike-cutout.png";
    if (category === "electric-car")
        return "/images/parkwise/vehicle-electric-car-cutout.png";
    return "/images/parkwise/vehicle-sedan-cutout.png";
}
function VehiclePhoto({
    category,
    photo,
    alt,
    sizes,
}: {
    category: VehicleCategory;
    photo?: string;
    alt: string;
    sizes: string;
}) {
    const fallback = defaultVehicleImage(category);
    const [failedPhoto, setFailedPhoto] = useState<string | null>(null);
    const source = photo && photo !== failedPhoto ? photo : fallback;
    return (
        <Image
            src={source}
            alt={alt}
            fill
            sizes={sizes}
            unoptimized={source.startsWith("data:")}
            onError={() => {
                if (photo && source === photo) setFailedPhoto(photo);
            }}
        />
    );
}
type Modal = "add" | "edit" | "delete" | null;
const MAX_VEHICLES = 10;

function formatPlate(value: string, category: VehicleCategory) {
    return formatVehiclePlate(value, plateGroup(category));
}

function getVehicleDetails(vehicle: Vehicle) {
    return [
        vehicle.nickname
            ? { label: "Nickname", value: vehicle.nickname, icon: "tag" }
            : null,
        vehicle.brand
            ? {
                  label: "Vehicle Brand",
                  value: vehicle.brand,
                  icon: "tag",
              }
            : null,
        vehicle.model?.trim()
            ? {
                  label: "Vehicle Model",
                  value: vehicle.model.trim(),
                  icon: isMotorbikeCategory(vehicle.category)
                      ? "motorbike"
                      : "car",
              }
            : null,
        vehicle.vehicleType
            ? {
                  label: isMotorbikeCategory(vehicle.category)
                      ? "Motorbike type"
                      : "Body type",
                  icon: isMotorbikeCategory(vehicle.category)
                      ? "motorbike"
                      : "car",
                  value:
                      (isMotorbikeCategory(vehicle.category)
                          ? MOTORBIKE_TYPE_OPTIONS
                          : VEHICLE_TYPE_OPTIONS
                      ).find((option) => option.value === vehicle.vehicleType)
                          ?.label ?? vehicle.vehicleType,
              }
            : null,
        vehicle.seats
            ? { label: "Seats", value: vehicle.seats, icon: "seats" }
            : null,
        vehicle.fuelType && vehicle.fuelType !== "electric"
            ? {
                  label: "Fuel type",
                  icon: "fuel",
                  value:
                      (isMotorbikeCategory(vehicle.category)
                          ? MOTORBIKE_FUEL_OPTIONS
                          : FUEL_OPTIONS
                      ).find((option) => option.value === vehicle.fuelType)
                          ?.label ?? vehicle.fuelType,
              }
            : null,
        vehicle.color
            ? {
                  label: "Color",
                  icon: "palette",
                  value:
                      COLOR_OPTIONS.find(
                          (option) => option.value === vehicle.color,
                      )?.label ?? vehicle.color,
              }
            : null,
        vehicle.manufacturingYear
            ? {
                  label: "Year",
                  value: vehicle.manufacturingYear,
                  icon: "calendar",
              }
            : null,
    ].filter(
        (detail): detail is { label: string; value: string; icon: string } =>
            Boolean(detail),
    );
}
const sampleVehicles: Vehicle[] = [
    {
        id: "sample-sedan",
        category: "electric-car",
        plate: "51K-123.45",
        brand: "Toyota",
        vehicleType: "sedan",
        seats: "5",
        fuelType: "electric",
        color: "white",
        manufacturingYear: "2023",
        size: "standard",
        photo: "/images/parkwise/vehicle-sedan-cutout.png",
    },
    {
        id: "sample-suv",
        category: "car",
        plate: "30A-987.65",
        brand: "Toyota",
        vehicleType: "suv",
        seats: "7",
        fuelType: "gasoline",
        color: "white",
        manufacturingYear: "2022",
        size: "large",
        photo: "/images/parkwise/vehicle-suv.png",
    },
    {
        id: "sample-motorbike",
        category: "motorbike",
        plate: "59A1-123.45",
        brand: "Honda",
        model: "Vision",
        vehicleType: "scooter",
        fuelType: "gasoline",
        color: "white",
        manufacturingYear: "2024",
        size: "standard",
        photo: "/images/parkwise/vehicle-motorbike-cutout.png",
    },
];

function fromRegisteredVehicle(
    vehicle: Omit<RegisteredVehicle, "createdAtUtc">,
): Vehicle {
    const details = vehicle.details;
    const electric = details?.fuelType === "electric";
    return {
        id: vehicle.vehicleId,
        category:
            vehicle.vehicleType === "Bicycle"
                ? "bicycle"
                : vehicle.vehicleType === "Motorbike"
                  ? electric
                      ? "electric-motorbike"
                      : "motorbike"
                  : electric
                    ? "electric-car"
                    : "car",
        plate: vehicle.licensePlate ?? "",
        brand: details?.brand ?? "",
        nickname: vehicle.nickName ?? "",
        model: details?.model ?? "",
        vehicleType: details?.motorbikeKind ?? "",
        seats: details?.seats?.toString() ?? "",
        fuelType: details?.fuelType ?? "",
        color: details?.color ?? "",
        manufacturingYear: details?.manufacturingYear?.toString() ?? "",
        size: details?.size === "large" ? "large" : "standard",
        photo:
            details?.photoUrl ??
            defaultVehicleImage(
                vehicle.vehicleType === "Bicycle"
                    ? "bicycle"
                    : vehicle.vehicleType === "Motorbike"
                      ? electric
                          ? "electric-motorbike"
                          : "motorbike"
                      : electric
                        ? "electric-car"
                        : "car",
            ),
        details,
        isDefault: vehicle.isDefault,
    };
}

function Icon({ name, size = 20 }: { name: string; size?: number }) {
    const paths: Record<string, ReactNode> = {
        plus: <path d="M12 5v14M5 12h14" />,
        close: <path d="M5 5l14 14M19 5 5 19" />,
        arrow: <path d="M4 12h16m-6-6 6 6-6 6" />,
        car: (
            <>
                <path d="m5 11 1.7-4.2A2 2 0 0 1 8.6 5h6.8a2 2 0 0 1 1.9 1.8L19 11" />
                <rect x="3" y="11" width="18" height="7" rx="2" />
                <path d="M5 18v2m14-2v2M7 14h.01M17 14h.01" />
            </>
        ),
        motorbike: (
            <>
                <circle cx="5.5" cy="17.5" r="3.5" />
                <circle cx="18.5" cy="17.5" r="3.5" />
                <path d="m8.5 17.5 3-7h3l4 7M8 9h3m1.5 1.5 2.5-3h2m-5.5 10h4" />
            </>
        ),
        bicycle: (
            <>
                <circle cx="5.5" cy="17" r="3.5" />
                <circle cx="18.5" cy="17" r="3.5" />
                <path d="m5.5 17 4-9h4l5 9M9.5 8l5 9m-9 0h9M8 6h3m3 0h3" />
            </>
        ),
        book: (
            <>
                <path d="M12 6c-2-1.5-4.7-1.6-8-1v13c3.3-.6 6-.5 8 1 2-1.5 4.7-1.6 8-1V5c-3.3-.6-6-.5-8 1Z" />
                <path d="M12 6v13" />
            </>
        ),
        bolt: <path d="m13 2-8 11h6l-1 9 9-12h-6V2Z" />,
        upload: (
            <>
                <path d="M12 16V3m-5 5 5-5 5 5" />
                <path d="M4 15v5h16v-5" />
            </>
        ),
        seats: (
            <>
                <circle cx="8" cy="8" r="2" />
                <circle cx="17" cy="8" r="2" />
                <path d="M2.5 18c0-3 2-5 5.5-5s5.5 2 5.5 5m-1-4c1-.7 2.5-1 4.5-1 3 0 5 2 5 5" />
            </>
        ),
        fuel: (
            <>
                <rect x="4" y="4" width="10" height="17" rx="1" />
                <path d="M7 8h4m3 1h2l3 3v6a2 2 0 0 0 4 0v-7l-3-3" />
            </>
        ),
        calendar: (
            <>
                <rect x="3" y="5" width="18" height="16" rx="2" />
                <path d="M7 3v4m10-4v4M3 10h18m-12 4h.01M14 14h.01M9 18h.01" />
            </>
        ),
        tag: (
            <>
                <path d="M4 4h8l8 8-8 8-8-8V4Z" />
                <circle cx="8.5" cy="8.5" r="1" />
            </>
        ),
        palette: (
            <>
                <path d="M12 3a9 9 0 0 0 0 18h1a2 2 0 0 0 1.7-3 1.5 1.5 0 0 1 1.3-2.3h2A3 3 0 0 0 21 12a9 9 0 0 0-9-9Z" />
                <path d="M7 11h.01M9 7.5h.01M14 7h.01M17 10h.01" />
            </>
        ),
        info: (
            <>
                <circle cx="12" cy="12" r="9" />
                <path d="M12 11v5m0-8h.01" />
            </>
        ),
        document: (
            <>
                <path d="M6 3h8l4 4v14H6z" />
                <path d="M14 3v5h4M9 12h6m-6 4h6" />
            </>
        ),
        headset: (
            <>
                <path d="M4 13V11a8 8 0 0 1 16 0v2" />
                <rect x="3" y="12" width="4" height="7" rx="2" />
                <rect x="17" y="12" width="4" height="7" rx="2" />
                <path d="M19 19c0 2-2 3-5 3h-2" />
            </>
        ),
        edit: (
            <>
                <path d="m4 20 4.5-1 10-10-3.5-3.5-10 10L4 20Z" />
                <path d="m13.5 7 3.5 3.5" />
            </>
        ),
        trash: (
            <>
                <path d="M4 7h16M9 7V4h6v3m3 0-1 13H7L6 7m4 4v6m4-6v6" />
            </>
        ),
        camera: (
            <>
                <path d="M4 7h4l1.5-2h5L16 7h4v12H4z" />
                <circle cx="12" cy="13" r="3" />
            </>
        ),
    };
    return (
        <svg
            width={size}
            height={size}
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            {paths[name]}
        </svg>
    );
}

function TwoWheelerTypeSelector({
    category,
    disabled,
    onChange,
}: {
    category: VehicleCategory;
    disabled: boolean;
    onChange: (kind: "motorbike" | "electric-motorbike" | "bicycle") => void;
}) {
    const { copy } = useCustomerLanguage();
    if (baseCategory(category) !== "motorbike") return null;
    const options = ["motorbike", "electric-motorbike", "bicycle"] as const;
    return (
        <fieldset className="vehicles-size-field vehicles-two-wheeler-field">
            <legend>{copy("Two-wheeler type", "Loại xe hai bánh")}</legend>
            <div className="vehicles-size-options">
                {options.map((kind) => {
                    const info = categoryInfo(kind);
                    return (
                        <label
                            key={kind}
                            className={`vehicles-size-option ${category === kind ? "selected" : ""}`}
                        >
                            <input
                                type="radio"
                                name="vehicles-two-wheeler-type"
                                checked={category === kind}
                                disabled={disabled}
                                onChange={() => onChange(kind)}
                            />
                            <span className="vehicles-size-indicator" />
                            <Icon name={info.icon} size={21} />
                            <strong>
                                {copy(info.label, vehicleLabelVi(info.label))}
                            </strong>
                        </label>
                    );
                })}
            </div>
        </fieldset>
    );
}

function PowerSourceSelector({
    category,
    disabled,
    onChange,
}: {
    category: VehicleCategory;
    disabled: boolean;
    onChange: (electric: boolean) => void;
}) {
    const { copy } = useCustomerLanguage();
    if (!isCarCategory(category)) return null;
    const electric = category === "electric-car";
    return (
        <fieldset className="vehicles-size-field vehicles-power-field">
            <legend>{copy("Power source", "Nguồn năng lượng")}</legend>
            <div className="vehicles-size-options">
                {[
                    { value: false, label: "Fuel / hybrid", icon: "fuel" },
                    { value: true, label: "Electric", icon: "bolt" },
                ].map((option) => (
                    <label
                        key={option.label}
                        className={`vehicles-size-option ${electric === option.value ? "selected" : ""}`}
                    >
                        <input
                            type="radio"
                            name="vehicles-power-source"
                            checked={electric === option.value}
                            disabled={disabled}
                            onChange={() => onChange(option.value)}
                        />
                        <span className="vehicles-size-indicator" />
                        <Icon name={option.icon} size={23} />
                        <strong>
                            {copy(
                                option.label,
                                option.value ? "Điện" : "Xăng / hybrid",
                            )}
                        </strong>
                    </label>
                ))}
            </div>
        </fieldset>
    );
}

export function MyVehiclesPage({ fontClassName }: { fontClassName: string }) {
    const { language, copy } = useCustomerLanguage();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const [vehicles, setVehicles] = useState<Vehicle[]>(sampleVehicles);
    const [loadedAccountId, setLoadedAccountId] = useState<string | null>(null);
    const [modal, setModal] = useState<Modal>(null);
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [plate, setPlate] = useState("");
    const [category, setCategory] = useState<Vehicle["category"]>("car");
    const [brand, setBrand] = useState("");
    const [nickname, setNickname] = useState("");
    const [model, setModel] = useState("");
    const [vehicleType, setVehicleType] = useState("");
    const [seats, setSeats] = useState("");
    const [fuelType, setFuelType] = useState("");
    const [color, setColor] = useState("");
    const [manufacturingYear, setManufacturingYear] = useState("");
    const [size, setSize] = useState<Vehicle["size"]>("standard");
    const [photo, setPhoto] = useState<string | undefined>();
    const [photoFile, setPhotoFile] = useState<File | null>(null);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState("");
    const [brandError, setBrandError] = useState("");
    const [photoError, setPhotoError] = useState("");
    const [notice, setNotice] = useState("");
    const dialogRef = useRef<HTMLDialogElement>(null);
    const fileRef = useRef<HTMLInputElement>(null);
    const plateInputRef = useRef<HTMLInputElement>(null);
    const selectedVehicle = vehicles.find(
        (vehicle) => vehicle.id === selectedId,
    );

    useEffect(() => {
        if (!session) return;
        const controller = new AbortController();
        getMyVehicles(controller.signal)
            .then((result) => {
                setVehicles(result.vehicles.map(fromRegisteredVehicle));
                setLoadedAccountId(session.account.id);
            })
            .catch(() => {
                if (controller.signal.aborted) return;
                setVehicles([]);
                setLoadedAccountId(session.account.id);
                setNotice(
                    copy(
                        "Your vehicles could not be loaded. Please refresh the page.",
                        "Không thể tải danh sách xe. Vui lòng tải lại trang.",
                    ),
                );
            });
        return () => controller.abort();
    }, [session, copy]);

    useEffect(() => {
        const dialog = dialogRef.current;
        if (modal && dialog && !dialog.open) dialog.showModal();
        if (!modal && dialog?.open) dialog.close();
    }, [modal]);

    function openAdd() {
        if (vehicles.length >= MAX_VEHICLES) return;
        setSelectedId(null);
        setPlate("");
        setCategory("motorbike");
        setBrand("");
        setNickname("");
        setModel("");
        setVehicleType("");
        setSeats("");
        setFuelType("");
        setColor("");
        setManufacturingYear("");
        setSize("standard");
        setPhoto(undefined);
        setPhotoFile(null);
        setError("");
        setBrandError("");
        setPhotoError("");
        setModal("add");
    }
    function openEdit(vehicle: Vehicle) {
        setSelectedId(vehicle.id);
        setPlate(vehicle.plate);
        setCategory(vehicle.category);
        setBrand(vehicle.brand);
        setNickname(vehicle.nickname ?? "");
        setModel(vehicle.model ?? "");
        setVehicleType(vehicle.vehicleType ?? "");
        setSeats(vehicle.seats ?? "");
        setFuelType(vehicle.fuelType ?? "");
        setColor(vehicle.color ?? "");
        setManufacturingYear(vehicle.manufacturingYear ?? "");
        setSize(vehicle.size);
        setPhoto(vehicle.photo);
        setPhotoFile(null);
        setError("");
        setBrandError("");
        setPhotoError("");
        setModal("edit");
    }
    function resetVehicleDetails() {
        setPlate("");
        setBrand("");
        setModel("");
        setVehicleType("");
        setSeats("");
        setFuelType("");
        setPhoto(undefined);
        setPhotoFile(null);
        setBrandError("");
        setPhotoError("");
        setError("");
        setSize("standard");
    }
    function chooseCategory(kind: VehicleCategory) {
        if (baseCategory(category) === kind) return;
        setCategory(kind);
        resetVehicleDetails();
    }
    function chooseTwoWheelerType(
        kind: "motorbike" | "electric-motorbike" | "bicycle",
    ) {
        if (category === kind) return;
        if ((category === "bicycle") !== (kind === "bicycle")) {
            resetVehicleDetails();
        }
        setCategory(kind);
        setFuelType(kind === "electric-motorbike" ? "electric" : "");
    }
    function choosePowerSource(electric: boolean) {
        if (!isCarCategory(category)) return;
        setCategory(electric ? "electric-car" : "car");
        setFuelType(electric ? "electric" : "");
    }
    function closeModal() {
        setModal(null);
        setError("");
        setBrandError("");
        setPhotoError("");
    }
    function updatePlate(value: string, cursor: number | null) {
        const formatted = formatPlate(value, category);
        const charactersBeforeCursor = value
            .slice(0, cursor ?? value.length)
            .replace(/[^A-Za-z0-9]/g, "").length;
        setPlate(formatted);
        setError("");
        requestAnimationFrame(() => {
            const input = plateInputRef.current;
            if (!input || document.activeElement !== input) return;
            let position = 0;
            let characters = 0;
            while (
                position < formatted.length &&
                characters < charactersBeforeCursor
            ) {
                if (/[A-Za-z0-9]/.test(formatted[position])) characters += 1;
                position += 1;
            }
            input.setSelectionRange(position, position);
        });
    }
    async function saveVehicle(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        if (isSaving) return;
        const nextPlate = needsPlate(category)
            ? formatPlate(plate, category)
            : "";
        const nextBrand = brand.trim();
        const needsBrand = needsPlate(category);
        setBrandError(
            nextBrand || session || !needsBrand
                ? ""
                : "Select or enter a vehicle brand.",
        );
        const plateCheck = validateVehiclePlate(
            nextPlate,
            category === "bicycle"
                ? "Bicycle"
                : isCarCategory(category)
                  ? "Car"
                  : "Motorbike",
        );
        if (modal === "add" && !plateCheck.valid) {
            setError(
                copy(
                    category === "bicycle"
                        ? "A bicycle does not use a license plate."
                        : "Enter a valid Vietnamese civilian plate for this vehicle type.",
                    category === "bicycle"
                        ? "Xe đạp không có biển số."
                        : "Nhập biển số dân sự Việt Nam hợp lệ cho loại xe này.",
                ),
            );
            return;
        }
        if (
            modal === "add" &&
            nextPlate &&
            vehicles.some(
                (vehicle) =>
                    validateVehiclePlate(
                        vehicle.plate,
                        vehicle.category === "bicycle"
                            ? "Bicycle"
                            : isCarCategory(vehicle.category)
                              ? "Car"
                              : "Motorbike",
                    ).normalized === plateCheck.normalized,
            )
        ) {
            setError(
                copy(
                    "This license plate is already in your list.",
                    "Biển số này đã có trong danh sách xe của bạn.",
                ),
            );
            return;
        }
        if (!nextBrand && !session && needsBrand) {
            document.getElementById("vehicles-brand")?.focus();
            return;
        }
        if (modal === "add") {
            if (session) {
                try {
                    setIsSaving(true);
                    const uploadedPhoto = photoFile
                        ? await uploadCustomerMedia("vehicle-photo", photoFile)
                        : null;
                    const details: VehicleDetails = {
                        brand: nextBrand || null,
                        model: model.trim() || null,
                        motorbikeKind: isMotorbikeCategory(category)
                            ? vehicleType || null
                            : null,
                        seats: seats ? Number(seats) : null,
                        fuelType: fuelType || null,
                        color: color || null,
                        manufacturingYear: manufacturingYear
                            ? Number(manufacturingYear)
                            : null,
                        size,
                        photoUrl:
                            uploadedPhoto?.url ?? defaultVehicleImage(category),
                    };
                    const saved = await addMyVehicle(
                        nextPlate || null,
                        category === "bicycle"
                            ? "Bicycle"
                            : isCarCategory(category)
                              ? "Car"
                              : "Motorbike",
                        nextBrand || null,
                        details,
                    );
                    setVehicles((current) => [
                        ...current,
                        fromRegisteredVehicle(saved),
                    ]);
                    setNotice(
                        copy(
                            "Vehicle added to your account.",
                            "Đã thêm xe vào tài khoản.",
                        ),
                    );
                    closeModal();
                } catch (error) {
                    setError(vehicleAddErrorMessage(error, language));
                } finally {
                    setIsSaving(false);
                }
                return;
            }
            setVehicles((current) => [
                ...current,
                {
                    id: crypto.randomUUID(),
                    category,
                    plate: nextPlate,
                    brand: nextBrand,
                    model: model.trim(),
                    vehicleType,
                    seats,
                    fuelType,
                    color,
                    manufacturingYear,
                    size,
                    photo: photo ?? defaultVehicleImage(category),
                    draft: Boolean(session),
                },
            ]);
            setNotice(
                copy(
                    "Vehicle added for this session.",
                    "Đã thêm xe cho phiên này.",
                ),
            );
        } else if (modal === "edit" && selectedId) {
            if (session && selectedVehicle && !selectedVehicle.draft) {
                try {
                    setIsSaving(true);
                    const uploadedPhoto = photoFile
                        ? await uploadCustomerMedia("vehicle-photo", photoFile)
                        : null;
                    const details: VehicleDetails = {
                        brand: selectedVehicle.details?.brand ?? null,
                        model: selectedVehicle.details?.model ?? null,
                        motorbikeKind:
                            selectedVehicle.details?.motorbikeKind ?? null,
                        seats: selectedVehicle.details?.seats ?? null,
                        fuelType: selectedVehicle.details?.fuelType ?? null,
                        color: color || null,
                        manufacturingYear:
                            selectedVehicle.details?.manufacturingYear ?? null,
                        size:
                            selectedVehicle.details?.size ??
                            selectedVehicle.size,
                        photoUrl:
                            uploadedPhoto?.url ??
                            selectedVehicle.details?.photoUrl ??
                            defaultVehicleImage(category),
                    };
                    const saved = await updateMyVehicle(
                        selectedId,
                        nickname.trim() || null,
                        details,
                    );
                    setVehicles((current) =>
                        current.map((vehicle) =>
                            vehicle.id === selectedId
                                ? fromRegisteredVehicle(saved)
                                : vehicle,
                        ),
                    );
                    setNotice(
                        copy(
                            "Vehicle updated in your account.",
                            "Đã cập nhật xe trong tài khoản.",
                        ),
                    );
                    closeModal();
                } catch {
                    setError(
                        "Could not update this vehicle. Please try again.",
                    );
                } finally {
                    setIsSaving(false);
                }
                return;
            }
            setVehicles((current) =>
                current.map((vehicle) =>
                    vehicle.id === selectedId
                        ? {
                              ...vehicle,
                              nickname: nickname.trim(),
                              color,
                              photo: photo ?? defaultVehicleImage(category),
                          }
                        : vehicle,
                ),
            );
            setNotice(
                copy(
                    "Vehicle updated for this session.",
                    "Đã cập nhật xe trong phiên này.",
                ),
            );
        }
        closeModal();
    }
    function handlePhoto(file?: File) {
        if (!file) return;
        if (
            !["image/jpeg", "image/png", "image/webp"].includes(file.type) ||
            file.size > 5 * 1024 * 1024
        ) {
            setPhotoError("Choose a JPG, PNG or WebP image under 5 MB.");
            return;
        }
        const reader = new FileReader();
        reader.onload = () => {
            if (typeof reader.result === "string") setPhoto(reader.result);
        };
        reader.readAsDataURL(file);
        setPhotoFile(file);
        setPhotoError("");
    }
    async function removeVehicle() {
        if (!selectedId) return;
        if (session && !selectedVehicle?.draft) {
            try {
                await deleteMyVehicle(selectedId);
            } catch {
                setNotice(
                    copy(
                        "Could not remove this vehicle. Please try again.",
                        "Không thể xóa xe. Vui lòng thử lại.",
                    ),
                );
                closeModal();
                return;
            }
        }
        setVehicles((current) => {
            const remaining = current.filter(
                (vehicle) => vehicle.id !== selectedId,
            );
            const wasDefault = current.find(
                (vehicle) => vehicle.id === selectedId,
            )?.isDefault;
            if (wasDefault && remaining.length > 0) {
                remaining[0] = { ...remaining[0], isDefault: true };
            }
            return remaining;
        });
        setNotice(
            session
                ? copy(
                      "Vehicle removed from your account.",
                      "Đã xóa xe khỏi tài khoản.",
                  )
                : copy(
                      "Vehicle removed for this session.",
                      "Đã xóa xe khỏi phiên này.",
                  ),
        );
        closeModal();
    }

    if (session && loadedAccountId !== session.account.id) {
        return (
            <main
                className={`parkwise-page vehicles-page ${fontClassName}`}
                aria-live="polite"
            >
                {copy("Loading your vehicles...", "Đang tải danh sách xe...")}
            </main>
        );
    }

    return (
        <div
            lang={language}
            className={`parkwise-page vehicles-page ${fontClassName}`}
        >
            <ParkWiseNavbar
                variant="customer"
                navigationMode="dashboard"
                activeHref="/customer/vehicles"
                accountControl={
                    <div className="dashboard-header-actions">
                        <CustomerLanguageSwitcher />
                        <CustomerNotifications />
                        <CustomerAccountMenu activeHref="/customer/vehicles" />
                    </div>
                }
            />
            <main className="vehicles-main">
                <div className="vehicles-layout">
                    <div className="vehicles-primary-column">
                        <div className="vehicles-heading">
                            <h1>{copy("My Vehicles", "Xe của tôi")}</h1>
                            <p>
                                {copy(
                                    "Manage the vehicles you use when parking.",
                                    "Quản lý những xe bạn sử dụng khi đỗ xe.",
                                )}
                            </p>
                        </div>
                        <section
                            className="vehicles-summary"
                            aria-label={copy("My vehicles", "Xe của tôi")}
                        >
                            <span className="vehicles-summary-icon">
                                <Icon name="car" size={25} />
                            </span>
                            <div className="vehicles-summary-stats">
                                <span>{copy("My vehicles", "Xe của tôi")}</span>
                                <strong>
                                    {vehicles.length} / {MAX_VEHICLES}
                                </strong>
                            </div>
                            <div
                                className="vehicles-progress"
                                role="progressbar"
                                aria-label={copy(
                                    "Vehicle slots used",
                                    "Số xe đã thêm",
                                )}
                                aria-valuenow={vehicles.length}
                                aria-valuemin={0}
                                aria-valuemax={MAX_VEHICLES}
                            >
                                <span
                                    style={{
                                        width: `${vehicles.length * 10}%`,
                                    }}
                                />
                            </div>
                            <button
                                type="button"
                                className="vehicles-dark-button"
                                onClick={openAdd}
                                disabled={vehicles.length >= MAX_VEHICLES}
                            >
                                <Icon name="plus" />{" "}
                                {copy("Add Vehicle", "Thêm xe")}
                            </button>
                        </section>
                        {notice && (
                            <p className="vehicles-notice" role="status">
                                {notice}
                            </p>
                        )}
                        <div className="vehicles-card-grid">
                            {vehicles.map((vehicle) => {
                                const details = getVehicleDetails(vehicle);
                                return (
                                    <article
                                        className="vehicles-card"
                                        key={vehicle.id}
                                    >
                                        <div className="vehicles-card-top">
                                            <div className="vehicles-badges">
                                                <span
                                                    className={`vehicles-badge ${vehicle.category}`}
                                                >
                                                    <Icon
                                                        name={
                                                            categoryInfo(
                                                                vehicle.category,
                                                            ).icon
                                                        }
                                                        size={16}
                                                    />
                                                    {copy(
                                                        categoryInfo(
                                                            vehicle.category,
                                                        ).label,
                                                        vehicleLabelVi(
                                                            categoryInfo(
                                                                vehicle.category,
                                                            ).label,
                                                        ),
                                                    )}
                                                </span>
                                                {isCarCategory(
                                                    vehicle.category,
                                                ) &&
                                                    vehicle.size ===
                                                        "large" && (
                                                        <span className="vehicles-badge large">
                                                            {copy(
                                                                "Large",
                                                                "Cỡ lớn",
                                                            )}
                                                        </span>
                                                    )}
                                                {vehicle.draft && (
                                                    <span className="vehicles-badge draft">
                                                        {copy(
                                                            "Local draft",
                                                            "Bản nháp trên thiết bị",
                                                        )}
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                        <div className="vehicles-card-image">
                                            <VehiclePhoto
                                                category={vehicle.category}
                                                photo={vehicle.photo}
                                                alt={`Vehicle ${formatPlate(vehicle.plate, vehicle.category) || vehicle.brand || categoryInfo(vehicle.category).label}`}
                                                sizes="(max-width: 700px) 90vw, 360px"
                                            />
                                        </div>
                                        <div className="vehicles-card-info">
                                            <h2>
                                                {(vehicle.plate &&
                                                    formatPlate(
                                                        vehicle.plate,
                                                        vehicle.category,
                                                    )) ||
                                                    vehicle.brand ||
                                                    copy(
                                                        categoryInfo(
                                                            vehicle.category,
                                                        ).label,
                                                        vehicleLabelVi(
                                                            categoryInfo(
                                                                vehicle.category,
                                                            ).label,
                                                        ),
                                                    )}
                                            </h2>
                                            <p className="vehicles-card-subtitle">
                                                {copy(
                                                    categoryInfo(
                                                        vehicle.category,
                                                    ).description,
                                                    vehicleLabelVi(
                                                        categoryInfo(
                                                            vehicle.category,
                                                        ).description,
                                                    ),
                                                )}
                                            </p>
                                            {details.length > 0 && (
                                                <ul className="vehicles-card-details">
                                                    {details.map((detail) => (
                                                        <li key={detail.label}>
                                                            <span className="vehicles-card-detail-icon">
                                                                <Icon
                                                                    name={
                                                                        detail.icon
                                                                    }
                                                                    size={19}
                                                                />
                                                            </span>
                                                            <span className="vehicles-card-detail-copy">
                                                                <span>
                                                                    {copy(
                                                                        detail.label,
                                                                        vehicleLabelVi(
                                                                            detail.label,
                                                                        ),
                                                                    )}
                                                                </span>
                                                                <strong>
                                                                    {
                                                                        detail.value
                                                                    }
                                                                </strong>
                                                            </span>
                                                        </li>
                                                    ))}
                                                </ul>
                                            )}
                                        </div>
                                        <div className="vehicles-card-actions">
                                            <button
                                                type="button"
                                                onClick={() =>
                                                    openEdit(vehicle)
                                                }
                                            >
                                                <Icon name="edit" size={16} />{" "}
                                                {copy("Edit", "S\u1eeda")}
                                            </button>
                                            <button
                                                type="button"
                                                className="danger"
                                                onClick={() => {
                                                    setSelectedId(vehicle.id);
                                                    setModal("delete");
                                                }}
                                            >
                                                <Icon name="trash" size={17} />{" "}
                                                {copy("Delete", "Xóa")}
                                            </button>
                                        </div>
                                    </article>
                                );
                            })}
                            {vehicles.length < MAX_VEHICLES && (
                                <button
                                    type="button"
                                    className="vehicles-add-card"
                                    onClick={openAdd}
                                >
                                    <span className="vehicles-add-circle">
                                        <Icon name="plus" size={30} />
                                    </span>
                                    <strong>
                                        {vehicles.length === 0
                                            ? copy(
                                                  "No vehicles added yet",
                                                  "Chưa thêm xe nào",
                                              )
                                            : copy(
                                                  "Add another vehicle",
                                                  "Thêm xe khác",
                                              )}
                                    </strong>
                                    <span>
                                        {copy(
                                            `Manage up to ${MAX_VEHICLES} vehicles.`,
                                            `Quản lý tối đa ${MAX_VEHICLES} xe.`,
                                        )}
                                    </span>
                                    <span className="vehicles-outline-button">
                                        <Icon name="plus" size={17} />{" "}
                                        {vehicles.length === 0
                                            ? copy(
                                                  "Add Your First Vehicle",
                                                  "Thêm xe đầu tiên",
                                              )
                                            : copy("Add Vehicle", "Thêm xe")}
                                    </span>
                                </button>
                            )}
                        </div>
                    </div>
                    <aside
                        className="vehicles-aside"
                        aria-label={copy(
                            "Vehicle information",
                            "Thông tin về xe",
                        )}
                    >
                        <section className="vehicles-guidelines">
                            <div className="vehicles-aside-title">
                                <span>
                                    <Icon name="book" />
                                </span>
                                <h2>
                                    {copy(
                                        "Vehicle guidelines",
                                        "Hướng dẫn về xe",
                                    )}
                                </h2>
                            </div>
                            <ul>
                                <li>
                                    <span>
                                        <Icon name="car" size={19} />
                                    </span>
                                    {copy(
                                        "Manage up to 10 vehicles.",
                                        "Quản lý tối đa 10 xe.",
                                    )}
                                </li>
                                <li>
                                    <span>
                                        <Icon name="document" size={18} />
                                    </span>
                                    {copy(
                                        "Choose the parking area that matches your vehicle.",
                                        "Chọn khu đỗ phù hợp với xe của bạn.",
                                    )}
                                </li>
                                <li>
                                    <span>
                                        <Icon name="bolt" size={18} />
                                    </span>
                                    {copy(
                                        "A matching illustration is used until you add your own photo.",
                                        "Hệ thống dùng ảnh minh họa phù hợp cho đến khi bạn tải ảnh riêng lên.",
                                    )}
                                </li>
                                <li>
                                    <span>
                                        <Icon name="info" size={18} />
                                    </span>
                                    {copy(
                                        "Government priority vehicles do not register here.",
                                        "Xe ưu tiên theo quy định không đăng ký tại đây.",
                                    )}
                                </li>
                            </ul>
                        </section>
                        <section className="vehicles-help">
                            <div className="vehicles-aside-title">
                                <span>
                                    <Icon name="headset" size={24} />
                                </span>
                                <div>
                                    <h2>{copy("Need Help?", "Cần hỗ trợ?")}</h2>
                                    <p>
                                        {copy(
                                            "Our support team is here to assist you.",
                                            "Đội hỗ trợ sẵn sàng giúp bạn.",
                                        )}
                                    </p>
                                </div>
                            </div>
                        </section>
                    </aside>
                </div>
            </main>
            <ParkWiseFooter language={language} />
            <dialog
                ref={dialogRef}
                className={
                    modal === "delete"
                        ? "vehicles-dialog vehicles-dialog--delete"
                        : "vehicles-dialog"
                }
                onClose={() => setModal(null)}
                onCancel={() => setModal(null)}
                aria-labelledby="vehicles-dialog-title"
            >
                {modal === "delete" ? (
                    <div className="vehicles-delete-dialog">
                        <button
                            type="button"
                            className="vehicles-dialog-close"
                            aria-label="Close"
                            onClick={closeModal}
                        >
                            <Icon name="close" size={17} />
                        </button>
                        <span className="vehicles-delete-icon">
                            <Icon name="trash" size={26} />
                        </span>
                        <h2 id="vehicles-dialog-title">
                            {copy("Delete Vehicle?", "Xóa xe?")}
                        </h2>
                        <p>
                            {copy(
                                "Are you sure you want to remove this vehicle from your account?",
                                "Bạn có chắc muốn xóa xe này khỏi tài khoản không?",
                            )}
                        </p>
                        <strong className="vehicles-delete-plate">
                            <Icon
                                name={
                                    selectedVehicle
                                        ? categoryInfo(selectedVehicle.category)
                                              .icon
                                        : "car"
                                }
                                size={20}
                            />{" "}
                            {selectedVehicle?.plate ||
                                selectedVehicle?.brand ||
                                (selectedVehicle
                                    ? categoryInfo(selectedVehicle.category)
                                          .label
                                    : "")}
                        </strong>
                        <div className="vehicles-dialog-actions">
                            <button
                                type="button"
                                className="vehicles-light-button"
                                onClick={closeModal}
                            >
                                {copy("Cancel", "Hủy")}
                            </button>
                            <button
                                type="button"
                                className="vehicles-delete-button"
                                onClick={removeVehicle}
                            >
                                {copy("Delete Vehicle", "Xóa xe")}
                            </button>
                        </div>
                    </div>
                ) : (
                    modal && (
                        <form
                            className="vehicles-form"
                            onSubmit={saveVehicle}
                            noValidate
                        >
                            <div className="vehicles-dialog-header">
                                <div>
                                    <h2 id="vehicles-dialog-title">
                                        {modal === "add"
                                            ? copy("Add Vehicle", "Thêm xe")
                                            : copy("Edit Vehicle", "Sửa xe")}
                                    </h2>
                                    <p>
                                        {modal === "add"
                                            ? copy(
                                                  "Choose the parking area that fits this vehicle.",
                                                  "Chọn khu đỗ phù hợp với xe này.",
                                              )
                                            : copy(
                                                  "Update this vehicle's details.",
                                                  "Cập nhật thông tin xe này.",
                                              )}
                                    </p>
                                </div>
                                <button
                                    type="button"
                                    className="vehicles-dialog-close"
                                    aria-label="Close"
                                    onClick={closeModal}
                                >
                                    <Icon name="close" size={27} />
                                </button>
                            </div>
                            <div className="vehicles-form-body">
                                <div className="vehicles-upload">
                                    <input
                                        ref={fileRef}
                                        type="file"
                                        accept="image/jpeg,image/png,image/webp"
                                        className="vehicles-file-input"
                                        tabIndex={-1}
                                        aria-hidden="true"
                                        onChange={(event) => {
                                            handlePhoto(
                                                event.target.files?.[0],
                                            );
                                            event.target.value = "";
                                        }}
                                    />
                                    <button
                                        type="button"
                                        className="vehicles-upload-target"
                                        aria-label={
                                            photo
                                                ? "Change vehicle photo"
                                                : "Choose vehicle photo"
                                        }
                                        onClick={() => fileRef.current?.click()}
                                    >
                                        <span
                                            className={`vehicles-upload-content vehicles-upload-content--${category}`}
                                        >
                                            <span className="vehicles-upload-icon">
                                                <Icon
                                                    name={
                                                        categoryInfo(category)
                                                            .icon
                                                    }
                                                    size={43}
                                                />
                                            </span>
                                            <strong>
                                                {copy(
                                                    "Upload Vehicle Photo",
                                                    "Tải ảnh xe lên",
                                                )}{" "}
                                            </strong>
                                            <small>
                                                JPG, PNG or WebP · Max 5 MB
                                            </small>
                                            <span className="vehicles-photo-button">
                                                <Icon name="upload" size={19} />
                                                {photo
                                                    ? copy(
                                                          "Change Photo",
                                                          "Đổi ảnh",
                                                      )
                                                    : copy(
                                                          "Choose Photo",
                                                          "Chọn ảnh",
                                                      )}
                                            </span>
                                            <span className="vehicles-upload-divider">
                                                <span>
                                                    {copy("or", "hoặc")}
                                                </span>
                                            </span>
                                            <span
                                                className={`vehicles-upload-preview vehicles-upload-preview--${category}`}
                                            >
                                                <VehiclePhoto
                                                    category={category}
                                                    photo={photo}
                                                    alt={
                                                        photo
                                                            ? "Selected vehicle"
                                                            : `${categoryInfo(category).label} illustration`
                                                    }
                                                    sizes="(max-width: 850px) 85vw, 320px"
                                                />
                                            </span>
                                            <span className="vehicles-upload-caption">
                                                {photo
                                                    ? copy(
                                                          "Your vehicle photo is ready. Choose another photo to replace it.",
                                                          "Ảnh xe đã sẵn sàng. Chọn ảnh khác để thay thế.",
                                                      )
                                                    : category === "bicycle"
                                                      ? copy(
                                                            "A bicycle illustration will be used if you skip this step.",
                                                            "Nếu bỏ qua, hệ thống sẽ dùng ảnh minh họa xe đạp.",
                                                        )
                                                      : copy(
                                                            "Add a photo to help you recognize your vehicle.",
                                                            "Thêm ảnh để dễ nhận ra xe của bạn.",
                                                        )}
                                            </span>
                                        </span>
                                    </button>
                                    {photoError && (
                                        <p
                                            className="vehicles-form-error"
                                            role="alert"
                                        >
                                            {photoError}
                                        </p>
                                    )}
                                </div>
                                <div className="vehicles-form-fields">
                                    {modal === "edit" ? (
                                        <div className="vehicles-edit-fields">
                                            <p className="vehicles-edit-identity">
                                                <strong>
                                                    {copy(
                                                        "Vehicle identity",
                                                        "Định danh xe",
                                                    )}
                                                </strong>
                                                <span>
                                                    {copy(
                                                        categoryInfo(category)
                                                            .label,
                                                        vehicleLabelVi(
                                                            categoryInfo(
                                                                category,
                                                            ).label,
                                                        ),
                                                    )}
                                                    {plate ? ` · ${plate}` : ""}
                                                </span>
                                                <small>
                                                    {copy(
                                                        "Vehicle type and plate cannot be edited here.",
                                                        "Không thể sửa loại xe và biển số tại đây.",
                                                    )}
                                                </small>
                                            </p>
                                            <div className="vehicles-field">
                                                <label htmlFor="vehicles-nickname">
                                                    {copy(
                                                        "Nickname (Optional)",
                                                        "Tên gợi nhớ (Không bắt buộc)",
                                                    )}
                                                </label>
                                                <input
                                                    id="vehicles-nickname"
                                                    value={nickname}
                                                    maxLength={80}
                                                    onChange={(event) =>
                                                        setNickname(
                                                            event.target.value,
                                                        )
                                                    }
                                                />
                                            </div>
                                            <div className="vehicles-field">
                                                <label htmlFor="vehicles-edit-color">
                                                    {copy(
                                                        "Vehicle Color (Optional)",
                                                        "Màu xe (Không bắt buộc)",
                                                    )}
                                                </label>
                                                <VehicleDropdown
                                                    id="vehicles-edit-color"
                                                    value={color}
                                                    options={COLOR_OPTIONS}
                                                    placeholder={copy(
                                                        "Select color",
                                                        "Chọn màu",
                                                    )}
                                                    onChange={setColor}
                                                />
                                            </div>
                                        </div>
                                    ) : (
                                        <>
                                            <fieldset className="vehicles-size-field vehicles-category-field">
                                                <legend>
                                                    {copy(
                                                        "Vehicle Type",
                                                        "Loại xe",
                                                    )}{" "}
                                                    <span aria-hidden="true">
                                                        *
                                                    </span>
                                                </legend>
                                                <div className="vehicles-size-options">
                                                    {VEHICLE_FORM_CATEGORIES.map(
                                                        (item) => (
                                                            <label
                                                                key={item.value}
                                                                className={`vehicles-size-option ${baseCategory(category) === item.value ? "selected" : ""}`}
                                                            >
                                                                <input
                                                                    type="radio"
                                                                    name="vehicles-category"
                                                                    value={
                                                                        item.value
                                                                    }
                                                                    checked={
                                                                        baseCategory(
                                                                            category,
                                                                        ) ===
                                                                        item.value
                                                                    }
                                                                    onChange={() =>
                                                                        chooseCategory(
                                                                            item.value,
                                                                        )
                                                                    }
                                                                />
                                                                <span className="vehicles-size-indicator" />
                                                                <Icon
                                                                    name={
                                                                        item.icon
                                                                    }
                                                                    size={27}
                                                                />
                                                                <span>
                                                                    <strong>
                                                                        {copy(
                                                                            item.label,
                                                                            vehicleLabelVi(
                                                                                item.label,
                                                                            ),
                                                                        )}
                                                                    </strong>
                                                                    <small>
                                                                        {copy(
                                                                            item.description,
                                                                            vehicleLabelVi(
                                                                                item.description,
                                                                            ),
                                                                        )}
                                                                    </small>
                                                                </span>
                                                            </label>
                                                        ),
                                                    )}
                                                </div>
                                            </fieldset>
                                            <TwoWheelerTypeSelector
                                                category={category}
                                                disabled={false}
                                                onChange={chooseTwoWheelerType}
                                            />
                                            <PowerSourceSelector
                                                category={category}
                                                disabled={false}
                                                onChange={choosePowerSource}
                                            />
                                            {category === "bicycle" && (
                                                <div className="vehicles-field-grid vehicles-field-grid--extra">
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-bicycle-brand">
                                                            {copy(
                                                                "Brand (Optional)",
                                                                "Hãng xe (Không bắt buộc)",
                                                            )}
                                                        </label>
                                                        <input
                                                            id="vehicles-bicycle-brand"
                                                            value={brand}
                                                            maxLength={80}
                                                            onChange={(event) =>
                                                                setBrand(
                                                                    event.target
                                                                        .value,
                                                                )
                                                            }
                                                        />
                                                    </div>
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-bicycle-model">
                                                            {copy(
                                                                "Model (Optional)",
                                                                "Dòng xe (Không bắt buộc)",
                                                            )}
                                                        </label>
                                                        <input
                                                            id="vehicles-bicycle-model"
                                                            value={model}
                                                            maxLength={80}
                                                            onChange={(event) =>
                                                                setModel(
                                                                    event.target
                                                                        .value,
                                                                )
                                                            }
                                                        />
                                                    </div>
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-bicycle-color">
                                                            {copy(
                                                                "Color (Optional)",
                                                                "Màu sắc (Không bắt buộc)",
                                                            )}
                                                        </label>
                                                        <VehicleDropdown
                                                            id="vehicles-bicycle-color"
                                                            value={color}
                                                            options={
                                                                COLOR_OPTIONS
                                                            }
                                                            placeholder={copy(
                                                                "Select color",
                                                                "Chọn màu",
                                                            )}
                                                            onChange={setColor}
                                                        />
                                                    </div>
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-bicycle-year">
                                                            {copy(
                                                                "Year (Optional)",
                                                                "Năm sản xuất (Không bắt buộc)",
                                                            )}
                                                        </label>
                                                        <VehicleDropdown
                                                            id="vehicles-bicycle-year"
                                                            value={
                                                                manufacturingYear
                                                            }
                                                            options={
                                                                YEAR_OPTIONS
                                                            }
                                                            placeholder={copy(
                                                                "Select year",
                                                                "Chọn năm",
                                                            )}
                                                            onChange={
                                                                setManufacturingYear
                                                            }
                                                        />
                                                    </div>
                                                </div>
                                            )}
                                            {needsPlate(category) && (
                                                <div className="vehicles-field">
                                                    <label htmlFor="vehicles-plate">
                                                        {copy(
                                                            "License Plate",
                                                            "Biển số xe",
                                                        )}{" "}
                                                        <span aria-hidden="true">
                                                            *
                                                        </span>
                                                    </label>
                                                    <input
                                                        id="vehicles-plate"
                                                        ref={plateInputRef}
                                                        value={plate}
                                                        onChange={(event) =>
                                                            updatePlate(
                                                                event.target
                                                                    .value,
                                                                event.target
                                                                    .selectionStart,
                                                            )
                                                        }
                                                        onBlur={() =>
                                                            setPlate(
                                                                (current) =>
                                                                    formatPlate(
                                                                        current,
                                                                        category,
                                                                    ),
                                                            )
                                                        }
                                                        placeholder={
                                                            isMotorbikeCategory(
                                                                category,
                                                            )
                                                                ? "e.g. 59A1-123.45"
                                                                : "e.g. 51K-123.45"
                                                        }
                                                        maxLength={15}
                                                        autoCapitalize="characters"
                                                        spellCheck={false}
                                                        aria-required={needsPlate(
                                                            category,
                                                        )}
                                                        aria-invalid={Boolean(
                                                            error,
                                                        )}
                                                        aria-describedby={
                                                            error
                                                                ? "vehicles-form-error"
                                                                : undefined
                                                        }
                                                    />
                                                    {error && (
                                                        <p
                                                            id="vehicles-form-error"
                                                            className="vehicles-form-error"
                                                            role="alert"
                                                        >
                                                            {error}
                                                        </p>
                                                    )}
                                                </div>
                                            )}
                                            {needsPlate(category) && (
                                                <div className="vehicles-field-grid">
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-brand">
                                                            {copy(
                                                                "Vehicle Brand",
                                                                "Hãng xe",
                                                            )}{" "}
                                                            <span aria-hidden="true">
                                                                *
                                                            </span>
                                                        </label>
                                                        <VehicleDropdown
                                                            id="vehicles-brand"
                                                            value={brand}
                                                            options={
                                                                isMotorbikeCategory(
                                                                    category,
                                                                )
                                                                    ? MOTORBIKE_BRAND_OPTIONS
                                                                    : BRAND_OPTIONS
                                                            }
                                                            placeholder={copy(
                                                                "Select brand",
                                                                "Chọn hãng xe",
                                                            )}
                                                            onChange={(
                                                                nextBrand,
                                                            ) => {
                                                                setBrand(
                                                                    nextBrand,
                                                                );
                                                                setBrandError(
                                                                    "",
                                                                );
                                                                setModel("");
                                                            }}
                                                            required
                                                            invalid={Boolean(
                                                                brandError,
                                                            )}
                                                            describedBy={
                                                                brandError
                                                                    ? "vehicles-brand-error"
                                                                    : undefined
                                                            }
                                                            searchable
                                                            otherLabel={copy(
                                                                "Other: enter a brand",
                                                                "Khác: nhập hãng xe",
                                                            )}
                                                            leadingIcon={
                                                                <Icon
                                                                    name={
                                                                        isMotorbikeCategory(
                                                                            category,
                                                                        )
                                                                            ? "motorbike"
                                                                            : "car"
                                                                    }
                                                                    size={19}
                                                                />
                                                            }
                                                        />
                                                        {brandError && (
                                                            <p
                                                                id="vehicles-brand-error"
                                                                className="vehicles-form-error"
                                                                role="alert"
                                                            >
                                                                {brandError}
                                                            </p>
                                                        )}
                                                    </div>
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-model">
                                                            {copy(
                                                                "Vehicle Model",
                                                                "Dòng xe",
                                                            )}{" "}
                                                            <span>
                                                                {copy(
                                                                    "(Optional)",
                                                                    "(Không bắt buộc)",
                                                                )}
                                                            </span>
                                                        </label>
                                                        <VehicleDropdown
                                                            id="vehicles-model"
                                                            value={model}
                                                            options={
                                                                isMotorbikeCategory(
                                                                    category,
                                                                )
                                                                    ? getMotorbikeModelOptions(
                                                                          brand,
                                                                      )
                                                                    : getModelOptions(
                                                                          brand,
                                                                      )
                                                            }
                                                            placeholder={copy(
                                                                "Select model",
                                                                "Chọn dòng xe",
                                                            )}
                                                            onChange={setModel}
                                                            searchable
                                                            otherLabel={copy(
                                                                "Other: enter a model",
                                                                "Khác: nhập dòng xe",
                                                            )}
                                                            emptyMessage={
                                                                brand
                                                                    ? copy(
                                                                          "No suggested models for this brand",
                                                                          "Chưa có dòng xe gợi ý cho hãng này",
                                                                      )
                                                                    : copy(
                                                                          "Select a brand to see models",
                                                                          "Chọn hãng để xem dòng xe",
                                                                      )
                                                            }
                                                        />
                                                    </div>
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-type">
                                                            {isMotorbikeCategory(
                                                                category,
                                                            )
                                                                ? copy(
                                                                      "Motorbike Type",
                                                                      "Loại xe máy",
                                                                  )
                                                                : copy(
                                                                      "Body Type",
                                                                      "Kiểu thân xe",
                                                                  )}{" "}
                                                            <span>
                                                                {copy(
                                                                    "(Optional)",
                                                                    "(Không bắt buộc)",
                                                                )}
                                                            </span>
                                                        </label>
                                                        <VehicleDropdown
                                                            id="vehicles-type"
                                                            value={vehicleType}
                                                            options={
                                                                isMotorbikeCategory(
                                                                    category,
                                                                )
                                                                    ? MOTORBIKE_TYPE_OPTIONS
                                                                    : VEHICLE_TYPE_OPTIONS
                                                            }
                                                            placeholder={copy(
                                                                "Select type",
                                                                "Chọn loại xe",
                                                            )}
                                                            onChange={
                                                                setVehicleType
                                                            }
                                                            leadingIcon={
                                                                <Icon
                                                                    name={
                                                                        isMotorbikeCategory(
                                                                            category,
                                                                        )
                                                                            ? "motorbike"
                                                                            : "car"
                                                                    }
                                                                    size={20}
                                                                />
                                                            }
                                                        />
                                                    </div>
                                                    {isCarCategory(
                                                        category,
                                                    ) && (
                                                        <div className="vehicles-field">
                                                            <label htmlFor="vehicles-seats">
                                                                {copy(
                                                                    "Number of Seats",
                                                                    "Số ghế",
                                                                )}{" "}
                                                                <span>
                                                                    {copy(
                                                                        "(Optional)",
                                                                        "(Không bắt buộc)",
                                                                    )}
                                                                </span>
                                                            </label>
                                                            <VehicleDropdown
                                                                id="vehicles-seats"
                                                                value={seats}
                                                                options={
                                                                    SEAT_OPTIONS
                                                                }
                                                                placeholder={copy(
                                                                    "Select seats",
                                                                    "Chọn số ghế",
                                                                )}
                                                                onChange={
                                                                    setSeats
                                                                }
                                                                leadingIcon={
                                                                    <Icon
                                                                        name="seats"
                                                                        size={
                                                                            20
                                                                        }
                                                                    />
                                                                }
                                                            />
                                                        </div>
                                                    )}
                                                </div>
                                            )}
                                            {isCarCategory(category) && (
                                                <fieldset className="vehicles-size-field">
                                                    <legend>
                                                        {copy(
                                                            "Parking Space Size",
                                                            "Kích thước chỗ đỗ",
                                                        )}{" "}
                                                        <span aria-hidden="true">
                                                            *
                                                        </span>
                                                        <span
                                                            className="vehicles-size-info"
                                                            title="Choose the parking space that best fits your vehicle."
                                                            aria-label="Choose the parking space that best fits your vehicle."
                                                        >
                                                            <Icon
                                                                name="info"
                                                                size={19}
                                                            />
                                                        </span>
                                                    </legend>
                                                    <div className="vehicles-size-options">
                                                        <label
                                                            className={
                                                                size ===
                                                                "standard"
                                                                    ? "vehicles-size-option selected"
                                                                    : "vehicles-size-option"
                                                            }
                                                        >
                                                            <input
                                                                type="radio"
                                                                name="vehicles-size"
                                                                value="standard"
                                                                checked={
                                                                    size ===
                                                                    "standard"
                                                                }
                                                                onChange={() =>
                                                                    setSize(
                                                                        "standard",
                                                                    )
                                                                }
                                                            />
                                                            <span className="vehicles-size-indicator" />
                                                            <Icon
                                                                name="car"
                                                                size={31}
                                                            />
                                                            <span>
                                                                <strong>
                                                                    {copy(
                                                                        "Standard",
                                                                        "Tiêu chuẩn",
                                                                    )}
                                                                </strong>
                                                                <small>
                                                                    {copy(
                                                                        "For most cars",
                                                                        "Phù hợp đa số ô tô",
                                                                    )}
                                                                </small>
                                                            </span>
                                                        </label>
                                                        <label
                                                            className={
                                                                size === "large"
                                                                    ? "vehicles-size-option selected"
                                                                    : "vehicles-size-option"
                                                            }
                                                        >
                                                            <input
                                                                type="radio"
                                                                name="vehicles-size"
                                                                value="large"
                                                                checked={
                                                                    size ===
                                                                    "large"
                                                                }
                                                                onChange={() =>
                                                                    setSize(
                                                                        "large",
                                                                    )
                                                                }
                                                            />
                                                            <span className="vehicles-size-indicator" />
                                                            <Icon
                                                                name="car"
                                                                size={31}
                                                            />
                                                            <span>
                                                                <strong>
                                                                    {copy(
                                                                        "Large",
                                                                        "Cỡ lớn",
                                                                    )}
                                                                </strong>
                                                                <small>
                                                                    {copy(
                                                                        "For larger vehicles (SUV, MPV, Pickup)",
                                                                        "Cho xe lớn (SUV, MPV, bán tải)",
                                                                    )}
                                                                </small>
                                                            </span>
                                                        </label>
                                                    </div>
                                                </fieldset>
                                            )}
                                            {needsPlate(category) && (
                                                <div className="vehicles-field-grid vehicles-field-grid--extra">
                                                    {category !==
                                                        "electric-car" &&
                                                        category !==
                                                            "electric-motorbike" && (
                                                            <div className="vehicles-field">
                                                                <label htmlFor="vehicles-fuel">
                                                                    {copy(
                                                                        "Fuel Type",
                                                                        "Loại nhiên liệu",
                                                                    )}{" "}
                                                                    <span>
                                                                        {copy(
                                                                            "(Optional)",
                                                                            "(Không bắt buộc)",
                                                                        )}
                                                                    </span>
                                                                </label>
                                                                <VehicleDropdown
                                                                    id="vehicles-fuel"
                                                                    value={
                                                                        fuelType
                                                                    }
                                                                    options={(isMotorbikeCategory(
                                                                        category,
                                                                    )
                                                                        ? MOTORBIKE_FUEL_OPTIONS
                                                                        : FUEL_OPTIONS
                                                                    ).filter(
                                                                        (
                                                                            option,
                                                                        ) =>
                                                                            option.value !==
                                                                            "electric",
                                                                    )}
                                                                    placeholder={copy(
                                                                        "Select fuel type",
                                                                        "Chọn nhiên liệu",
                                                                    )}
                                                                    onChange={
                                                                        setFuelType
                                                                    }
                                                                    leadingIcon={
                                                                        <Icon
                                                                            name="fuel"
                                                                            size={
                                                                                19
                                                                            }
                                                                        />
                                                                    }
                                                                />
                                                            </div>
                                                        )}
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-color">
                                                            {copy(
                                                                "Vehicle Color",
                                                                "Màu xe",
                                                            )}{" "}
                                                            <span>
                                                                {copy(
                                                                    "(Optional)",
                                                                    "(Không bắt buộc)",
                                                                )}
                                                            </span>
                                                        </label>
                                                        <VehicleDropdown
                                                            id="vehicles-color"
                                                            value={color}
                                                            options={
                                                                COLOR_OPTIONS
                                                            }
                                                            placeholder={copy(
                                                                "Select color",
                                                                "Chọn màu",
                                                            )}
                                                            onChange={setColor}
                                                            leadingIcon={
                                                                <span
                                                                    className="vehicles-color-swatch"
                                                                    style={{
                                                                        backgroundColor:
                                                                            COLOR_SWATCHES[
                                                                                color
                                                                            ] ??
                                                                            "#fff",
                                                                    }}
                                                                />
                                                            }
                                                        />
                                                    </div>
                                                    <div className="vehicles-field">
                                                        <label htmlFor="vehicles-year">
                                                            {copy(
                                                                "Manufacturing Year",
                                                                "Năm sản xuất",
                                                            )}{" "}
                                                            <span>
                                                                {copy(
                                                                    "(Optional)",
                                                                    "(Không bắt buộc)",
                                                                )}
                                                            </span>
                                                        </label>
                                                        <VehicleDropdown
                                                            id="vehicles-year"
                                                            value={
                                                                manufacturingYear
                                                            }
                                                            options={
                                                                YEAR_OPTIONS
                                                            }
                                                            placeholder={copy(
                                                                "Select year",
                                                                "Chọn năm",
                                                            )}
                                                            onChange={
                                                                setManufacturingYear
                                                            }
                                                            leadingIcon={
                                                                <Icon
                                                                    name="calendar"
                                                                    size={19}
                                                                />
                                                            }
                                                        />
                                                    </div>
                                                </div>
                                            )}
                                        </>
                                    )}
                                </div>
                            </div>
                            <div className="vehicles-dialog-actions">
                                <button
                                    type="button"
                                    className="vehicles-light-button"
                                    onClick={closeModal}
                                >
                                    {copy("Cancel", "Hủy")}
                                </button>
                                <button
                                    type="submit"
                                    className="vehicles-dark-button"
                                    disabled={isSaving}
                                    aria-busy={isSaving}
                                >
                                    {isSaving
                                        ? copy("Saving…", "Đang lưu…")
                                        : modal === "add"
                                          ? copy("Add Vehicle", "Thêm xe")
                                          : copy(
                                                "Save Changes",
                                                "Lưu thay đổi",
                                            )}
                                    <Icon name="arrow" size={17} />
                                </button>
                            </div>
                        </form>
                    )
                )}
            </dialog>
        </div>
    );
}
