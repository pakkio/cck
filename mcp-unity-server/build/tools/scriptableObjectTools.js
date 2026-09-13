import * as z from 'zod';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { getToolTimeout } from '../utils/timeouts.js';
// ============================================================================
// create_scriptable_object
// ============================================================================
const toolName = 'create_scriptable_object';
const toolDescription = 'Creates a new ScriptableObject asset file (.asset) in the project from a C# class name with optional serialized field values.';
const paramsSchema = z.object({
    className: z.string().describe('The name of the ScriptableObject C# class (e.g. "GameConfig", "ItemData")'),
    assetPath: z.string().describe('The project path where the .asset file should be saved (e.g. "Assets/Data/GameConfig.asset")'),
    fieldValues: z.record(z.any()).optional().describe('Optional JSON object of serialized field values to initialize on the ScriptableObject'),
    reason: z.string().optional().describe('Optional explanation of why this ScriptableObject is being created')
});
export function registerCreateScriptableObjectTool(server, mcpUnity, logger) {
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
async function toolHandler(mcpUnity, params) {
    if (!params.className || !params.assetPath) {
        throw new McpUnityError(ErrorType.VALIDATION, "Both 'className' and 'assetPath' must be provided.");
    }
    const response = await mcpUnity.sendRequest({
        method: toolName,
        params
    }, { timeout: getToolTimeout(toolName) });
    if (!response.success) {
        throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || `Failed to create ScriptableObject`);
    }
    return {
        content: [{
                type: response.type || 'text',
                text: response.message || `Successfully created ScriptableObject`
            }],
        structuredContent: {
            assetPath: response.assetPath,
            guid: response.guid,
            className: response.className
        }
    };
}
