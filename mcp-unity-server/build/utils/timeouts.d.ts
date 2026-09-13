/**
 * Per-tool request timeout overrides, in milliseconds.
 *
 * The default request timeout (McpUnitySettings.RequestTimeoutSeconds, 10s minimum) is sized
 * for ordinary scene/GameObject operations. A handful of tools routinely run longer than that -
 * package resolution, domain reloads, test runs - and were silently relying on
 * McpUnity.sendRequest's unused `options.timeout` override never actually being passed. That
 * meant every one of these was one slow CI machine away from a spurious timeout.
 */
export declare const TOOL_TIMEOUTS_MS: Record<string, number>;
export declare function getToolTimeout(toolName: string): number | undefined;
