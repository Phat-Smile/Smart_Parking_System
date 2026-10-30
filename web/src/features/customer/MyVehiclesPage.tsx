"use client";

import {
    useEffect,
    useRef,
    useState,
    type FormEvent,
    type ReactNode,
} from "react";
import Image from "next/image";
import { ParkWiseNavbar } from "@/features/parkwise/ParkWiseNavbar";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";
import { CustomerAccountMenu } from "./CustomerShell";
import { VehicleDropdown } from "./VehicleDropdown";
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
    category: "car" | "motorbike";
    plate: string;
    brand: string;
    model?: string;
    vehicleType?: string;
    seats?: string;
    fuelType?: string;
    color?: string;
    manufacturingYear?: string;
    size: "standard" | "large";
    electric: boolean;
    photo?: string;
};
type Modal = "add" | "edit" | "delete" | null;
const MAX_VEHICLES = 10;

function formatPlate(value: string, category: Vehicle["category"]) {
    const normalized = value.trim().replace(/\s+/g, " ").toUpperCase();
    const compact = normalized.replace(/[ .-]/g, "");
    const parts = (
        category === "motorbike"
            ? /^(\d{2}[A-Z]\d)(\d{0,5})$/
            : /^(\d{2}[A-Z]{1,2})(\d{0,5})$/
    ).exec(compact);
    if (!parts) return normalized;
    const [, prefix, number] = parts;
    if (!number) return prefix;
    return `${prefix}-${number.slice(0, 3)}${number.length > 3 ? `.${number.slice(3)}` : ""}`;
}

function getVehicleDetails(vehicle: Vehicle) {
    return [
        {
            label: "Vehicle Brand",
            value: vehicle.brand,
            icon: "tag",
        },
        vehicle.model?.trim()
            ? {
                  label: "Vehicle Model",
                  value: vehicle.model.trim(),
                  icon: vehicle.category === "motorbike" ? "motorbike" : "car",
              }
            : null,
        vehicle.vehicleType
            ? {
                  label:
                      vehicle.category === "motorbike"
                          ? "Motorbike type"
                          : "Body type",
                  icon: vehicle.category === "motorbike" ? "motorbike" : "car",
                  value:
                      (vehicle.category === "motorbike"
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
                      (vehicle.category === "motorbike"
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
        category: "car",
        plate: "51K-123.45",
        brand: "Toyota",
        vehicleType: "sedan",
        seats: "5",
        fuelType: "electric",
        color: "white",
        manufacturingYear: "2023",
        size: "standard",
        electric: true,
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
        electric: false,
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
        electric: false,
        photo: "/images/parkwise/vehicle-motorbike-cutout.png",
    },
];

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

export function MyVehiclesPage({ fontClassName }: { fontClassName: string }) {
    const [vehicles, setVehicles] = useState<Vehicle[]>(sampleVehicles);
    const [modal, setModal] = useState<Modal>(null);
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [plate, setPlate] = useState("");
    const [category, setCategory] = useState<Vehicle["category"]>("car");
    const [brand, setBrand] = useState("");
    const [model, setModel] = useState("");
    const [vehicleType, setVehicleType] = useState("");
    const [seats, setSeats] = useState("");
    const [fuelType, setFuelType] = useState("");
    const [color, setColor] = useState("");
    const [manufacturingYear, setManufacturingYear] = useState("");
    const [size, setSize] = useState<Vehicle["size"]>("standard");
    const [electric, setElectric] = useState(false);
    const [photo, setPhoto] = useState<string | undefined>();
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
        const dialog = dialogRef.current;
        if (modal && dialog && !dialog.open) dialog.showModal();
        if (!modal && dialog?.open) dialog.close();
    }, [modal]);

    function openAdd() {
        if (vehicles.length >= MAX_VEHICLES) return;
        setSelectedId(null);
        setPlate("");
        setCategory("car");
        setBrand("");
        setModel("");
        setVehicleType("");
        setSeats("");
        setFuelType("");
        setColor("");
        setManufacturingYear("");
        setSize("standard");
        setElectric(false);
        setPhoto(undefined);
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
        setModel(vehicle.model ?? "");
        setVehicleType(vehicle.vehicleType ?? "");
        setSeats(vehicle.seats ?? "");
        setFuelType(vehicle.fuelType ?? "");
        setColor(vehicle.color ?? "");
        setManufacturingYear(vehicle.manufacturingYear ?? "");
        setSize(vehicle.size);
        setElectric(vehicle.electric);
        setPhoto(vehicle.photo);
        setError("");
        setBrandError("");
        setPhotoError("");
        setModal("edit");
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
    function saveVehicle(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const nextPlate = formatPlate(plate, category);
        const nextBrand = brand.trim();
        setBrandError(nextBrand ? "" : "Select or enter a vehicle brand.");
        if (modal === "add" && !/^[A-Z0-9 .-]{5,15}$/.test(nextPlate)) {
            setError("Enter a license plate with 5–15 letters or numbers.");
            return;
        }
        if (
            modal === "add" &&
            vehicles.some((vehicle) => vehicle.plate === nextPlate)
        ) {
            setError("This license plate is already registered here.");
            return;
        }
        if (!nextBrand) {
            document.getElementById("vehicles-brand")?.focus();
            return;
        }
        if (modal === "add") {
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
                    electric,
                    photo,
                },
            ]);
            setNotice("Vehicle added for this session.");
        } else if (modal === "edit" && selectedId) {
            setVehicles((current) =>
                current.map((vehicle) =>
                    vehicle.id === selectedId
                        ? {
                              ...vehicle,
                              category,
                              brand: nextBrand,
                              model: model.trim(),
                              vehicleType,
                              seats,
                              fuelType,
                              color,
                              manufacturingYear,
                              size,
                              electric,
                              photo,
                          }
                        : vehicle,
                ),
            );
            setNotice("Vehicle updated for this session.");
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
        setPhotoError("");
    }
    function removeVehicle() {
        if (!selectedId) return;
        setVehicles((current) =>
            current.filter((vehicle) => vehicle.id !== selectedId),
        );
        setNotice("Vehicle removed for this session.");
        closeModal();
    }

    return (
        <div
            lang="en"
            className={`parkwise-page vehicles-page ${fontClassName}`}
        >
            <ParkWiseNavbar
                variant="customer"
                navigationMode="customer"
                activeHref="/customer/vehicles"
                accountControl={
                    <CustomerAccountMenu activeHref="/customer/vehicles" />
                }
            />
            <main className="vehicles-main">
                <div className="vehicles-layout">
                    <div className="vehicles-primary-column">
                        <div className="vehicles-heading">
                            <h1>My Vehicles</h1>
                            <p>
                                Manage your registered vehicles for a smoother
                                parking experience.
                            </p>
                        </div>
                        <section
                            className="vehicles-summary"
                            aria-label="Registered vehicles"
                        >
                            <span className="vehicles-summary-icon">
                                <Icon name="car" size={25} />
                            </span>
                            <div className="vehicles-summary-stats">
                                <span>Registered vehicles</span>
                                <strong>
                                    {vehicles.length} / {MAX_VEHICLES}
                                </strong>
                            </div>
                            <div
                                className="vehicles-progress"
                                role="progressbar"
                                aria-label="Vehicle slots used"
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
                                <Icon name="plus" /> Add Vehicle
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
                                                    className={`vehicles-badge ${vehicle.category === "motorbike" ? "motorbike" : vehicle.size}`}
                                                >
                                                    <Icon
                                                        name={
                                                            vehicle.category ===
                                                            "motorbike"
                                                                ? "motorbike"
                                                                : "car"
                                                        }
                                                        size={16}
                                                    />
                                                    {vehicle.category ===
                                                    "motorbike"
                                                        ? "Motorbike"
                                                        : vehicle.size ===
                                                            "standard"
                                                          ? "Standard"
                                                          : "Large"}
                                                </span>
                                                {vehicle.electric && (
                                                    <span className="vehicles-badge electric">
                                                        <Icon
                                                            name="bolt"
                                                            size={14}
                                                        />{" "}
                                                        Electric
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                        <div className="vehicles-card-image">
                                            {vehicle.photo ? (
                                                <Image
                                                    src={vehicle.photo}
                                                    alt={`Vehicle ${vehicle.plate}`}
                                                    fill
                                                    sizes="(max-width: 700px) 90vw, 360px"
                                                    unoptimized={vehicle.photo.startsWith(
                                                        "data:",
                                                    )}
                                                />
                                            ) : (
                                                <span className="vehicles-photo-placeholder">
                                                    <Icon
                                                        name={
                                                            vehicle.category ===
                                                            "motorbike"
                                                                ? "motorbike"
                                                                : "car"
                                                        }
                                                        size={56}
                                                    />
                                                </span>
                                            )}
                                        </div>
                                        <div className="vehicles-card-info">
                                            <h2>{vehicle.plate}</h2>
                                            <p className="vehicles-card-subtitle">
                                                {vehicle.category ===
                                                "motorbike"
                                                    ? "Motorbike parking"
                                                    : `${vehicle.size === "standard" ? "Standard" : "Large"} parking space`}
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
                                                                    {
                                                                        detail.label
                                                                    }
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
                                                Edit
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
                                                Delete
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
                                            ? "No vehicles added yet"
                                            : "Add another vehicle"}
                                    </strong>
                                    <span>
                                        You can register up to {MAX_VEHICLES}{" "}
                                        vehicles.
                                    </span>
                                    <span className="vehicles-outline-button">
                                        <Icon name="plus" size={17} />{" "}
                                        {vehicles.length === 0
                                            ? "Add Your First Vehicle"
                                            : "Add Vehicle"}
                                    </span>
                                </button>
                            )}
                        </div>
                    </div>
                    <aside
                        className="vehicles-aside"
                        aria-label="Vehicle information"
                    >
                        <section className="vehicles-guidelines">
                            <div className="vehicles-aside-title">
                                <span>
                                    <Icon name="book" />
                                </span>
                                <h2>Vehicle guidelines</h2>
                            </div>
                            <ul>
                                <li>
                                    <span>
                                        <Icon name="car" size={19} />
                                    </span>
                                    You can register up to 10 vehicles.
                                </li>
                                <li>
                                    <span>
                                        <Icon name="document" size={18} />
                                    </span>
                                    Cars and motorbikes can be registered.
                                </li>
                                <li>
                                    <span>
                                        <Icon name="bolt" size={18} />
                                    </span>
                                    Electric vehicles can be marked for a better
                                    parking experience.
                                </li>
                                <li>
                                    <span>
                                        <Icon name="info" size={18} />
                                    </span>
                                    The license plate cannot be edited. To
                                    change it, please delete the vehicle and add
                                    a new one.
                                </li>
                            </ul>
                        </section>
                        <section className="vehicles-help">
                            <div className="vehicles-aside-title">
                                <span>
                                    <Icon name="headset" size={24} />
                                </span>
                                <div>
                                    <h2>Need Help?</h2>
                                    <p>
                                        Our support team is here to assist you.
                                    </p>
                                </div>
                            </div>
                            <button
                                type="button"
                                className="vehicles-support-link"
                                onClick={() =>
                                    setNotice(
                                        "Support contact is not available yet.",
                                    )
                                }
                            >
                                Contact Support <Icon name="arrow" size={16} />
                            </button>
                        </section>
                    </aside>
                </div>
            </main>
            <ParkWiseFooter />
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
                        <h2 id="vehicles-dialog-title">Delete Vehicle?</h2>
                        <p>
                            Are you sure you want to remove this vehicle from
                            your account?
                        </p>
                        <strong className="vehicles-delete-plate">
                            <Icon
                                name={
                                    selectedVehicle?.category === "motorbike"
                                        ? "motorbike"
                                        : "car"
                                }
                                size={20}
                            />{" "}
                            {selectedVehicle?.plate}
                        </strong>
                        <div className="vehicles-dialog-actions">
                            <button
                                type="button"
                                className="vehicles-light-button"
                                onClick={closeModal}
                            >
                                Cancel
                            </button>
                            <button
                                type="button"
                                className="vehicles-delete-button"
                                onClick={removeVehicle}
                            >
                                Delete Vehicle
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
                                            ? "Add Vehicle"
                                            : "Edit Vehicle"}
                                    </h2>
                                    <p>
                                        {modal === "add"
                                            ? "Register a new vehicle to use ParkWise parking services."
                                            : "Update your vehicle details for ParkWise parking services."}
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
                                        <span className="vehicles-upload-icon">
                                            <Icon
                                                name={
                                                    category === "motorbike"
                                                        ? "motorbike"
                                                        : "car"
                                                }
                                                size={43}
                                            />
                                        </span>
                                        <strong>Upload Vehicle Photo</strong>
                                        <small>
                                            JPG, PNG or WebP · Max 5 MB
                                        </small>
                                        <span className="vehicles-photo-button">
                                            <Icon name="upload" size={19} />
                                            {photo
                                                ? "Change Photo"
                                                : "Choose Photo"}
                                        </span>
                                        <span className="vehicles-upload-divider">
                                            <span>or</span>
                                        </span>
                                        <span className="vehicles-upload-preview">
                                            <Image
                                                src={
                                                    photo ??
                                                    (category === "motorbike"
                                                        ? "/images/parkwise/vehicle-motorbike-cutout.png"
                                                        : "/images/parkwise/vehicle-sedan-cutout.png")
                                                }
                                                alt={
                                                    photo
                                                        ? "Selected vehicle"
                                                        : ""
                                                }
                                                fill
                                                sizes="(max-width: 850px) 85vw, 320px"
                                                unoptimized={Boolean(
                                                    photo?.startsWith("data:"),
                                                )}
                                            />
                                        </span>
                                        <span className="vehicles-upload-caption">
                                            {photo
                                                ? "Your vehicle photo is ready. Choose another photo to replace it."
                                                : "Add a photo to help you recognize your vehicle more easily."}
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
                                    <fieldset className="vehicles-size-field vehicles-category-field">
                                        <legend>
                                            Vehicle Type{" "}
                                            <span aria-hidden="true">*</span>
                                        </legend>
                                        <div className="vehicles-size-options">
                                            {(
                                                ["car", "motorbike"] as const
                                            ).map((kind) => (
                                                <label
                                                    key={kind}
                                                    className={`vehicles-size-option ${category === kind ? "selected" : ""}`}
                                                >
                                                    <input
                                                        type="radio"
                                                        name="vehicles-category"
                                                        value={kind}
                                                        checked={
                                                            category === kind
                                                        }
                                                        onChange={() => {
                                                            if (
                                                                category ===
                                                                kind
                                                            )
                                                                return;
                                                            setCategory(kind);
                                                            setBrand("");
                                                            setModel("");
                                                            setVehicleType("");
                                                            setSeats("");
                                                            setFuelType("");
                                                            setElectric(false);
                                                            setPhoto(undefined);
                                                            setBrandError("");
                                                            setSize("standard");
                                                        }}
                                                    />
                                                    <span className="vehicles-size-indicator" />
                                                    <Icon
                                                        name={
                                                            kind === "car"
                                                                ? "car"
                                                                : "motorbike"
                                                        }
                                                        size={27}
                                                    />
                                                    <span>
                                                        <strong>
                                                            {kind === "car"
                                                                ? "Car"
                                                                : "Motorbike"}
                                                        </strong>
                                                        <small>
                                                            {kind === "car"
                                                                ? "Standard or large space"
                                                                : "Motorbike space"}
                                                        </small>
                                                    </span>
                                                </label>
                                            ))}
                                        </div>
                                    </fieldset>
                                    <div className="vehicles-field">
                                        <label htmlFor="vehicles-plate">
                                            License Plate{" "}
                                            {modal === "add" && (
                                                <span aria-hidden="true">
                                                    *
                                                </span>
                                            )}
                                        </label>
                                        <input
                                            id="vehicles-plate"
                                            ref={plateInputRef}
                                            value={plate}
                                            onChange={(event) =>
                                                updatePlate(
                                                    event.target.value,
                                                    event.target.selectionStart,
                                                )
                                            }
                                            onBlur={() =>
                                                setPlate((current) =>
                                                    formatPlate(
                                                        current,
                                                        category,
                                                    ),
                                                )
                                            }
                                            placeholder={
                                                category === "motorbike"
                                                    ? "e.g. 59A1-123.45"
                                                    : "e.g. 51K-123.45"
                                            }
                                            maxLength={15}
                                            disabled={modal === "edit"}
                                            autoCapitalize="characters"
                                            spellCheck={false}
                                            aria-required={modal === "add"}
                                            aria-invalid={Boolean(error)}
                                            aria-describedby={
                                                error
                                                    ? "vehicles-form-error"
                                                    : modal === "edit"
                                                      ? "vehicles-plate-hint"
                                                      : undefined
                                            }
                                        />
                                        {modal === "edit" && (
                                            <small id="vehicles-plate-hint">
                                                The license plate cannot be
                                                edited.
                                            </small>
                                        )}
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
                                    <div className="vehicles-field-grid">
                                        <div className="vehicles-field">
                                            <label htmlFor="vehicles-brand">
                                                Vehicle Brand{" "}
                                                <span aria-hidden="true">
                                                    *
                                                </span>
                                            </label>
                                            <VehicleDropdown
                                                id="vehicles-brand"
                                                value={brand}
                                                options={
                                                    category === "motorbike"
                                                        ? MOTORBIKE_BRAND_OPTIONS
                                                        : BRAND_OPTIONS
                                                }
                                                placeholder="Select brand"
                                                onChange={(nextBrand) => {
                                                    setBrand(nextBrand);
                                                    setBrandError("");
                                                    setModel("");
                                                }}
                                                required
                                                invalid={Boolean(brandError)}
                                                describedBy={
                                                    brandError
                                                        ? "vehicles-brand-error"
                                                        : undefined
                                                }
                                                searchable
                                                otherLabel="Other — enter a brand"
                                                leadingIcon={
                                                    <Icon
                                                        name={
                                                            category ===
                                                            "motorbike"
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
                                                Vehicle Model{" "}
                                                <span>(Optional)</span>
                                            </label>
                                            <VehicleDropdown
                                                id="vehicles-model"
                                                value={model}
                                                options={
                                                    category === "motorbike"
                                                        ? getMotorbikeModelOptions(
                                                              brand,
                                                          )
                                                        : getModelOptions(brand)
                                                }
                                                placeholder="Select model"
                                                onChange={setModel}
                                                searchable
                                                otherLabel="Other — enter a model"
                                                emptyMessage={
                                                    brand
                                                        ? "No suggested models for this brand"
                                                        : "Select a brand to see models"
                                                }
                                            />
                                        </div>
                                        <div className="vehicles-field">
                                            <label htmlFor="vehicles-type">
                                                {category === "motorbike"
                                                    ? "Motorbike Type"
                                                    : "Body Type"}{" "}
                                                <span>(Optional)</span>
                                            </label>
                                            <VehicleDropdown
                                                id="vehicles-type"
                                                value={vehicleType}
                                                options={
                                                    category === "motorbike"
                                                        ? MOTORBIKE_TYPE_OPTIONS
                                                        : VEHICLE_TYPE_OPTIONS
                                                }
                                                placeholder="Select type"
                                                onChange={setVehicleType}
                                                leadingIcon={
                                                    <Icon
                                                        name={
                                                            category ===
                                                            "motorbike"
                                                                ? "motorbike"
                                                                : "car"
                                                        }
                                                        size={20}
                                                    />
                                                }
                                            />
                                        </div>
                                        {category === "car" && (
                                            <div className="vehicles-field">
                                                <label htmlFor="vehicles-seats">
                                                    Number of Seats{" "}
                                                    <span>(Optional)</span>
                                                </label>
                                                <VehicleDropdown
                                                    id="vehicles-seats"
                                                    value={seats}
                                                    options={SEAT_OPTIONS}
                                                    placeholder="Select seats"
                                                    onChange={setSeats}
                                                    leadingIcon={
                                                        <Icon
                                                            name="seats"
                                                            size={20}
                                                        />
                                                    }
                                                />
                                            </div>
                                        )}
                                    </div>
                                    {category === "car" && (
                                        <fieldset className="vehicles-size-field">
                                            <legend>
                                                Parking Space Size{" "}
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
                                                        size === "standard"
                                                            ? "vehicles-size-option selected"
                                                            : "vehicles-size-option"
                                                    }
                                                >
                                                    <input
                                                        type="radio"
                                                        name="vehicles-size"
                                                        value="standard"
                                                        checked={
                                                            size === "standard"
                                                        }
                                                        onChange={() =>
                                                            setSize("standard")
                                                        }
                                                    />
                                                    <span className="vehicles-size-indicator" />
                                                    <Icon
                                                        name="car"
                                                        size={31}
                                                    />
                                                    <span>
                                                        <strong>
                                                            Standard
                                                        </strong>
                                                        <small>
                                                            For most cars
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
                                                            size === "large"
                                                        }
                                                        onChange={() =>
                                                            setSize("large")
                                                        }
                                                    />
                                                    <span className="vehicles-size-indicator" />
                                                    <Icon
                                                        name="car"
                                                        size={31}
                                                    />
                                                    <span>
                                                        <strong>Large</strong>
                                                        <small>
                                                            For larger vehicles
                                                            (SUV, MPV, Pickup)
                                                        </small>
                                                    </span>
                                                </label>
                                            </div>
                                        </fieldset>
                                    )}
                                    {category === "motorbike" && (
                                        <p className="vehicles-motorbike-note">
                                            <Icon name="motorbike" size={22} />
                                            Motorbike parking space is selected
                                            automatically.
                                        </p>
                                    )}
                                    <div className="vehicles-field-grid vehicles-field-grid--extra">
                                        <div className="vehicles-field">
                                            <label htmlFor="vehicles-fuel">
                                                Fuel Type{" "}
                                                <span>(Optional)</span>
                                            </label>
                                            <VehicleDropdown
                                                id="vehicles-fuel"
                                                value={fuelType}
                                                options={
                                                    category === "motorbike"
                                                        ? MOTORBIKE_FUEL_OPTIONS
                                                        : FUEL_OPTIONS
                                                }
                                                placeholder="Select fuel type"
                                                onChange={(nextFuel) => {
                                                    setFuelType(nextFuel);
                                                    setElectric(
                                                        nextFuel === "electric",
                                                    );
                                                }}
                                                leadingIcon={
                                                    <Icon
                                                        name="fuel"
                                                        size={19}
                                                    />
                                                }
                                            />
                                        </div>
                                        <label className="vehicles-electric-checkbox">
                                            <input
                                                type="checkbox"
                                                checked={electric}
                                                onChange={(event) => {
                                                    const isElectric =
                                                        event.target.checked;
                                                    setElectric(isElectric);
                                                    setFuelType((current) =>
                                                        isElectric
                                                            ? "electric"
                                                            : current ===
                                                                "electric"
                                                              ? ""
                                                              : current,
                                                    );
                                                }}
                                            />
                                            <Icon name="bolt" size={25} />
                                            This is an electric vehicle
                                        </label>
                                        <div className="vehicles-field">
                                            <label htmlFor="vehicles-color">
                                                Vehicle Color{" "}
                                                <span>(Optional)</span>
                                            </label>
                                            <VehicleDropdown
                                                id="vehicles-color"
                                                value={color}
                                                options={COLOR_OPTIONS}
                                                placeholder="Select color"
                                                onChange={setColor}
                                                leadingIcon={
                                                    <span
                                                        className="vehicles-color-swatch"
                                                        style={{
                                                            backgroundColor:
                                                                COLOR_SWATCHES[
                                                                    color
                                                                ] ?? "#fff",
                                                        }}
                                                    />
                                                }
                                            />
                                        </div>
                                        <div className="vehicles-field">
                                            <label htmlFor="vehicles-year">
                                                Manufacturing Year{" "}
                                                <span>(Optional)</span>
                                            </label>
                                            <VehicleDropdown
                                                id="vehicles-year"
                                                value={manufacturingYear}
                                                options={YEAR_OPTIONS}
                                                placeholder="Select year"
                                                onChange={setManufacturingYear}
                                                leadingIcon={
                                                    <Icon
                                                        name="calendar"
                                                        size={19}
                                                    />
                                                }
                                            />
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div className="vehicles-dialog-actions">
                                <button
                                    type="button"
                                    className="vehicles-light-button"
                                    onClick={closeModal}
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    className="vehicles-dark-button"
                                >
                                    {modal === "add"
                                        ? "Add Vehicle"
                                        : "Save Changes"}
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
