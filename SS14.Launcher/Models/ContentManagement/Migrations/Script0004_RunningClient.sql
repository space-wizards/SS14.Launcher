-- Tracks running game client processes.
-- This is used to disallow content removal if a client is running.
CREATE TABLE RunningClient(
    -- OS process ID of the client.
    ProcessId INTEGER PRIMARY KEY NOT NULL,

    -- Main module of the process as gotten by .NET, used to tell if a PID was re-used.
    MainModule TEXT NOT NULL,

    -- Content version in use by this client.
    UsedVersion INTEGER NOT NULL REFERENCES ContentVersion(Id) ON DELETE RESTRICT
);
