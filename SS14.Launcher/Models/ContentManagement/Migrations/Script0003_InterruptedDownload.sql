-- Represents a content download that was interrupted.
-- Used to reference the content blobs that were successfully downloaded,
-- so that they are not immediately garbage collected.
CREATE TABLE InterruptedDownload(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- Date the download was interrupted at.
    -- Used to eventually remove this from the database if unused.
    Added DATE NOT NULL
);

-- A single content blob of which the download was interrupted.
CREATE TABLE InterruptedDownloadContent(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    -- The ID of the interrupted download row.
    InterruptedDownloadId INTEGER NOT NULL REFERENCES InterruptedDownload(Id) ON DELETE CASCADE,
    -- The ID of the content blob that was downloaded.
    -- This value is unique: a new download shouldn't be re-downloading a blob if we already have it.
    -- Also, we need the index for GC purposes.
    ContentId INTEGER NOT NULL UNIQUE REFERENCES Content(Id) ON DELETE CASCADE
)
