-- Describes a single server version currently stored in this database.
CREATE TABLE ContentVersion(
    -- Autoincrement to avoid use-after-free type stuff if somebody clears the DB while the game is running.
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- SHA256 hash of the FULL manifest for this version.
    Hash BLOB NOT NULL,
    -- Fork & version ID reported by server.
    -- This is exclusively used to improve heuristics for which versions to evict from the download cache.
    -- It should not be trusted for security reasons.
    ForkId TEXT NULL,
    ForkVersion TEXT NULL,
    -- Last time this version was used, so we cull old ones.
    LastUsed DATE NOT NULL,
    -- If this version was downloaded via a non-delta zip file, the SHA256 hash of the zip file.
    -- This is used to provide backwards compatibility to servers
    -- that do not support the infrastructure for delta updates.
    ZipHash BLOB NULL
    -- Used engine version is stored in "ContentEngineDependency" table as 'Robust' module.
);

-- Stores the actual content of game files.
CREATE TABLE Content(
    -- Autoincrement to avoid use-after-free type stuff if somebody clears the DB while the game is running.
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- SHA256 hash of the (uncompressed) data stored in this file.
    -- Unique constraint to not allow duplicate blobs in the database.
    -- Also should be backed by an index allowing us to efficiently look up existing blobs when writing.
    Hash BLOB NOT NULL UNIQUE,
    -- Uncompressed size of the data stored in this file.
    Size INTEGER NOT NULL,
    -- Compression scheme used to store this file.
    -- See ContentCompressionScheme enum for
    Compression INTEGER NOT NULL,
    -- Actual data for the file. May be compressed based on "Compression".
    Data BLOB NOT NULL,
    -- Simple check: if a file is uncompressed, "Size" MUST match "Data" length.
    CONSTRAINT UncompressedSameSize CHECK(Compression != 0 OR length(Data) = Size)
);

-- Stores the actual file list for each server version.
CREATE TABLE ContentManifest(
    Id INTEGER PRIMARY KEY,
    -- Reference to ContentVersion to see which server version this belongs to.
    VersionId INTEGER NOT NULL REFERENCES ContentVersion(Id) ON DELETE CASCADE,
    -- File path of the game file in question.
    Path TEXT NOT NULL,
    -- Reference to the actual content blob.
    -- Do not allow a file to be deleted
    ContentId INTEGER NOT NULL REFERENCES Content(Id) ON DELETE RESTRICT,

    -- Make sure that we aren't writing directory entries from the zip into this.
    -- Just something I had happen in development, it only wastes DB space.
    CONSTRAINT NotDirectory CHECK (Path NOT LIKE '%/')
);

-- Can't have a duplicate path entry for a single version.
CREATE UNIQUE INDEX ContentManifestUniqueIndex ON ContentManifest(VersionId, Path);

-- Engine dependencies needed by a specified ContentVersion.
-- This includes both the base engine version (stored as the Robust module).
-- And any extra modules such as Robust.Client.WebView.
-- This does NOT describe the actual on-disk available modules and engines, only the ones which are *necessary*.
-- This is intended to be checked before the game is launched and while updating.
-- (the reason for this is so that updating doesn't have to be considered uninterruptible
-- a *massive* 'transaction' of content + engine + modules).
CREATE TABLE ContentEngineDependency(
    Id INTEGER PRIMARY KEY,
    -- Reference to ContentVersion to see which server version this belongs to.
    VersionId INTEGER NOT NULL REFERENCES ContentVersion(Id) ON DELETE CASCADE,
    -- The name of the module. 'Robust' means this module is actually the base server version.
    ModuleName TEXT NOT NULL,
    -- The version of the module.
    ModuleVersion TEXT NOT NULL
);

-- Cannot have multiple versions of the same module for a single installed version.
CREATE UNIQUE INDEX ContentEngineModuleUniqueIndex ON ContentEngineDependency(VersionId, ModuleName);
