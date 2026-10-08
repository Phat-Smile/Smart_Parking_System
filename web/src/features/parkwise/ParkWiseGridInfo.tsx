const stats = [
    ["Explore", "Nearby parking"],
    ["Compare", "Parking options"],
    ["Preview", "Parking layouts"],
    ["Plan", "Your arrival"],
];

/** src/Components/Pages/GridInfo.jsx. */
export function ParkWiseGridInfo() {
    return (
        <div className="parkwise-stats">
            {stats.map(([value, label]) => (
                <div key={label}>
                    <h3>{value}</h3>
                    <p>{label}</p>
                </div>
            ))}
        </div>
    );
}
