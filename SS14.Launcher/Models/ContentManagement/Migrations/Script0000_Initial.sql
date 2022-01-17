-- WAL is probably a better idea for this than journal.
-- So that you can connect to another server while a game is running.
PRAGMA journal_mode=WAL;

-- Describes a single server version currently stored in this database.
CREATE TABLE ContentVersion(
    Id INTEGER PRIMARY KEY,
    -- Hash of the FULL manifest for this version.
    Hash TEXT NOT NULL,
    -- Fork & version ID reported by server.
    -- This is exclusively used to improve heuristics for which versions to evict from the download cache.
    -- It should not be trusted for security reasons.
    ForkId TEXT NULL,
    ForkVersion TEXT NULL,
    -- Engine version used by this content version, so we know which engine versions can be culled.
    EngineVersion TEXT NOT NULL,
    -- Last time this version was used, so we cull old ones.
    LastUsed DATE NOT NULL
);

-- Stores the actual content of game files.
CREATE TABLE Content(
    Id INTEGER PRIMARY KEY,
    -- SHA256 hash of the (uncompressed) data stored in this file.
    Hash BLOB NOT NULL,
    -- Uncompressed size of the data stored in this file.
    Size INTEGER NOT NULL,
    -- Compression algorithm used to store this file.
    -- 0: no compression.
    Compression INTEGER NOT NULL,
    -- Actual data for the file. May be compressed based on "Compression".
    Data BLOB NOT NULL,
    -- Simple check: if a file is uncompressed, "Size" MUST match "Data" length.
    CONSTRAINT UncompressedSameSize CHECK(Compression != 0 OR length(Data) = Size)
);

-- Index to efficiently look up entries when writing new data into DB.
-- Not used when reading, because ContentManifest references the int PK.
CREATE UNIQUE INDEX ContentHashIndex ON Content(Hash);

-- Stores the actual file list for each server version.
CREATE TABLE ContentManifest(
    Id INTEGER PRIMARY KEY,
    -- Reference to ContentVersion to see which server version this belongs to.
    VersionId INTEGER NOT NULL REFERENCES ContentVersion(Id) ON DELETE CASCADE,
    -- File path of the game file in question.
    Path TEXT NOT NULL,
    -- Reference to the actual content blob.
    -- Do not allow a file to be deleted
    ContentId INTEGER NOT NULL REFERENCES Content(Id) ON DELETE RESTRICT
);

-- Can't have a duplicate path entry for a single version.
CREATE UNIQUE INDEX ContentManifestUniqueIndex ON ContentManifest(VersionId, Path);
