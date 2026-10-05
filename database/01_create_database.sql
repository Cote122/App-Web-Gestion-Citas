/* Opcional: Entity Framework puede crear la base automáticamente en desarrollo. */
IF DB_ID(N'GestionCitasDb') IS NULL
BEGIN
    CREATE DATABASE GestionCitasDb;
END;
GO
