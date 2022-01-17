CREATE TABLE EngineModule (
    Name TEXT NOT NULL,
    Version TEXT NOT NULL,

    CONSTRAINT NameVersion PRIMARY KEY (Name, Version)
);
