import * as z from 'zod';
import { McpUnityError, ErrorType } from '../utils/errors.js';
// Constants for the tool
const toolName = 'manage_cck_test_mode';
const toolDescription = "Enters/exits ChilloutVR CCK's built-in Test Mode (spawns a walkable player and builds a " +
    "temporary version of the selected World/Avatar/Prop), or reports whether it's currently active. This is the " +
    "same action as the Control Panel's 'Test in PlayMode' menu item, invoked directly since that item lives in a " +
    "native context menu no UI automation can click.";
const paramsSchema = z.object({
    action: z.enum(['enter', 'exit', 'status']).default('enter').describe('Which Test Mode action to perform'),
    objectPath: z.string().optional().describe('Hierarchy path of a specific CVRAssetInfo-bearing GameObject to test (defaults to auto-picking the World in the scene, or the only CVRAssetInfo found)'),
    instanceId: z.number().optional().describe('Instance ID alternative to objectPath')
});
/**
 * Creates and registers the CCK Test Mode tool with the MCP server
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerCvrTestModeTool(server, mcpUnity, logger) {
    logger.info(`Registering tool: ${toolName}`);
    server.tool(toolName, toolDescription, paramsSchema.shape, async (params) => {
        try {
            logger.info(`Executing tool: ${toolName}`, params);
            const result = await toolHandler(mcpUnity, params);
            logger.info(`Tool execution successful: ${toolName}`);
            return result;
        }
        catch (error) {
            logger.error(`Tool execution failed: ${toolName}`, error);
            throw error;
        }
    });
}
/**
 * Handles requests to enter/exit CCK Test Mode or query its status
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity, params) {
    const response = await mcpUnity.sendRequest({
        method: toolName,
        params: {
            action: params.action,
            objectPath: params.objectPath,
            instanceId: params.instanceId
        }
    });
    if (!response.success) {
        throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || `Failed to ${params.action} CCK Test Mode`);
    }
    return {
        content: [{
                type: 'text',
                text: response.message || JSON.stringify(response, null, 2)
            }]
    };
}
