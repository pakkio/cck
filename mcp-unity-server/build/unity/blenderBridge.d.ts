import { Logger } from '../utils/logger.js';
export declare enum BlenderConnectionState {
    Disconnected = "disconnected",
    Connecting = "connecting",
    Connected = "connected",
    Reconnecting = "reconnecting"
}
export declare class BlenderBridge {
    private logger;
    private host;
    private port;
    private ws;
    private state;
    private pendingRequests;
    private reconnectAttempt;
    private maxReconnectAttempts;
    private minReconnectDelay;
    private maxReconnectDelay;
    private reconnectMultiplier;
    private requestTimeout;
    constructor(logger: Logger, host?: string, port?: number);
    get url(): string;
    get connectionState(): BlenderConnectionState;
    get isConnected(): boolean;
    connect(): Promise<void>;
    private handleMessage;
    private rejectAllPending;
    private scheduleReconnect;
    sendRequest(method: string, params: any, timeoutMs?: number): Promise<any>;
    disconnect(): Promise<void>;
}
