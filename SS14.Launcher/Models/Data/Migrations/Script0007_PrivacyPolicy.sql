-- Represents privacy policies that have been accepted by the user.
-- Each policy is stored with its server-unique identifier and version value.
CREATE TABLE AcceptedPrivacyPolicy(
    -- The "identifier" field from the privacy_policy server info response.
    Identifier TEXT NOT NULL PRIMARY KEY,

    -- The "version" field from the privacy_policy server info response.
    Version TEXT NOT NULL,

    -- The time the user accepted the privacy policy for the first time.
    AcceptedTime DATETIME NOT NULL,

    -- The last time the user connected to a server using this privacy policy.
    -- Intended to enable culling of this table for servers the user has not connected to in a long time,
    -- though this is not currently implemented at the time of writing.
    LastConnected DATETIME NOT NULL
);
