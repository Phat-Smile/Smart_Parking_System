// The part of Google Identity Services (https://accounts.google.com/gsi/client) the sign-in button uses.
interface GoogleCredentialResponse {
    credential: string;
}

interface GoogleIdConfiguration {
    client_id: string;
    callback: (response: GoogleCredentialResponse) => void;
    ux_mode?: "popup" | "redirect";
}

interface GoogleButtonConfiguration {
    type?: "standard" | "icon";
    theme?: "outline" | "filled_blue" | "filled_black";
    size?: "large" | "medium" | "small";
    shape?: "rectangular" | "pill" | "circle" | "square";
    text?: "signin_with" | "signup_with" | "continue_with" | "signin";
    logo_alignment?: "left" | "center";
    width?: number;
    locale?: string;
}

interface Window {
    google?: {
        accounts: {
            id: {
                initialize: (configuration: GoogleIdConfiguration) => void;
                renderButton: (
                    parent: HTMLElement,
                    options: GoogleButtonConfiguration,
                ) => void;
            };
        };
    };
}
