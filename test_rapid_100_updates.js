const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');

// Try to import the generated classes
let TestViewModelServiceClient, UpdatePropertyValueRequest, TestViewModelState;

try {
    const serviceModule = require('./TestViewModelService_grpc_web_pb.js');
    TestViewModelServiceClient = serviceModule.TestViewModelServiceClient;
    
    const messageModule = require('./TestViewModelService_pb.js');
    UpdatePropertyValueRequest = messageModule.UpdatePropertyValueRequest;
    TestViewModelState = messageModule.TestViewModelState;
    
    console.log('? Successfully imported generated gRPC classes');
} catch (error) {
    console.error('? Error importing generated classes:', error.message);
    console.log('Available files:', require('fs').readdirSync('.').filter(f => f.includes('pb')));
    process.exit(1);
}

async function test100RapidUpdates() {
    console.log('?? Testing 100 Rapid Property Updates');
    console.log('=====================================');
    
    const client = new TestViewModelServiceClient('http://localhost:5000');
    
    try {
        // Get initial state
        console.log('?? Getting initial state...');
        const initialState = await client.getState(new Empty());
        
        // Discover message property
        const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(initialState));
        const getters = methods.filter(m => m.startsWith('get') && typeof initialState[m] === 'function');
        const messageGetter = getters.find(m => m.toLowerCase().includes('message')) || getters.find(m => m.toLowerCase().includes('status'));
        
        let initialMessage = 'unknown';
        if (messageGetter) {
            try {
                initialMessage = initialState[messageGetter]() || 'empty';
            } catch (e) {
                console.log(`Warning: Could not call ${messageGetter}: ${e.message}`);
            }
        }
        
        console.log(`?? Initial property value: "${initialMessage}"`);
        console.log(`?? Using getter method: ${messageGetter || 'none found'}`);
        
        // Prepare 100 update requests
        const updates = [];
        const startTime = Date.now();
        
        console.log('\n?? Preparing 100 update requests...');
        
        for (let i = 1; i <= 100; i++) {
            const request = new UpdatePropertyValueRequest();
            request.setPropertyName('Message'); // or 'Status' depending on your ViewModel
            
            const stringValue = new StringValue();
            stringValue.setValue(`Update_${i}_${Date.now()}`);
            
            const anyValue = new Any();
            anyValue.pack(stringValue.serializeBinary(), 'google.protobuf.StringValue');
            request.setNewValue(anyValue);
            
            updates.push({
                id: i,
                request: request,
                expectedValue: `Update_${i}_${Date.now()}`
            });
        }
        
        console.log('? All 100 requests prepared');
        
        // Send all updates rapidly
        console.log('\n? Sending 100 rapid updates...');
        const responses = [];
        const promises = [];
        
        for (const update of updates) {
            const promise = client.updatePropertyValue(update.request)
                .then(response => {
                    return {
                        id: update.id,
                        success: response.getSuccess ? response.getSuccess() : true,
                        error: response.getErrorMessage ? response.getErrorMessage() : null,
                        timestamp: Date.now()
                    };
                })
                .catch(error => {
                    return {
                        id: update.id,
                        success: false,
                        error: error.message,
                        timestamp: Date.now()
                    };
                });
            promises.push(promise);
        }
        
        // Wait for all updates to complete
        console.log('? Waiting for all 100 updates to complete...');
        const allResponses = await Promise.all(promises);
        
        const endTime = Date.now();
        const totalTime = endTime - startTime;
        
        console.log(`\n?? All updates completed in ${totalTime}ms (avg: ${(totalTime/100).toFixed(1)}ms per update)`);
        
        // Analyze results
        const successful = allResponses.filter(r => r.success);
        const failed = allResponses.filter(r => !r.success);
        
        console.log('\n?? RESULTS ANALYSIS:');
        console.log(`? Successful updates: ${successful.length}/100`);
        console.log(`? Failed updates: ${failed.length}/100`);
        
        if (failed.length > 0) {
            console.log('\n? FAILED UPDATES:');
            failed.forEach(f => {
                console.log(`   Update ${f.id}: ${f.error}`);
            });
        }
        
        // Get final state
        console.log('\n?? Getting final state...');
        const finalState = await client.getState(new Empty());
        
        let finalMessage = 'unknown';
        if (messageGetter) {
            try {
                finalMessage = finalState[messageGetter]() || 'empty';
            } catch (e) {
                console.log(`Warning: Could not get final state: ${e.message}`);
            }
        }
        
        console.log(`?? Final property value: "${finalMessage}"`);
        
        // Validate final state
        const lastUpdate = updates[updates.length - 1];
        const expectedFinalValue = lastUpdate.expectedValue;
        
        console.log(`?? Expected final value: "${expectedFinalValue}"`);
        
        // Check if final value contains expected pattern (accounting for potential race conditions)
        const finalValueValid = finalMessage.includes('Update_') && finalMessage.includes('_');
        
        console.log('\n?? FINAL VALIDATION:');
        console.log(`   All updates successful: ${failed.length === 0 ? '?' : '?'}`);
        console.log(`   No crashes/timeouts: ? (test completed)`);
        console.log(`   Final value format valid: ${finalValueValid ? '?' : '?'}`);
        console.log(`   Total time reasonable: ${totalTime < 30000 ? '?' : '?'} (${totalTime}ms < 30s)`);
        
        // Overall test result
        const testPassed = (
            failed.length === 0 &&           // All updates successful
            finalValueValid &&               // Final value has correct format
            totalTime < 30000               // Completed within reasonable time
        );
        
        console.log('\n?? OVERALL TEST RESULT:');
        if (testPassed) {
            console.log('? SUCCESS: 100 rapid updates test PASSED!');
            console.log('   - No deadlocks detected');
            console.log('   - All property updates successful');
            console.log('   - Threading appears stable');
            process.exit(0);
        } else {
            console.log('? FAILURE: 100 rapid updates test FAILED!');
            console.log('   - Check threading issues');
            console.log('   - Check property update mechanism');
            console.log('   - Check for race conditions');
            process.exit(1);
        }
        
    } catch (error) {
        console.error('?? CRITICAL ERROR in 100 updates test:', error.message);
        console.error(error.stack || error);
        
        console.log('\n?? ERROR ANALYSIS:');
        if (error.message.includes('timeout')) {
            console.log('? Likely deadlock detected - updates timed out');
        } else if (error.message.includes('connection')) {
            console.log('? Connection issue - server may have crashed');
        } else {
            console.log('? Unknown error - check server logs');
        }
        
        process.exit(1);
    }
}

// Run the test
test100RapidUpdates();

// Safety timeout
setTimeout(() => {
    console.error('? TIMEOUT: Test took longer than 60 seconds');
    console.log('\n?? TIMEOUT ANALYSIS:');
    console.log('? This suggests a deadlock or severe performance issue');
    console.log('? Check PropertyChanged event handling');
    console.log('? Check dispatcher thread usage');
    console.log('? Check event handler add/remove logic');
    process.exit(1);
}, 60000);