// Example of how the modified TypeScript updatePropertyValue method now works

class TestViewModelRemoteClient {
    // Properties
    name: string = "";
    count: number = 0;
    
    // Mock response that simulates success
    private createMockSuccessResponse(): any {
        return {
            getSuccess: () => true,  // This indicates the update was successful
            // ... other response properties
        };
    }

    // The modified updatePropertyValue method (simplified version)
    async updatePropertyValue(propertyName: string, value: any): Promise<any> {
        // Create and send request (simplified)
        console.log(`Updating ${propertyName} to ${value}`);
        
        // Simulate gRPC call and response
        const response = this.createMockSuccessResponse();
        
        // NEW FUNCTIONALITY: If the response indicates success, update the local property value
        if (typeof response.getSuccess === 'function' && response.getSuccess()) {
            this.updateLocalProperty(propertyName, value);
            console.log(`? Local property ${propertyName} updated to ${value}`);
        } else {
            console.log(`? Update failed, local property not changed`);
        }
        
        return response;
    }

    // Helper method to update local properties
    private updateLocalProperty(propertyName: string, value: any): void {
        const camelCasePropertyName = this.toCamelCase(propertyName);
        
        // Update the local property if it exists
        if (camelCasePropertyName in this) {
            (this as any)[camelCasePropertyName] = value;
            console.log(`Local property ${camelCasePropertyName} updated to: ${value}`);
        }
    }

    private toCamelCase(str: string): string {
        return str.charAt(0).toLowerCase() + str.slice(1);
    }
}

// Example usage:
async function demonstrateFeature() {
    const client = new TestViewModelRemoteClient();
    
    console.log("=== Before Update ===");
    console.log(`Name: ${client.name}`);
    console.log(`Count: ${client.count}`);
    
    console.log("\n=== Performing Updates ===");
    await client.updatePropertyValue("Name", "Updated Name");
    await client.updatePropertyValue("Count", 42);
    
    console.log("\n=== After Update ===");
    console.log(`Name: ${client.name}`);
    console.log(`Count: ${client.count}`);
}

// demonstrateFeature();