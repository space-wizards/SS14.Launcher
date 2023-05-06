-- Stores selected server filters for the server list.
-- Each row is one checked filter. Maps to the ServerFilter type in C#.
CREATE TABLE ServerFilter (
    Category INTEGER NOT NULL,
    Data TEXT NOT NULL,

    CONSTRAINT CategoryData PRIMARY KEY (Category, Data),
    -- 0 isn't a valid filter category.
    CONSTRAINT CategoryValid CHECK (Category <> 0),
    -- Data probably can't be empty.
    CONSTRAINT DataNotEmpty CHECK (Data <> '')
);

-- Set default filters up to not show 18+ servers.
INSERT INTO ServerFilter (Category, Data) VALUES (4, 'false');
