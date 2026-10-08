import Image from "next/image";

const steps = [
    {
        image: "box-delivery-package-car-pin-location-group.svg",
        title: "Find Your Location",
        text: "Search for parking spots in your desired area using our easy-to-use platform.",
    },
    {
        image: "car-checkmark.svg",
        title: "Choose Your Spot",
        text: "Compare prices and locations to select the perfect parking spot for your needs.",
    },
    {
        image: "hand-mobile-credit-card-pay-checkmark.svg",
        title: "Book & Pay Online",
        text: "Registered drivers can book and pay for a parking spot in advance.",
    },
    {
        image: "car-key-protection.svg",
        title: "Park with Ease",
        text: "Simply arrive at your reserved spot and enjoy hassle-free parking.",
    },
];

/** src/Components/Pages/HowItWorks.jsx, with Next image paths. */
export function ParkWiseHowItWorks() {
    return (
        <section id="how-it-works" className="parkwise-how">
            <div className="parkwise-container">
                <div className="parkwise-section-title">
                    <p>HOW IT WORKS</p>
                    <h2>
                        Simple Steps to Find Your{" "}
                        <br className="parkwise-desktop-break" /> Perfect
                        Parking Space
                    </h2>
                </div>
                <div className="parkwise-steps">
                    {steps.map((step) => (
                        <div className="parkwise-step" key={step.title}>
                            <Image
                                src={`/images/parkwise/${step.image}`}
                                alt=""
                                width={64}
                                height={64}
                            />
                            <h3>{step.title}</h3>
                            <p>{step.text}</p>
                        </div>
                    ))}
                </div>
            </div>
        </section>
    );
}
