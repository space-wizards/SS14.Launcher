CREATE TABLE Hub (
    Address TEXT NOT NULL PRIMARY KEY,
    Priority INTEGER NOT NULL UNIQUE, -- 0 is highest priority

    -- Address can't be empty
    CONSTRAINT AddressNotEmpty CHECK (Address <> ''),
    -- Ensure priority is >= 0
    CONSTRAINT PriorityNotNegative CHECK (Priority >= 0)
);

-- Set Space Wizards hub as default
INSERT INTO Hub (Address, Priority) VALUES ('https://central.spacestation14.io/hub/', 0);
