-- I changed the hash type of most things from SHA256 to BLAKE2b.
-- To avoid any unforeseen problems, I'm gonna go and wipe the database here so any old SHA256 hashes get wiped.

DELETE FROM ContentVersion;
DELETE FROM Content;
