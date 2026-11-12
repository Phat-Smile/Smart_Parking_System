import {
    HubConnectionBuilder,
    type HubConnection,
    type IHttpConnectionOptions,
} from "@microsoft/signalr";
import { getGatewayBaseUrl } from "@/shared/config/env";

export interface RealtimeConnectionOptions {
    hubPath: string;
    accessTokenFactory?: IHttpConnectionOptions["accessTokenFactory"];
    withCredentials?: boolean;
}

export const DEFAULT_RECONNECT_DELAYS_MS = [0, 2_000, 10_000, 30_000] as const;

export function createRealtimeConnection(
    options: RealtimeConnectionOptions,
): HubConnection {
    if (!options.hubPath.startsWith("/") || options.hubPath.startsWith("//")) {
        throw new Error("Hub path must be root-relative.");
    }
    const url = new URL(options.hubPath, `${getGatewayBaseUrl()}/`);
    if (url.origin !== new URL(getGatewayBaseUrl()).origin) {
        throw new Error("Hub path cannot change Gateway origin.");
    }

    return new HubConnectionBuilder()
        .withUrl(url.toString(), {
            ...(options.accessTokenFactory
                ? { accessTokenFactory: options.accessTokenFactory }
                : {}),
            ...(options.withCredentials !== undefined
                ? { withCredentials: options.withCredentials }
                : {}),
        })
        .withAutomaticReconnect([...DEFAULT_RECONNECT_DELAYS_MS])
        .build();
}
