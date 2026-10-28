import type { VehicleOption } from "./VehicleDropdown";

// Suggestions only: customers can enter a make or model that is not listed.
const brands = [
    "Abarth",
    "Acura",
    "Alfa Romeo",
    "Aston Martin",
    "Audi",
    "BAIC",
    "Bentley",
    "Bestune",
    "BMW",
    "Buick",
    "BYD",
    "Cadillac",
    "Changan",
    "Chery",
    "Chevrolet",
    "Chrysler",
    "Citroën",
    "Cupra",
    "Daihatsu",
    "Denza",
    "Dodge",
    "Dongfeng",
    "DS",
    "Ferrari",
    "Fiat",
    "Ford",
    "GAC",
    "Geely",
    "Genesis",
    "GMC",
    "Great Wall",
    "Haval",
    "Honda",
    "Hongqi",
    "Hyundai",
    "Infiniti",
    "Isuzu",
    "Jaguar",
    "Jeep",
    "Kia",
    "Lamborghini",
    "Land Rover",
    "Lexus",
    "Li Auto",
    "Lincoln",
    "Lotus",
    "Lucid",
    "Maserati",
    "Mazda",
    "McLaren",
    "Mercedes-Benz",
    "MG",
    "MINI",
    "Mitsubishi",
    "Nissan",
    "Opel",
    "Peugeot",
    "Polestar",
    "Porsche",
    "Proton",
    "Renault",
    "Rolls-Royce",
    "SEAT",
    "Škoda",
    "Smart",
    "Subaru",
    "Suzuki",
    "Tata",
    "Tesla",
    "Toyota",
    "VinFast",
    "Volkswagen",
    "Volvo",
    "Wuling",
    "XPeng",
    "Zeekr",
];

export const BRAND_OPTIONS: VehicleOption[] = brands.map((brand) => ({
    value: brand,
    label: brand,
}));

export const MOTORBIKE_BRAND_OPTIONS: VehicleOption[] = [
    "Honda",
    "Yamaha",
    "Suzuki",
    "SYM",
    "Piaggio",
    "Vespa",
    "VinFast",
    "Dat Bike",
    "Yadea",
    "Kawasaki",
    "Bajaj",
    "BMW",
    "Ducati",
    "KTM",
    "Other",
].map((brand) => ({ value: brand, label: brand }));

const motorbikeModelsByBrand: Record<string, string[]> = {
    Honda: [
        "Air Blade",
        "Future",
        "Lead",
        "SH",
        "Vision",
        "Wave Alpha",
        "Winner X",
    ],
    Yamaha: ["Exciter", "Grande", "Janus", "Jupiter", "NVX", "Sirius"],
    Suzuki: ["Address", "Burgman Street", "Raider", "Satria"],
    SYM: ["Attila", "Elegant", "Galaxy", "Husky"],
    Piaggio: ["Liberty", "Medley", "Zip"],
    Vespa: ["GTS", "Primavera", "Sprint"],
    VinFast: ["Evo", "Feliz", "Klara", "Vento", "Theon"],
    "Dat Bike": ["Quantum", "Weaver"],
    Yadea: ["Oris", "Vekoo", "Voltguard"],
};

export function getMotorbikeModelOptions(brand: string): VehicleOption[] {
    return (motorbikeModelsByBrand[brand] ?? []).map((model) => ({
        value: model,
        label: model,
    }));
}

const modelsByBrand: Record<string, string[]> = {
    Audi: ["A3", "A4", "A6", "Q3", "Q5", "Q7"],
    BMW: ["3 Series", "5 Series", "X1", "X3", "X5"],
    BYD: ["Atto 3", "Dolphin", "Seal", "Sealion 6"],
    Chevrolet: ["Captiva", "Colorado", "Spark", "Trailblazer"],
    Ford: ["EcoSport", "Everest", "Explorer", "Ranger", "Territory"],
    Honda: ["Accord", "BR-V", "City", "Civic", "CR-V", "HR-V"],
    Hyundai: ["Accent", "Creta", "Elantra", "Santa Fe", "Tucson"],
    Isuzu: ["D-Max", "mu-X"],
    Kia: ["Carnival", "K3", "Seltos", "Sonet", "Sorento", "Sportage"],
    Lexus: ["ES", "NX", "RX", "UX"],
    Mazda: ["CX-3", "CX-5", "CX-8", "Mazda2", "Mazda3", "Mazda6"],
    "Mercedes-Benz": ["A-Class", "C-Class", "E-Class", "GLA", "GLC", "GLE"],
    MG: ["MG4", "MG5", "HS", "ZS"],
    Mitsubishi: ["Attrage", "Outlander", "Triton", "Xpander", "Xforce"],
    Nissan: ["Almera", "Kicks", "Navara", "Terra"],
    Peugeot: ["2008", "3008", "5008", "408"],
    Subaru: ["Forester", "Outback", "XV"],
    Suzuki: ["Ciaz", "Ertiga", "Swift", "XL7"],
    Tesla: ["Model 3", "Model S", "Model X", "Model Y"],
    Toyota: [
        "Camry",
        "Corolla Cross",
        "Fortuner",
        "Innova Cross",
        "Vios",
        "Yaris Cross",
    ],
    VinFast: ["VF 3", "VF 5", "VF 6", "VF 7", "VF 8", "VF 9", "VF e34"],
    Volkswagen: ["Golf", "Polo", "T-Cross", "Tiguan", "Touareg"],
    Volvo: ["EX30", "S60", "XC40", "XC60", "XC90"],
    Wuling: ["Mini EV"],
};

export function getModelOptions(brand: string): VehicleOption[] {
    return (modelsByBrand[brand] ?? []).map((model) => ({
        value: model,
        label: model,
    }));
}

export const VEHICLE_TYPE_OPTIONS: VehicleOption[] = [
    { value: "sedan", label: "Sedan" },
    { value: "suv", label: "SUV" },
    { value: "hatchback", label: "Hatchback" },
    { value: "pickup", label: "Pickup" },
    { value: "van", label: "Van / MPV" },
    { value: "coupe", label: "Coupe" },
];

export const MOTORBIKE_TYPE_OPTIONS: VehicleOption[] = [
    { value: "scooter", label: "Scooter" },
    { value: "underbone", label: "Underbone" },
    { value: "manual", label: "Manual motorcycle" },
    { value: "sport", label: "Sport motorcycle" },
    { value: "electric-bike", label: "Electric motorbike" },
];

export const MOTORBIKE_FUEL_OPTIONS: VehicleOption[] = [
    { value: "gasoline", label: "Gasoline" },
    { value: "electric", label: "Electric" },
    { value: "other", label: "Other" },
];

export const SEAT_OPTIONS: VehicleOption[] = [2, 4, 5, 7, 8, 9].map(
    (count) => ({ value: String(count), label: `${count} seats` }),
);

export const FUEL_OPTIONS: VehicleOption[] = [
    { value: "gasoline", label: "Gasoline" },
    { value: "diesel", label: "Diesel" },
    { value: "hybrid", label: "Hybrid" },
    { value: "plug-in-hybrid", label: "Plug-in Hybrid" },
    { value: "electric", label: "Electric" },
    { value: "lpg", label: "LPG" },
    { value: "other", label: "Other" },
];

export const COLOR_OPTIONS: VehicleOption[] = [
    "White",
    "Black",
    "Silver",
    "Gray",
    "Blue",
    "Red",
    "Green",
    "Brown",
    "Beige",
    "Yellow",
    "Orange",
    "Other",
].map((color) => ({ value: color.toLowerCase(), label: color }));

export const COLOR_SWATCHES: Record<string, string> = {
    white: "#ffffff",
    black: "#24262c",
    silver: "#c5cad2",
    gray: "#777f8d",
    blue: "#3074ce",
    red: "#d94b4b",
    green: "#469b61",
    brown: "#8e6549",
    beige: "#e3d7bc",
    yellow: "#f3d34e",
    orange: "#eb914a",
};

const CURRENT_YEAR = new Date().getFullYear();
export const YEAR_OPTIONS: VehicleOption[] = Array.from(
    { length: CURRENT_YEAR - 1979 },
    (_, index) => {
        const year = String(CURRENT_YEAR - index);
        return { value: year, label: year };
    },
);
