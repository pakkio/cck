import * as z from 'zod';
import { McpUnityError, ErrorType } from '../utils/errors.js';
// Constants for the tool
const toolName = 'list_editor_windows';
const toolDescription = 'Lists all open Unity Editor windows (title, type name). Pass windowTitle to additionally ' +
    'list every clickable UI Toolkit element (Button and Button-like elements) inside that window, with its ' +
    'name/text/type - use this to discover what click_ui_element can target.';
const paramsSchema = z.object({
    windowTitle: z.string().optional().describe('Substring to match against an open window\'s title or type name. If provided, also lists the clickable elements inside the matched window.')
});
/**
 * Creates and registers the List Editor Windows tool with the MCP server
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerListEditorWindowsTool(server, mcpUnity, logger) {
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
 * Handles requests to list open editor windows and their clickable elements
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity, params) {
    const response = await mcpUnity.sendRequest({
        method: toolName,
        params: { windowTitle: params.windowTitle }
    });
    if (!response.success) {
        throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || 'Failed to list editor windows');
    }
    return {
        content: [{
                type: 'text',
                text: JSON.stringify(response, null, 2)
            }]
    };
}
