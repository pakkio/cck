import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
export declare function registerApplyPrefabOverridesTool(server: McpServer, mcpUnity: McpUnity, logger: Logger): void;
export declare function registerRevertPrefabOverridesTool(server: McpServer, mcpUnity: McpUnity, logger: Logger): void;
export declare function registerUnpackPrefabTool(server: McpServer, mcpUnity: McpUnity, logger: Logger): void;
