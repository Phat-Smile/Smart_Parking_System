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

type Vehicle = {
    id: string;
    plate: string;
    size: "standard" | "large";
    electric: boolean;
    photo?: string;
};
type Modal = "add" | "edit" | "delete" | null;
const MAX_VEHICLES = 10;
const sampleVehicles: Vehicle[] = [
    {
        id: "sample-sedan",
        plate: "51K-123.45",
        size: "standard",
        electric: true,
        photo: "/images/parkwise/vehicle-sedan.png",
    },
    {
        id: "sample-suv",
        plate: "30A-987.65",
        size: "large",
        electric: false,
        photo: "/images/parkwise/vehicle-suv.png",
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
        book: (
            <>
                <path d="M12 6c-2-1.5-4.7-1.6-8-1v13c3.3-.6 6-.5 8 1 2-1.5 4.7-1.6 8-1V5c-3.3-.6-6-.5-8 1Z" />
                <path d="M12 6v13" />
            </>
        ),
        bolt: <path d="m13 2-8 11h6l-1 9 9-12h-6V2Z" />,
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
        more: (
            <>
                <circle cx="12" cy="5" r="1" />
                <circle cx="12" cy="12" r="1" />
                <circle cx="12" cy="19" r="1" />
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
    const [size, setSize] = useState<Vehicle["size"]>("standard");
    const [electric, setElectric] = useState(false);
    const [photo, setPhoto] = useState<string | undefined>();
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const dialogRef = useRef<HTMLDialogElement>(null);
    const fileRef = useRef<HTMLInputElement>(null);
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
        setSize("standard");
        setElectric(false);
        setPhoto(undefined);
        setError("");
        setModal("add");
    }
    function openEdit(vehicle: Vehicle) {
        setSelectedId(vehicle.id);
        setPlate(vehicle.plate);
        setSize(vehicle.size);
        setElectric(vehicle.electric);
        setPhoto(vehicle.photo);
        setError("");
        setModal("edit");
    }
    function closeModal() {
        setModal(null);
        setError("");
    }
    function saveVehicle(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const nextPlate = plate.trim().replace(/\s+/g, " ").toUpperCase();
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
        if (modal === "add") {
            setVehicles((current) => [
                ...current,
                {
                    id: crypto.randomUUID(),
                    plate: nextPlate,
                    size,
                    electric,
                    photo,
                },
            ]);
            setNotice("Vehicle added to this design preview.");
        } else if (modal === "edit" && selectedId) {
            setVehicles((current) =>
                current.map((vehicle) =>
                    vehicle.id === selectedId
                        ? { ...vehicle, size, electric, photo }
                        : vehicle,
                ),
            );
            setNotice("Vehicle updated in this design preview.");
        }
        closeModal();
    }
    function handlePhoto(file?: File) {
        if (!file) return;
        if (
            !["image/jpeg", "image/png", "image/webp"].includes(file.type) ||
            file.size > 5 * 1024 * 1024
        ) {
            setError("Choose a JPG, PNG or WebP image under 5 MB.");
            return;
        }
        const reader = new FileReader();
        reader.onload = () => {
            if (typeof reader.result === "string") setPhoto(reader.result);
        };
        reader.readAsDataURL(file);
        setError("");
    }
    function removeVehicle() {
        if (!selectedId) return;
        setVehicles((current) =>
            current.filter((vehicle) => vehicle.id !== selectedId),
        );
        setNotice("Vehicle removed from this design preview.");
        closeModal();
    }

    return (
        <div
            lang="en"
            className={`parkwise-page vehicles-page ${fontClassName}`}
        >
            <ParkWiseNavbar variant="login" />
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
                            {vehicles.map((vehicle) => (
                                <article
                                    className="vehicles-card"
                                    key={vehicle.id}
                                >
                                    <div className="vehicles-card-top">
                                        <div className="vehicles-badges">
                                            <span
                                                className={`vehicles-badge ${vehicle.size}`}
                                            >
                                                {vehicle.size === "standard"
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
                                        <button
                                            type="button"
                                            className="vehicles-more"
                                            aria-label={`Edit ${vehicle.plate}`}
                                            onClick={() => openEdit(vehicle)}
                                        >
                                            <Icon name="more" size={19} />
                                        </button>
                                    </div>
                                    <div className="vehicles-card-image">
                                        {vehicle.photo ? (
                                            <Image
                                                src={vehicle.photo}
                                                alt={`${vehicle.size} vehicle`}
                                                fill
                                                sizes="(max-width: 700px) 90vw, 360px"
                                                unoptimized={vehicle.photo.startsWith(
                                                    "data:",
                                                )}
                                            />
                                        ) : (
                                            <span className="vehicles-photo-placeholder">
                                                <Icon name="car" size={56} />
                                            </span>
                                        )}
                                    </div>
                                    <h2>{vehicle.plate}</h2>
                                    <p>
                                        <Icon name="car" size={16} />{" "}
                                        {vehicle.size === "standard"
                                            ? "Standard Vehicle"
                                            : "Large Vehicle"}
                                    </p>
                                    {vehicle.electric && (
                                        <p className="vehicles-electric-line">
                                            <Icon name="bolt" size={16} />{" "}
                                            Electric Vehicle
                                        </p>
                                    )}
                                    <div className="vehicles-card-actions">
                                        <button
                                            type="button"
                                            onClick={() => openEdit(vehicle)}
                                        >
                                            <Icon name="edit" size={16} /> Edit
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
                            ))}
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
                        <p className="vehicles-preview-caption">
                            Design preview: vehicle changes stay on this page
                            and are not saved to your account.
                        </p>
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
                                    Only cars are currently supported (standard
                                    or large).
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
                                        "Support contact is not connected in this design preview.",
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
                className="vehicles-dialog"
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
                            <Icon name="car" size={20} />{" "}
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
                                <h2 id="vehicles-dialog-title">
                                    {modal === "add"
                                        ? "Add Vehicle"
                                        : "Edit Vehicle"}
                                </h2>
                                <button
                                    type="button"
                                    className="vehicles-dialog-close"
                                    aria-label="Close"
                                    onClick={closeModal}
                                >
                                    <Icon name="close" size={17} />
                                </button>
                            </div>
                            <div className="vehicles-form-body">
                                <div className="vehicles-upload">
                                    <input
                                        ref={fileRef}
                                        type="file"
                                        accept="image/jpeg,image/png,image/webp"
                                        className="vehicles-file-input"
                                        aria-label="Upload vehicle photo"
                                        onChange={(event) =>
                                            handlePhoto(event.target.files?.[0])
                                        }
                                    />
                                    <button
                                        type="button"
                                        className="vehicles-upload-target"
                                        onClick={() => fileRef.current?.click()}
                                    >
                                        {photo ? (
                                            <Image
                                                src={photo}
                                                alt="Selected vehicle"
                                                fill
                                                sizes="160px"
                                                unoptimized={photo.startsWith(
                                                    "data:",
                                                )}
                                            />
                                        ) : (
                                            <>
                                                <Icon name="car" size={32} />
                                                <strong>
                                                    Upload Vehicle Photo
                                                </strong>
                                                <small>
                                                    JPG, PNG or WebP · Max 5 MB
                                                </small>
                                            </>
                                        )}
                                    </button>
                                    {photo && (
                                        <button
                                            type="button"
                                            className="vehicles-change-photo"
                                            onClick={() =>
                                                fileRef.current?.click()
                                            }
                                        >
                                            <Icon name="camera" size={15} />{" "}
                                            Change Photo
                                        </button>
                                    )}
                                </div>
                                <div className="vehicles-form-fields">
                                    <label htmlFor="vehicles-plate">
                                        License Plate{" "}
                                        {modal === "add" && <span>*</span>}
                                    </label>
                                    <input
                                        id="vehicles-plate"
                                        value={plate}
                                        onChange={(event) => {
                                            setPlate(event.target.value);
                                            setError("");
                                        }}
                                        placeholder="Enter license plate (e.g. 51K12345)"
                                        maxLength={15}
                                        disabled={modal === "edit"}
                                        aria-invalid={Boolean(error)}
                                        aria-describedby={
                                            error
                                                ? "vehicles-form-error"
                                                : "vehicles-plate-hint"
                                        }
                                    />
                                    <small id="vehicles-plate-hint">
                                        {modal === "edit"
                                            ? "The license plate cannot be edited. Delete this vehicle and add a new one to change it."
                                            : "We will format it automatically."}
                                    </small>
                                    <label htmlFor="vehicles-size">
                                        Vehicle Type <span>*</span>
                                    </label>
                                    <select
                                        id="vehicles-size"
                                        value={size}
                                        onChange={(event) =>
                                            setSize(
                                                event.target
                                                    .value as Vehicle["size"],
                                            )
                                        }
                                    >
                                        <option value="standard">
                                            Standard Vehicle
                                        </option>
                                        <option value="large">
                                            Large Vehicle
                                        </option>
                                    </select>
                                    <label className="vehicles-electric-checkbox">
                                        <input
                                            type="checkbox"
                                            checked={electric}
                                            onChange={(event) =>
                                                setElectric(
                                                    event.target.checked,
                                                )
                                            }
                                        />
                                        <Icon name="bolt" size={17} /> This is
                                        an electric vehicle
                                    </label>
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
