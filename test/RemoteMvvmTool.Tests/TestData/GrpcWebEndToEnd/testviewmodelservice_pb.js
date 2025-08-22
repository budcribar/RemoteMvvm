// Generated protobuf JavaScript stubs for testing
exports.TestViewModelState = class {
    constructor() { 
        this.zoneList = []; 
    }
    
    static deserializeBinary(bytes) {
        const instance = new this();
        // Simple protobuf parsing - look for repeated field 1 (zone_list)
        let pos = 0;
        while (pos < bytes.length) {
            if (pos >= bytes.length) break;
            
            // Read varint tag
            let tag = 0;
            let shift = 0;
            while (pos < bytes.length) {
                const byte = bytes[pos++];
                tag |= (byte & 0x7F) << shift;
                if ((byte & 0x80) === 0) break;
                shift += 7;
            }
            
            const fieldNumber = tag >> 3;
            const wireType = tag & 0x7;
            
            if (fieldNumber === 1 && wireType === 2) { // repeated zone_list field
                // Read length
                let length = 0;
                shift = 0;
                while (pos < bytes.length) {
                    const byte = bytes[pos++];
                    length |= (byte & 0x7F) << shift;
                    if ((byte & 0x80) === 0) break;
                    shift += 7;
                }
                
                if (pos + length <= bytes.length) {
                    const zoneBytes = bytes.slice(pos, pos + length);
                    const zone = exports.ThermalZoneComponentViewModelState.deserializeBinary(zoneBytes);
                    instance.zoneList.push(zone);
                    pos += length;
                } else {
                    break;
                }
            } else {
                // Skip unknown field
                break;
            }
        }
        return instance;
    }
    
    getZoneListList() { 
        return this.zoneList; 
    }
    
    setZoneListList(value) { 
        this.zoneList = value; 
    }
};

exports.ThermalZoneComponentViewModelState = class {
    constructor() { 
        this.zone = 0; 
        this.temperature = 0; 
    }
    
    static deserializeBinary(bytes) {
        const instance = new this();
        let pos = 0;
        
        while (pos < bytes.length) {
            if (pos >= bytes.length) break;
            
            // Read varint tag
            let tag = 0;
            let shift = 0;
            while (pos < bytes.length) {
                const byte = bytes[pos++];
                tag |= (byte & 0x7F) << shift;
                if ((byte & 0x80) === 0) break;
                shift += 7;
            }
            
            const fieldNumber = tag >> 3;
            
            if (fieldNumber === 1) { // zone field
                let value = 0;
                shift = 0;
                while (pos < bytes.length) {
                    const byte = bytes[pos++];
                    value |= (byte & 0x7F) << shift;
                    if ((byte & 0x80) === 0) break;
                    shift += 7;
                }
                instance.zone = value;
            } else if (fieldNumber === 2) { // temperature field
                let value = 0;
                shift = 0;
                while (pos < bytes.length) {
                    const byte = bytes[pos++];
                    value |= (byte & 0x7F) << shift;
                    if ((byte & 0x80) === 0) break;
                    shift += 7;
                }
                instance.temperature = value;
            } else {
                // Skip unknown field
                break;
            }
        }
        
        return instance;
    }
    
    getZone() { 
        return this.zone; 
    }
    
    setZone(value) { 
        this.zone = value; 
    }
    
    getTemperature() { 
        return this.temperature; 
    }
    
    setTemperature(value) { 
        this.temperature = value; 
    }
};

// Export for CommonJS compatibility
if (typeof module !== 'undefined' && module.exports) {
    module.exports = exports;
}
