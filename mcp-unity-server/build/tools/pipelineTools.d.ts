import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { Logger } from '../utils/logger.js';
import { BlenderBridge } from '../unity/blenderBridge.js';
export declare function registerBlenderExportVehicleFbxTool(server: McpServer, blender: BlenderBridge, logger: Logger): void;
export declare function registerBlenderGetVehicleInfoTool(server: McpServer, blender: BlenderBridge, logger: Logger): void;
export declare function registerBlenderImportVehicleToUnityTool(server: McpServer, logger: Logger): void;
export declare function registerPipelineBuildVehicleTool(server: McpServer, blender: BlenderBridge, mcpUnity: any, logger: Logger): void;
export declare function registerPipelineBridgeStatusTool(server: McpServer, blender: BlenderBridge, mcpUnity: any, logger: Logger): void;
