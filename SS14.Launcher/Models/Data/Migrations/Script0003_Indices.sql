-- Wow I can't believe I didn't have unique indices for these.

CREATE UNIQUE INDEX LoginUniqueId ON Login(UserId);
CREATE UNIQUE INDEX ConfigUniqueKey ON Config(Key);
