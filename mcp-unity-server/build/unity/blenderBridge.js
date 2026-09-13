import WebSocket from 'ws';
import { v4 as uuidv4 } from 'uuid';
export var BlenderConnectionState;
(function (BlenderConnectionState) {
    BlenderConnectionState["Disconnected"] = "disconnected";
    BlenderConnectionState["Connecting"] = "connecting";
    BlenderConnectionState["Connected"] = "connected";
    BlenderConnectionState["Reconnecting"] = "reconnecting";
})(BlenderConnectionState || (BlenderConnectionState = {}));
export class BlenderBridge {
    logger;
    host;
    port;
    ws = null;
    state = BlenderConnectionState.Disconnected;
    pendingRequests = new Map();
    reconnectAttempt = 0;
    maxReconnectAttempts = 50;
    minReconnectDelay = 1000;
    maxReconnectDelay = 30000;
    reconnectMultiplier = 2;
    requestTimeout = 15000;
    constructor(logger, host = '127.0.0.1', port = 9876) {
        this.logger = logger;
        this.host = host;
        this.port = port;
    }
    get url() {
        return `ws://${this.host}:${this.port}`;
    }
    get connectionState() {
        return this.state;
    }
    get isConnected() {
        return this.state === BlenderConnectionState.Connected && this.ws?.readyState === WebSocket.OPEN;
    }
    async connect() {
        if (this.isConnected)
            return;
        this.state = BlenderConnectionState.Connecting;
        this.reconnectAttempt++;
        this.logger.info(`Connecting to Blender bridge at ${this.url} (attempt ${this.reconnectAttempt})`);
        return new Promise((resolve, reject) => {
            try {
                this.ws = new WebSocket(this.url, { handshakeTimeout: 5000 });
                const connTimeout = setTimeout(() => {
                    if (this.state !== BlenderConnectionState.Connected) {
                        this.ws?.terminate();
                        reject(new Error('Connection timeout'));
                    }
                }, 5000);
                this.ws.on('open', () => {
                    clearTimeout(connTimeout);
                    this.state = BlenderConnectionState.Connected;
                    this.reconnectAttempt = 0;
                    this.logger.info(`Connected to Blender bridge at ${this.url}`);
                    resolve();
                });
                this.ws.on('message', (data) => {
                    this.handleMessage(data.toString());
                });
                this.ws.on('close', (code, reason) => {
                    this.state = BlenderConnectionState.Disconnected;
                    this.rejectAllPending('Connection to Blender bridge was closed');
                    if (code !== 1000)
                        this.scheduleReconnect();
                });
                this.ws.on('error', (err) => {
                    clearTimeout(connTimeout);
                    this.logger.error(`Blender bridge error: ${err.message}`);
                    this.state = BlenderConnectionState.Disconnected;
                    this.rejectAllPending(`Connection error: ${err.message}`);
                    reject(err);
                });
            }
            catch (err) {
                this.state = BlenderConnectionState.Disconnected;
                reject(err);
            }
        });
    }
    handleMessage(raw) {
        try {
            const msg = JSON.parse(raw);
            if (!msg.id)
                return;
            const pending = this.pendingRequests.get(msg.id);
            if (!pending)
                return;
            clearTimeout(pending.timeout);
            this.pendingRequests.delete(msg.id);
            if (msg.error) {
                pending.reject(new Error(msg.error.message || 'Blender bridge error'));
            }
            else {
                pending.resolve(msg.result);
            }
        }
        catch (err) {
            this.logger.warn(`Failed to parse Blender bridge message: ${err}`);
        }
    }
    rejectAllPending(reason) {
        for (const [, pending] of this.pendingRequests) {
            clearTimeout(pending.timeout);
            pending.reject(new Error(reason));
        }
        this.pendingRequests.clear();
    }
    scheduleReconnect() {
        if (this.reconnectAttempt >= this.maxReconnectAttempts) {
            this.logger.error('Max Blender reconnect attempts reached');
            return;
        }
        this.state = BlenderConnectionState.Reconnecting;
        const delay = Math.min(this.minReconnectDelay * Math.pow(this.reconnectMultiplier, this.reconnectAttempt - 1), this.maxReconnectDelay);
        setTimeout(() => { this.connect().catch(() => { }); }, delay);
    }
    async sendRequest(method, params, timeoutMs) {
        if (!this.isConnected)
            throw new Error('Not connected to Blender bridge');
        const id = uuidv4();
        const timeout = timeoutMs || this.requestTimeout;
        return new Promise((resolve, reject) => {
            const timeoutHandle = setTimeout(() => {
                this.pendingRequests.delete(id);
                reject(new Error(`Timed out waiting for '${method}' response`));
            }, timeout);
            this.pendingRequests.set(id, { resolve, reject, method, timeout: timeoutHandle });
            this.ws.send(JSON.stringify({ id, method, params }), (err) => {
                if (err) {
                    clearTimeout(timeoutHandle);
                    this.pendingRequests.delete(id);
                    reject(err);
                }
            });
        });
    }
    async disconnect() {
        this.rejectAllPending('Disconnecting');
        if (this.ws) {
            this.ws.close(1000, 'Normal close');
            this.ws = null;
        }
        this.state = BlenderConnectionState.Disconnected;
    }
}
